using UnityEngine;

namespace PousoLua
{
    /// <summary>
    /// Locomoção dinâmica em VR com controle:
    ///  - O analógico esquerdo move o jogador na direção para onde a câmera VR (cabeça) está olhando.
    ///  - Usa CharacterController: sobe degraus/rampas, colide com o cenário e acompanha o relevo do terreno.
    ///  - O movimento é projetado no plano da superfície onde o jogador pisa (subidas e descidas suaves)
    ///    e o jogador é mantido firme no chão ao descer crateras/morros.
    ///  - A altura da câmera é suavizada para acompanhar o relevo de forma orgânica no VR.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class VRPlayerMovement : MonoBehaviour
    {
        [Header("Referências (preenchidas automaticamente se vazias)")]
        [Tooltip("Câmera do VR (cabeça). A direção do movimento segue para onde ela olha.")]
        public Transform head;
        [Tooltip("Objeto entre o Player e a câmera que define a altura dos olhos.")]
        public Transform cameraOffset;

        [Header("Movimento")]
        [Tooltip("Velocidade máxima de caminhada (m/s).")]
        public float walkSpeed = 3.0f;
        [Tooltip("Aceleração ao começar a andar (m/s²).")]
        public float acceleration = 10f;
        [Tooltip("Desaceleração ao soltar o analógico (m/s²).")]
        public float deceleration = 12f;
        [Range(0f, 0.6f)] public float deadZone = 0.15f;

        [Header("Relevo")]
        [Tooltip("Projeta o movimento na inclinação do chão para acompanhar o relevo suavemente.")]
        public bool followSlope = true;
        [Tooltip("Quanto a velocidade é reduzida em subidas íngremes (0 = nada, 1 = muito).")]
        [Range(0f, 1f)] public float uphillSlowdown = 0.25f;
        [Tooltip("Inclinação máxima que o jogador consegue subir (graus).")]
        [Range(10f, 80f)] public float maxSlope = 60f;
        [Tooltip("Altura máxima de degrau/pedra que o jogador sobe sem parar (m).")]
        public float stepHeight = 0.45f;
        [Tooltip("Distância máxima para manter contato com o chão ao descer morros/crateras (m).")]
        public float groundSnapDistance = 0.75f;
        public LayerMask groundLayers = ~0;

        [Header("Gravidade")]
        [Tooltip("Gravidade do jogador (m/s²). Lua = -1.62, Terra = -9.81.")]
        public float gravity = -1.62f;
        [Tooltip("Aplica a mesma gravidade ao motor de física (pedras e objetos caem com gravidade lunar).")]
        public bool applyGravityToPhysics = true;
        public float maxFallSpeed = 20f;

        [Header("Corpo / Câmera")]
        [Tooltip("Altura dos olhos em relação aos pés (m).")]
        public float eyeHeight = 1.6f;
        public float bodyRadius = 0.3f;
        [Tooltip("Suavização vertical da câmera ao passar por irregularidades do terreno (s).")]
        public float cameraSmoothTime = 0.08f;
        [Tooltip("Atraso vertical máximo permitido para a câmera (m).")]
        public float maxCameraLag = 0.35f;

        [Header("Giro com analógico direito")]
        public bool snapTurn = true;
        public float snapTurnAngle = 30f;
        [Range(0.3f, 0.95f)] public float snapTurnThreshold = 0.7f;

        [Header("Teste no Editor (sem headset)")]
        [Tooltip("Segure o botão direito do mouse para olhar ao redor no Editor.")]
        public bool editorMouseLook = true;
        public float mouseSensitivity = 0.15f;

        public bool IsGrounded { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 GroundNormal => groundNormal;

        CharacterController cc;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        Vector3 groundNormal = Vector3.up;
        bool snapTurnReady = true;
        float smoothedY, smoothedYVelocity;
        bool smoothInitialized;
        float editorPitch;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            ResolveReferences();
            ConfigureController();

            if (applyGravityToPhysics)
                Physics.gravity = new Vector3(0f, gravity, 0f);
        }

        void Start()
        {
            SnapToGroundImmediate();
            smoothedY = transform.position.y;
            smoothInitialized = true;
            IsGrounded = true;
        }

        void OnValidate()
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            if (cc != null && !Application.isPlaying) ConfigureController();
        }

        // ------------------------------------------------------------------ Setup

        public void ResolveReferences()
        {
            if (head == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) head = cam.transform;
                else if (Camera.main != null) head = Camera.main.transform;
            }

            if (head == null)
            {
                Debug.LogError("[VRPlayerMovement] Nenhuma câmera encontrada para usar como cabeça.", this);
                enabled = false;
                return;
            }

