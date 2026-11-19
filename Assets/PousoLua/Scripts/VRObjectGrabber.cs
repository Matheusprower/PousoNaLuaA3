using System;
using System.Collections;
using UnityEngine;

namespace PousoLua
{
    /// <summary>
    /// Sistema de pegar objetos no estilo Half-Life 2 (Gravity Gun) / Garry's Mod (Physics Gun):
    ///  - Mire com o olhar (centro da visão / retícula VR) em uma pedra com <see cref="Grabbable"/>.
    ///  - Aperte o botão do controle: a pedra é puxada suavemente e flutua na sua mira, seguindo a câmera.
    ///  - A pedra continua física: colide com o terreno e outros objetos enquanto é segurada.
    ///  - Aperte o botão novamente para soltar, ou o gatilho para arremessar.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRObjectGrabber : MonoBehaviour
    {
        [Header("Referências (preenchidas automaticamente se vazias)")]
        [Tooltip("Origem da mira. Normalmente a própria câmera VR.")]
        public Transform head;
        [Tooltip("Collider do jogador, para o objeto segurado não empurrar o próprio jogador.")]
        public CharacterController playerBody;

        [Header("Mira")]
        [Tooltip("Distância máxima para alcançar e pegar pedras no chão (m).")]
        public float maxGrabDistance = 5f;
        [Tooltip("Raio de assistência da mira para facilitar pegar pedras no VR (m).")]
        public float aimRadius = 0.25f;
        public LayerMask grabLayers = ~0;
        [Tooltip("Massa máxima que o jogador consegue segurar (kg).")]
        public float maxGrabMass = 80f;

        [Header("Segurar (Estilo Garry's Mod / Half-Life 2)")]
        [Tooltip("Distância padrão em que a pedra fica flutuando na frente da mira (m).")]
        public float holdDistance = 1.5f;
        public float minHoldDistance = 0.75f;
        public float maxHoldDistance = 3.5f;
        [Tooltip("Velocidade para aproximar/afastar a pedra com o D-pad ou analógico direito Y (m/s).")]
        public float holdDistanceSpeed = 1.5f;
        [Tooltip("Força com que a pedra é puxada até o centro da mira.")]
        public float followStrength = 16f;
        public float maxFollowSpeed = 18f;
        [Tooltip("Força com que a pedra acompanha a rotação da visão.")]
        public float rotationStrength = 14f;
        [Tooltip("Mantém a orientação da pedra relativa ao olhar do jogador.")]
        public bool keepRelativeRotation = true;
        [Tooltip("Distância máxima antes de soltar caso a pedra fique presa atrás de um obstáculo (m).")]
        public float breakDistance = 3.5f;

        [Header("Arremesso")]
        [Tooltip("Velocidade adicionada ao arremessar a pedra com o gatilho (m/s).")]
        public float throwSpeed = 8.5f;
        [Tooltip("Velocidade máxima mantida ao apenas soltar a pedra (m/s).")]
        public float maxReleaseSpeed = 6f;

        public Grabbable Held => held;
        public Grabbable Hovered => hovered;
        public event Action<Grabbable> Grabbed;
        public event Action<Grabbable> Released;

        Grabbable held;
        Grabbable hovered;
        Rigidbody heldBody;

        bool savedUseGravity;
        float savedLinearDamping, savedAngularDamping;
        RigidbodyInterpolation savedInterpolation;
        CollisionDetectionMode savedCollisionMode;

        Quaternion relativeRotation;
        float currentHoldDistance;
        float heldRadius;
        bool isPullingIn;
        float stuckTimer;
        Vector3 lastHeadPosition, headVelocity;

        void Awake()
        {
            if (head == null) head = transform;
            if (playerBody == null) playerBody = GetComponentInParent<CharacterController>();
            lastHeadPosition = head.position;
        }

        void OnDisable()
        {
            if (held != null) Release(Vector3.zero);
            SetHovered(null);
        }

        // ------------------------------------------------------------------ Loop

        void Update()
        {
            if (held != null && (heldBody == null || !held.isActiveAndEnabled || !held.canBeGrabbed))
                Release(Vector3.zero);

            if (held == null)
            {
                UpdateHover();
                if (hovered != null && VRInput.GrabPressed)
                    Grab(hovered);
            }
            else
            {
                if (VRInput.ThrowPressed)
                {
                    Throw();
                }
                else if (VRInput.ReleasePressed)
                {
                    Release(Vector3.zero);
                }
                else
                {
                    float delta = VRInput.HoldDistanceAxis * holdDistanceSpeed * Time.deltaTime
                                  + VRInput.HoldDistanceSteps * 0.25f;
                    currentHoldDistance = Mathf.Clamp(currentHoldDistance + delta, MinDistanceFor(heldRadius), maxHoldDistance);
                }
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f || head == null) return;

            Vector3 headPos = head.position;
            headVelocity = Vector3.ClampMagnitude((headPos - lastHeadPosition) / dt, 20f);
            lastHeadPosition = headPos;

            if (heldBody == null) return;

            // Posição: puxa o centro de massa da pedra até o ponto na frente da mira
            Vector3 target = headPos + head.forward * currentHoldDistance;
            Vector3 delta = target - heldBody.worldCenterOfMass;
            float distToTarget = delta.magnitude;

            if (isPullingIn)
            {
                if (distToTarget < 0.55f)
                    isPullingIn = false;
            }
            else if (distToTarget > breakDistance)
            {
                stuckTimer += dt;
                if (stuckTimer > 0.35f)
                {
                    Release(Vector3.zero);
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;
            }

            Vector3 velocity = delta * followStrength + headVelocity;
            heldBody.linearVelocity = Vector3.ClampMagnitude(velocity, maxFollowSpeed);

            // Rotação: acompanha suavemente a direção para onde o jogador olha
            if (keepRelativeRotation)
            {
                Quaternion targetRotation = YawRotation() * relativeRotation;
                Quaternion diff = targetRotation * Quaternion.Inverse(heldBody.rotation);
                diff.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;

                if (Mathf.Abs(angle) > 0.1f && axis.sqrMagnitude > 1e-4f && !float.IsNaN(axis.x))
                    heldBody.angularVelocity = axis.normalized * (angle * Mathf.Deg2Rad * rotationStrength);
                else
                    heldBody.angularVelocity = Vector3.zero;
            }
            else
            {
                heldBody.angularVelocity *= 0.9f;
            }
        }

        // ------------------------------------------------------------------ Mira

        void UpdateHover()
        {
            Grabbable found = null;
            var ray = new Ray(head.position, head.forward);
            float maxSearchDist = maxGrabDistance;

            // 1) Raio central direto (preciso)
            if (Physics.Raycast(ray, out RaycastHit directHit, maxGrabDistance, grabLayers, QueryTriggerInteraction.Ignore))
            {
                found = FindValidGrabbable(directHit.collider);
                if (found == null)
                {
                    // Bateu no terreno/parede: limita a busca em cone até um pouco além do ponto de impacto
                    maxSearchDist = Mathf.Min(maxGrabDistance, directHit.distance + 0.35f);
                }
            }

            // 2) Assistência de mira em cone/esfera (encontra a pedra mais próxima do centro da mira)
            if (found == null && aimRadius > 0f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(ray, aimRadius, maxSearchDist, grabLayers, QueryTriggerInteraction.Ignore);
                float bestScore = float.MaxValue;

                for (int i = 0; i < hits.Length; i++)
                {
                    Grabbable candidate = FindValidGrabbable(hits[i].collider);
                    if (candidate == null) continue;

                    Vector3 center = candidate.Body != null ? candidate.Body.worldCenterOfMass : hits[i].collider.bounds.center;
                    Vector3 toCenter = center - ray.origin;
                    float forwardDist = Vector3.Dot(toCenter, ray.direction);
                    if (forwardDist <= 0.1f || forwardDist > maxGrabDistance + 0.3f) continue;

                    float perpDist = Vector3.Cross(ray.direction, toCenter).magnitude;
                    float score = perpDist * 3f + forwardDist * 0.25f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        found = candidate;
                    }
                }
            }

            SetHovered(found);
        }

        Grabbable FindValidGrabbable(Collider c)
        {
            if (c == null) return null;
            Grabbable g = c.attachedRigidbody != null
                ? c.attachedRigidbody.GetComponent<Grabbable>()
                : c.GetComponentInParent<Grabbable>();

            if (g == null || !g.canBeGrabbed || g.IsHeld || g.Body == null || g.Body.mass > maxGrabMass)
                return null;
            return g;
        }

        void SetHovered(Grabbable g)
        {
            if (g == hovered) return;
            if (hovered != null) hovered.SetHighlighted(false);
            hovered = g;
            if (hovered != null) hovered.SetHighlighted(true);
        }

        // ------------------------------------------------------------------ Pegar / soltar

        public void Grab(Grabbable g)
        {
            if (g == null || g.Body == null) return;
            if (held != null) Release(Vector3.zero);

            SetHovered(null);

            held = g;
            heldBody = g.Body;

            savedUseGravity = true; // ao soltar a pedra, ela sempre sofre ação da gravidade lunar
            savedLinearDamping = heldBody.linearDamping;
            savedAngularDamping = heldBody.angularDamping;
            savedInterpolation = heldBody.interpolation;
            savedCollisionMode = heldBody.collisionDetectionMode;

            heldBody.isKinematic = false;
            heldBody.useGravity = false;
            heldBody.linearDamping = 0f;
            heldBody.angularDamping = 1f;
            heldBody.interpolation = RigidbodyInterpolation.Interpolate;
            heldBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            heldBody.ResetCenterOfMass();
            heldBody.WakeUp();

            heldRadius = ComputeRadius(g);
            float preferred = g.holdDistanceOverride > 0f ? g.holdDistanceOverride : holdDistance;
            currentHoldDistance = Mathf.Clamp(preferred, MinDistanceFor(heldRadius), maxHoldDistance);
            relativeRotation = Quaternion.Inverse(YawRotation()) * heldBody.rotation;
            isPullingIn = true;
            stuckTimer = 0f;

            SetIgnorePlayer(g, true);
            g.IsHeld = true;
            Grabbed?.Invoke(g);
        }

        public void Throw()
        {
            if (heldBody == null) return;
            float massFactor = Mathf.Lerp(1f, 0.4f, Mathf.InverseLerp(1f, maxGrabMass, heldBody.mass));
            Release(head.forward * throwSpeed * massFactor);
        }

        public void Release(Vector3 extraVelocity)
        {
            if (held == null) return;

            Grabbable g = held;
            Rigidbody rb = heldBody;
            held = null;
            heldBody = null;
            isPullingIn = false;
            stuckTimer = 0f;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = savedUseGravity;
                rb.linearDamping = savedLinearDamping;
                rb.angularDamping = savedAngularDamping;
                rb.interpolation = savedInterpolation;
                rb.collisionDetectionMode = savedCollisionMode;
                rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxReleaseSpeed) + extraVelocity;
                rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 5f);
            }

            if (g != null)
            {
                g.IsHeld = false;
                if (isActiveAndEnabled) StartCoroutine(RestorePlayerCollisionWhenClear(g));
                else SetIgnorePlayer(g, false);
                Released?.Invoke(g);
            }
        }

        // ------------------------------------------------------------------ Helpers

        float MinDistanceFor(float radius) => Mathf.Max(minHoldDistance, radius + 0.3f);

        Quaternion YawRotation()
        {
            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.04f)
                flat = Vector3.ProjectOnPlane(head.forward.y < 0f ? head.up : -head.up, Vector3.up);
            return Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        static float ComputeRadius(Grabbable g)
        {
            var cols = g.Colliders;
            if (cols == null || cols.Length == 0) return 0.25f;

            bool init = false;
            Bounds b = default;
            foreach (var c in cols)
            {
                if (c == null || !c.enabled) continue;
                if (!init) { b = c.bounds; init = true; }
                else b.Encapsulate(c.bounds);
            }
            return init ? Mathf.Clamp(b.extents.magnitude, 0.08f, 1.5f) : 0.25f;
        }

        void SetIgnorePlayer(Grabbable g, bool ignore)
        {
            if (playerBody == null || g == null || g.Colliders == null) return;
            foreach (var c in g.Colliders)
                if (c != null) Physics.IgnoreCollision(c, playerBody, ignore);
        }

        IEnumerator RestorePlayerCollisionWhenClear(Grabbable g)
        {
            float t = 0f;
            while (g != null && !g.IsHeld && t < 3f && OverlapsPlayer(g))
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (g != null && !g.IsHeld) SetIgnorePlayer(g, false);
        }

        bool OverlapsPlayer(Grabbable g)
        {
            if (playerBody == null || g.Colliders == null) return false;
            Bounds pb = playerBody.bounds;
            pb.Expand(0.1f);
            foreach (var c in g.Colliders)
                if (c != null && c.bounds.Intersects(pb)) return true;
            return false;
        }
    }
}
