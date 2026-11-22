using System;
using UnityEngine;

namespace PousoLua
{
    /// <summary>
    /// Inicializador automático em runtime:
    /// Garante que, mesmo que a cena não tenha sido configurada manualmente pelo menu do Editor,
    /// o Player tenha movimentação VR dinâmica acompanhando o relevo, a câmera tenha o VRObjectGrabber,
    /// todas as rochas do mapa ("Rocha lunar...", "Detrito de regolito...") possam ser pegas,
    /// e existam pedras soltas perto do jogador ao iniciar.
    /// </summary>
    public static class PousoLuaBootstrap
    {
        const int InteractiveReticleLayer = 7; // Layer interativa configurada no CardboardReticlePointer (máscara 384)
        const float RockDensity = 2500f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeScene()
        {
            Camera cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;

            VRPlayerMovement movement = SetupPlayerRuntime(cam);
            SetupExistingMapRocks();
            EnsureRocksNearPlayer(movement != null ? movement.transform : cam.transform);
        }

        public static VRPlayerMovement SetupPlayerRuntime(Camera cam)
        {
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo == null)
            {
                Transform root = cam.transform.root;
                if (root != cam.transform)
                {
                    playerGo = root.gameObject;
                }
                else
                {
                    playerGo = new GameObject("Player");
                    playerGo.transform.position = cam.transform.position - Vector3.up * 1.6f;
                    cam.transform.SetParent(playerGo.transform, false);
                }
            }

            if (playerGo.GetComponent<CharacterController>() == null)
                playerGo.AddComponent<CharacterController>();

            var movement = playerGo.GetComponent<VRPlayerMovement>();
            if (movement == null)
                movement = playerGo.AddComponent<VRPlayerMovement>();

            movement.ResolveReferences();
            movement.ConfigureController();
            //movement.SnapToGroundImmediate();

            var grabber = cam.GetComponent<VRObjectGrabber>();
            if (grabber == null)
                grabber = cam.gameObject.AddComponent<VRObjectGrabber>();

            grabber.head = cam.transform;
            grabber.playerBody = playerGo.GetComponent<CharacterController>();

            return movement;
        }

        /// <summary>
        /// Torna todas as rochas existentes no mapa ("Rocha lunar fragmentada...", "Detrito de regolito...", "Pedra...")
        /// pegáveis pelo jogador. Elas iniciam como isKinematic = true para custo zero de física até serem pegas,
        /// passando a ter física dinâmica com gravidade lunar assim que o jogador as pega/solta.
        /// </summary>
        public static int SetupExistingMapRocks()
        {
            int configuredCount = 0;
            MeshFilter[] meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>();

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter mf = meshFilters[i];
                if (mf == null || mf.sharedMesh == null) continue;

                GameObject go = mf.gameObject;
                if (IsRockName(go.name))
                {
                    ConfigureRockObject(go, mf.sharedMesh, startKinematic: go.GetComponent<Rigidbody>() == null);
                    configuredCount++;
                }
            }

            return configuredCount;
        }

        static bool IsRockName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("Rocha lunar", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Detrito de regolito", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Pedra", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Rock", StringComparison.OrdinalIgnoreCase);
        }

        public static void ConfigureRockObject(GameObject go, Mesh mesh, bool startKinematic)
        {
            if (go.layer == 0)
                go.layer = InteractiveReticleLayer;

            Bounds b = mesh.bounds;
            Vector3 lossy = go.transform.lossyScale;

            Collider col = go.GetComponent<Collider>();
            if (col == null)
            {
                var box = go.AddComponent<BoxCollider>();
                Vector3 minLocalSize = new Vector3(
                    0.10f / Mathf.Max(Mathf.Abs(lossy.x), 1e-4f),
                    0.10f / Mathf.Max(Mathf.Abs(lossy.y), 1e-4f),
                    0.10f / Mathf.Max(Mathf.Abs(lossy.z), 1e-4f)
                );
                box.center = b.center;
                box.size = Vector3.Max(b.size, minLocalSize);
            }
            else if (col is MeshCollider mc && !mc.convex)
            {
                mc.convex = true;
            }

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
                Vector3 worldSize = Vector3.Scale(b.size, new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
                float approxVolume = Mathf.Max(0.001f, worldSize.x * worldSize.y * worldSize.z * 0.52f);
                rb.mass = Mathf.Clamp(approxVolume * RockDensity, 0.5f, 45f);
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.35f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.useGravity = true;
                rb.isKinematic = startKinematic;
            }

            Grabbable grabbable = go.GetComponent<Grabbable>();
            if (grabbable == null)
                grabbable = go.AddComponent<Grabbable>();
            else
                grabbable.RefreshCache();
        }

        /// <summary>
        /// Garante que existam pedras soltas próximas à posição inicial do jogador.
        /// </summary>
        public static void EnsureRocksNearPlayer(Transform player)
        {
            if (GameObject.Find("Pedras") != null) return;

            GameObject parent = new GameObject("Pedras");
            Vector3 origin = player.position;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

            Material mat = CreateRuntimeRockMaterial();
            const int count = 8;

            for (int i = 0; i < count; i++)
            {
                int seed = 1000 + i;
                var rng = new System.Random(seed);
                float radius = Mathf.Lerp(0.14f, 0.32f, (float)rng.NextDouble());
                Mesh mesh = RockMeshGenerator.Generate(seed, radius);

                float angle = Mathf.Lerp(-75f, 75f, (i + 0.5f) / count) + ((float)rng.NextDouble() - 0.5f) * 10f;
                float dist = Mathf.Lerp(1.6f, 3.6f, (float)rng.NextDouble());
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 pos = FindGroundPoint(origin + dir * dist) + Vector3.up * (mesh.bounds.extents.y + 0.05f);

                var go = new GameObject($"Pedra_{i + 1:00}");
                go.layer = InteractiveReticleLayer;
                go.transform.SetParent(parent.transform, true);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;

                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;

                var rb = go.AddComponent<Rigidbody>();
                float volume = RockMeshGenerator.ApproximateVolume(mesh, Vector3.one);
                rb.mass = Mathf.Clamp(volume * RockDensity, 0.5f, 45f);
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.35f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                go.AddComponent<Grabbable>();
            }
        }

        static Vector3 FindGroundPoint(Vector3 p)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 tp = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (p.x >= tp.x && p.x <= tp.x + size.x && p.z >= tp.z && p.z <= tp.z + size.z)
                    return new Vector3(p.x, terrain.SampleHeight(p) + tp.y, p.z);
            }

            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<CharacterController>() == null
                && hit.collider.GetComponentInParent<Grabbable>() == null)
            {
                return new Vector3(p.x, hit.point.y, p.z);
            }

            return p;
        }

        static Material CreateRuntimeRockMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = "RockMaterial_Runtime" };
            var grey = new Color(0.45f, 0.44f, 0.43f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", grey);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", grey);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.12f);
            return mat;
        }
    }
}
