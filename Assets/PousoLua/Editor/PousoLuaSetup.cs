using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PousoLua.EditorTools
{
    /// <summary>
    /// Menu "PousoLua" e configuração automática da cena no Editor.
    /// </summary>
    [InitializeOnLoad]
    public static class PousoLuaSetup
    {
        const string GeneratedFolder = "Assets/PousoLua/Generated";
        const float RockDensity = 2500f; // kg/m³ (rocha lunar ~2.5–3.3 g/cm³)
        const int InteractiveReticleLayer = 7;

        static PousoLuaSetup()
        {
            EditorApplication.delayCall += AutoConfigureActiveSceneIfNeeded;
        }

        static void AutoConfigureActiveSceneIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;

            bool changed = false;
            if (Object.FindAnyObjectByType<VRPlayerMovement>() == null || cam.GetComponent<VRObjectGrabber>() == null)
            {
                SetupPlayer();
                changed = true;
            }

            if (GameObject.Find("Pedras") == null)
            {
                SpawnRocks();
                changed = true;
            }

            if (changed)
            {
                int mapRocks = PousoLuaBootstrap.SetupExistingMapRocks();
                Debug.Log($"[PousoLua] Cena configurada automaticamente! ({mapRocks} rochas do mapa configuradas como pegáveis).");
            }
        }

        [MenuItem("PousoLua/1. Configurar Player VR (movimento + pegar objetos)", priority = 0)]
        public static void SetupPlayer()
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                EditorUtility.DisplayDialog("PousoLua", "Nenhuma câmera encontrada na cena.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Configurar Player VR");
            int group = Undo.GetCurrentGroup();

            // --- Player (raiz) ---
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo == null)
            {
                Transform root = cam.transform.root;
                playerGo = root != cam.transform ? root.gameObject : null;
            }
            if (playerGo == null)
            {
                playerGo = new GameObject("Player");
                Undo.RegisterCreatedObjectUndo(playerGo, "Criar Player");
                playerGo.transform.position = cam.transform.position - Vector3.up * 1.6f;
                Undo.SetTransformParent(cam.transform, playerGo.transform, "Parent camera");
            }
            Transform player = playerGo.transform;

            if (playerGo.GetComponent<CharacterController>() == null)
                Undo.AddComponent<CharacterController>(playerGo);

            var movement = playerGo.GetComponent<VRPlayerMovement>();
            if (movement == null) movement = Undo.AddComponent<VRPlayerMovement>(playerGo);

            // --- CameraOffset (altura dos olhos) ---
            Transform offset = cam.transform.parent;
            if (offset == null || offset == player)
            {
                Transform existingOffset = player.Find("CameraOffset");
                if (existingOffset != null)
                {
                    offset = existingOffset;
                }
                else
                {
                    var offsetGo = new GameObject("CameraOffset");
                    Undo.RegisterCreatedObjectUndo(offsetGo, "Criar CameraOffset");
                    offset = offsetGo.transform;
                    Undo.SetTransformParent(offset, player, "Parent CameraOffset");
                }
                Undo.SetTransformParent(cam.transform, offset, "Parent camera");
            }

            Undo.RecordObject(offset, "Offset");
            offset.localPosition = new Vector3(0f, movement.eyeHeight, 0f);
            offset.localRotation = Quaternion.identity;
            offset.localScale = Vector3.one;

            Undo.RecordObject(cam.transform, "Camera");
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;

            Undo.RecordObject(movement, "Movement refs");
            movement.head = cam.transform;
            movement.cameraOffset = offset;
            movement.ConfigureController();

            // --- Coloca os pés do jogador no chão ---
            Undo.RecordObject(player, "Player position");
            player.position = GroundPoint(player.position) + Vector3.up * 0.02f;

            // --- Pegador estilo gravity gun na câmera ---
            var grabber = cam.GetComponent<VRObjectGrabber>();
            if (grabber == null) grabber = Undo.AddComponent<VRObjectGrabber>(cam.gameObject);
            Undo.RecordObject(grabber, "Grabber refs");
            grabber.head = cam.transform;
            grabber.playerBody = playerGo.GetComponent<CharacterController>();

            PousoLuaBootstrap.SetupExistingMapRocks();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(playerGo.scene);
            Selection.activeGameObject = playerGo;

            Debug.Log("[PousoLua] Player VR configurado: CharacterController + VRPlayerMovement no Player, " +
                      "CameraOffset criado e VRObjectGrabber na câmera. Salve a cena (Ctrl+S).");
        }

        [MenuItem("PousoLua/2. Espalhar pedras pegáveis perto do Player", priority = 1)]
        public static void SpawnRocks()
        {
            Vector3 origin = Vector3.zero;
            Vector3 forward = Vector3.forward;
            var player = GameObject.Find("Player");
            if (player != null)
            {
                origin = player.transform.position;
                forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            }

            EnsureFolder(GeneratedFolder);
            Material mat = GetOrCreateRockMaterial();

            Undo.SetCurrentGroupName("Criar pedras");
            int group = Undo.GetCurrentGroup();

            GameObject parent = GameObject.Find("Pedras");
            if (parent == null)
            {
                parent = new GameObject("Pedras");
                Undo.RegisterCreatedObjectUndo(parent, "Criar Pedras");
            }

            int existing = parent.transform.childCount;
            const int count = 8;
            for (int i = 0; i < count; i++)
            {
                int seed = 1000 + existing + i;
                var rng = new System.Random(seed);
                float radius = Mathf.Lerp(0.14f, 0.32f, (float)rng.NextDouble());

                Mesh mesh = GetOrCreateRockMesh(seed, radius);

                // Distribui em um semicírculo de 1.5 a 3.8 m na frente do jogador.
                float angle = Mathf.Lerp(-75f, 75f, (i + 0.5f) / count) + ((float)rng.NextDouble() - 0.5f) * 10f;
                float dist = Mathf.Lerp(1.6f, 3.6f, (float)rng.NextDouble());
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 pos = GroundPoint(origin + dir * dist) + Vector3.up * (mesh.bounds.extents.y + 0.05f);

                var go = new GameObject($"Pedra_{existing + i + 1:00}");
                Undo.RegisterCreatedObjectUndo(go, "Criar pedra");
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
                rb.mass = Mathf.Clamp(volume * RockDensity, 0.5f, 50f);
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                go.AddComponent<Grabbable>();
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(parent.scene);
            Selection.activeGameObject = parent;
            Debug.Log($"[PousoLua] {count} pedras criadas em '{parent.name}'. Salve a cena (Ctrl+S).");
        }

        [MenuItem("PousoLua/Tornar seleção pegável (Rigidbody + Grabbable)", priority = 20)]
        public static void MakeSelectionGrabbable()
        {
            foreach (var go in Selection.gameObjects)
            {
                go.layer = InteractiveReticleLayer;
                if (go.GetComponentInChildren<Collider>() == null)
                {
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = Undo.AddComponent<MeshCollider>(go);
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                    }
                    else Undo.AddComponent<BoxCollider>(go);
                }
                if (go.GetComponent<Rigidbody>() == null) Undo.AddComponent<Rigidbody>(go);
                if (go.GetComponent<Grabbable>() == null) Undo.AddComponent<Grabbable>(go);
            }
            Debug.Log($"[PousoLua] {Selection.gameObjects.Length} objeto(s) agora podem ser pegos.");
        }

        [MenuItem("PousoLua/Tornar seleção pegável (Rigidbody + Grabbable)", true)]
        static bool MakeSelectionGrabbableValidate() => Selection.gameObjects.Length > 0;

        // ------------------------------------------------------------------ Helpers

        static Vector3 GroundPoint(Vector3 p)
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
                return new Vector3(p.x, hit.point.y, p.z);

            return p;
        }

        static Mesh GetOrCreateRockMesh(int seed, float radius)
        {
            string path = $"{GeneratedFolder}/RockMesh_{seed}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = RockMeshGenerator.Generate(seed, radius);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material GetOrCreateRockMaterial()
        {
            string path = $"{GeneratedFolder}/RockMaterial.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "RockMaterial" };
            var grey = new Color(0.42f, 0.41f, 0.40f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", grey);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", grey);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.12f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
