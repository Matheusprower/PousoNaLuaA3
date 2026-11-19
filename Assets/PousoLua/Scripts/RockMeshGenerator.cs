using System.Collections.Generic;
using UnityEngine;

namespace PousoLua
{
    /// <summary>
    /// Gera malhas de pedra low-poly (icosfera deformada com sombreamento facetado).
    /// </summary>
    public static class RockMeshGenerator
    {
        /// <param name="seed">Semente: cada valor gera uma pedra diferente.</param>
        /// <param name="radius">Raio aproximado (m).</param>
        /// <param name="subdivisions">0 a 3. 2 é um bom equilíbrio para mobile.</param>
        /// <param name="roughness">Quão irregular é a superfície (0..0.5).</param>
        public static Mesh Generate(int seed, float radius = 0.25f, int subdivisions = 2, float roughness = 0.28f)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();

            BuildIcosphere(Mathf.Clamp(subdivisions, 0, 3), out List<Vector3> verts, out List<int> tris);

            // "Calombos" aleatórios para deformar a esfera.
            int bumpCount = 6 + rng.Next(5);
            var bumpDirs = new Vector3[bumpCount];
            var bumpAmp = new float[bumpCount];
            for (int i = 0; i < bumpCount; i++)
            {
                bumpDirs[i] = new Vector3(R() * 2f - 1f, R() * 2f - 1f, R() * 2f - 1f).normalized;
                bumpAmp[i] = (R() * 2f - 1f) * roughness;
            }

            // Achata e estica para não parecer uma bola.
            var squash = new Vector3(0.85f + R() * 0.35f, 0.55f + R() * 0.3f, 0.8f + R() * 0.35f);

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 d = verts[i];
                float r = 1f;
                for (int b = 0; b < bumpCount; b++)
                {
                    float dot = Mathf.Max(0f, Vector3.Dot(d, bumpDirs[b]));
                    r += bumpAmp[b] * dot * dot * dot;
                }
                r += (R() - 0.5f) * roughness * 0.35f; // ruído fino
                Vector3 p = d * Mathf.Max(0.35f, r);
                verts[i] = Vector3.Scale(p, squash) * radius;
            }

            // Recentraliza no centro geométrico.
            Vector3 center = Vector3.zero;
            foreach (var v in verts) center += v;
            center /= verts.Count;

            // Desfaz o compartilhamento de vértices para sombreamento facetado (estilo low-poly).
            var fv = new Vector3[tris.Count];
            var fi = new int[tris.Count];
            var uv = new Vector2[tris.Count];
            for (int i = 0; i < tris.Count; i++)
            {
                Vector3 p = verts[tris[i]] - center;
                fv[i] = p;
                fi[i] = i;
                Vector3 n = p.normalized;
                uv[i] = new Vector2(0.5f + Mathf.Atan2(n.z, n.x) / (2f * Mathf.PI), 0.5f + Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f)) / Mathf.PI);
            }

            var mesh = new Mesh { name = $"Rock_{seed}" };
            mesh.vertices = fv;
            mesh.triangles = fi;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Volume aproximado (m³) da malha, usado para calcular a massa.</summary>
        public static float ApproximateVolume(Mesh mesh, Vector3 scale)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            float vol = 0f;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = Vector3.Scale(v[t[i]], scale);
                Vector3 b = Vector3.Scale(v[t[i + 1]], scale);
                Vector3 c = Vector3.Scale(v[t[i + 2]], scale);
                vol += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
            }
            return Mathf.Abs(vol);
        }

        static void BuildIcosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;

            tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };

            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var newTris = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Midpoint(a, b, verts, cache);
                    int bc = Midpoint(b, c, verts, cache);
                    int ca = Midpoint(c, a, verts, cache);
                    newTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = newTris;
            }
        }

        static int Midpoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }
    }
}