            if (cameraOffset == null)
            {
                Transform existingOffset = transform.Find("CameraOffset");
                if (existingOffset != null)
                {
                    cameraOffset = existingOffset;
                    if (head.parent != cameraOffset)
                    {
                        head.SetParent(cameraOffset, false);
                        head.localPosition = Vector3.zero;
                    }
                }
                else if (head.parent == transform || head.parent == null)
                {
                    var offset = new GameObject("CameraOffset").transform;
                    offset.SetParent(transform, false);
                    offset.localPosition = new Vector3(0f, eyeHeight, 0f);
                    head.SetParent(offset, false);
                    head.localPosition = Vector3.zero;
                    cameraOffset = offset;
                }
                else
                {
                    cameraOffset = head.parent;
                }
            }
        }

        public void ConfigureController()
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            if (cc == null) return;

            float height = Mathf.Max(eyeHeight + 0.15f, bodyRadius * 2f + 0.01f);
            cc.height = height;
            cc.radius = bodyRadius;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
            cc.slopeLimit = maxSlope;
            cc.stepOffset = Mathf.Clamp(stepHeight, 0f, height - 0.01f);
            cc.minMoveDistance = 0f;
        }

        /// <summary>Teleporta o jogador para uma posição (pés).</summary>
        public void Teleport(Vector3 feetPosition)
        {
            cc.enabled = false;
            transform.position = feetPosition;
            cc.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            smoothedY = feetPosition.y;
            smoothedYVelocity = 0f;
        }

        public void SnapToGroundImmediate()
        {
            Vector3 pos = transform.position;
            float? ground = SampleGroundHeight(pos);

            if (ground.HasValue)
            {
                bool wasEnabled = cc != null && cc.enabled;
                if (wasEnabled) cc.enabled = false;
                transform.position = new Vector3(pos.x, ground.Value + 0.02f, pos.z);
                if (wasEnabled) cc.enabled = true;
            }
        }

        float? SampleGroundHeight(Vector3 pos)
        {
            float? ground = null;

            var terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 tp = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (pos.x >= tp.x && pos.x <= tp.x + size.x && pos.z >= tp.z && pos.z <= tp.z + size.z)
                    ground = terrain.SampleHeight(pos) + tp.y;
            }

            RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 30f, Vector3.down, 150f, groundLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null || col == cc) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<Grabbable>() != null) continue;

                float hy = hits[i].point.y;
                if (!ground.HasValue || hy > ground.Value)
                    ground = hy;
            }

            return ground;
        }

        // ------------------------------------------------------------------ Loop

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || head == null) return;

            HandleSnapTurn();
            HandleEditorMouseLook();

            // 1) Direção desejada a partir do analógico esquerdo e da direção onde o jogador olha no VR
            Vector2 input = ApplyDeadZone(VRInput.Move);
            GetPlanarAxes(out Vector3 forward, out Vector3 right);
            Vector3 targetVelocity = (forward * input.y + right * input.x) * walkSpeed;

            float rate = targetVelocity.sqrMagnitude >= horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * dt);

            UpdateGroundNormal();

            // 2) Acompanhar o relevo do terreno onde o jogador está pisando
            Vector3 motion = horizontalVelocity;
            if (IsGrounded)
            {
                if (followSlope && horizontalVelocity.sqrMagnitude > 1e-4f)
                {
                    float speed = horizontalVelocity.magnitude;
                    Vector3 alongGround = Vector3.ProjectOnPlane(horizontalVelocity, groundNormal).normalized;
                    float uphill = Mathf.Clamp01(alongGround.y);
                    motion = alongGround * speed * (1f - uphillSlowdown * uphill);
                }
                verticalVelocity = -1.5f; // força contínua leve para manter contato com o relevo
            }
            else
            {
                verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -maxFallSpeed);
                motion.y += verticalVelocity;
            }

            // 3) Mover com CharacterController
            bool wasGrounded = IsGrounded;
            Vector3 before = transform.position;
            CollisionFlags flags = cc.Move(motion * dt);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;

            IsGrounded = cc.isGrounded || (flags & CollisionFlags.Below) != 0;

            // Ao descer um morro/cratera ou passar por ondulações do terreno, acompanha o relevo sem flutuar
            if (!IsGrounded && wasGrounded && verticalVelocity <= 0f)
                IsGrounded = SnapDown();

            ClampAboveTerrainFloor();
            UpdateGroundNormal();
            Velocity = (transform.position - before) / dt;
        }

        void LateUpdate()
        {
            if (cameraOffset == null) return;

            float y = transform.position.y;
            if (!smoothInitialized)
            {
                smoothedY = y;
                smoothedYVelocity = 0f;
                smoothInitialized = true;
            }
            else
            {
                smoothedY = Mathf.SmoothDamp(smoothedY, y, ref smoothedYVelocity, cameraSmoothTime);
                smoothedY = Mathf.Clamp(smoothedY, y - maxCameraLag, y + maxCameraLag);
            }

            float scale = Mathf.Max(transform.lossyScale.y, 1e-4f);
            Vector3 lp = cameraOffset.localPosition;
            cameraOffset.localPosition = new Vector3(lp.x, eyeHeight + (smoothedY - y) / scale, lp.z);
        }

        // ------------------------------------------------------------------ Helpers

        Vector2 ApplyDeadZone(Vector2 v)
        {
            float m = v.magnitude;
            if (m < deadZone) return Vector2.zero;
            return v / m * Mathf.InverseLerp(deadZone, 1f, Mathf.Min(m, 1f));
        }

        void GetPlanarAxes(out Vector3 forward, out Vector3 right)
        {
            Vector3 f = head.forward;
            Vector3 flat = Vector3.ProjectOnPlane(f, Vector3.up);

            // Se estiver olhando quase verticalmente para baixo (ex.: olhando uma pedra nos pés) ou para cima,
            // usa o eixo "up" da câmera para manter a direção frontal consistente.
            if (flat.sqrMagnitude < 0.04f)
                flat = Vector3.ProjectOnPlane(f.y < 0f ? head.up : -head.up, Vector3.up);

            forward = flat.normalized;
            right = Vector3.Cross(Vector3.up, forward);
        }

        bool SnapDown()
        {
            float castRadius = cc.radius * 0.9f;
            float halfHeight = cc.height * 0.5f;
            Vector3 waist = transform.position + Vector3.up * halfHeight;
            float baseDist = Mathf.Max(0.05f, halfHeight - castRadius);

            if (Physics.SphereCast(waist, castRadius, Vector3.down, out RaycastHit hit,
                    baseDist + groundSnapDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Angle(hit.normal, Vector3.up) <= cc.slopeLimit + 8f)
                {
                    float drop = Mathf.Max(0f, hit.distance - baseDist);
                    cc.Move(Vector3.down * (drop + cc.skinWidth));
                    groundNormal = hit.normal;
                    return true;
                }
            }

            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float terrainY = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
                float diff = transform.position.y - terrainY;
                if (diff >= -0.05f && diff <= groundSnapDistance)
                {
                    cc.Move(Vector3.down * (diff + cc.skinWidth));
                    return true;
                }
            }
            return false;
        }

        void ClampAboveTerrainFloor()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            float terrainY = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
            if (transform.position.y < terrainY)
            {
                cc.enabled = false;
                Vector3 p = transform.position;
                transform.position = new Vector3(p.x, terrainY + 0.01f, p.z);
                cc.enabled = true;
                IsGrounded = true;
                if (verticalVelocity < 0f) verticalVelocity = -1f;
            }
        }

        void UpdateGroundNormal()
        {
            float castRadius = cc.radius * 0.85f;
            float halfHeight = cc.height * 0.5f;
            Vector3 waist = transform.position + Vector3.up * halfHeight;
            float baseDist = Mathf.Max(0.05f, halfHeight - castRadius);

            if (Physics.SphereCast(waist, castRadius, Vector3.down, out RaycastHit hit,
                    baseDist + 0.35f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Angle(hit.normal, Vector3.up) <= cc.slopeLimit + 10f)
                {
                    groundNormal = hit.normal;
                    return;
                }
            }

            if (Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out hit,
                    0.8f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Angle(hit.normal, Vector3.up) <= cc.slopeLimit + 10f)
                {
                    groundNormal = hit.normal;
                    return;
                }
            }

            groundNormal = Vector3.up;
        }

        void HandleSnapTurn()
        {
            if (!snapTurn) return;

            float x = VRInput.Turn;
            if (snapTurnReady && Mathf.Abs(x) >= snapTurnThreshold)
            {
                transform.Rotate(0f, Mathf.Sign(x) * snapTurnAngle, 0f, Space.World);
                snapTurnReady = false;
            }
            else if (Mathf.Abs(x) < snapTurnThreshold * 0.5f)
            {
                snapTurnReady = true;
            }
        }

        void HandleEditorMouseLook()
        {
#if UNITY_EDITOR
            if (!editorMouseLook || cameraOffset == null) return;

            Vector2 d = VRInput.EditorLookDelta * mouseSensitivity;
            if (d.sqrMagnitude < 1e-6f) return;

            transform.Rotate(0f, d.x, 0f, Space.World);
            editorPitch = Mathf.Clamp(editorPitch - d.y, -85f, 85f);
            cameraOffset.localRotation = Quaternion.Euler(editorPitch, 0f, 0f);
#endif
        }
    }
}
