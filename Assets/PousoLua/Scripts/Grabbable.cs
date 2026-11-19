using UnityEngine;

namespace PousoLua
{
    /// <summary>
    /// Marca um objeto com Rigidbody como "pegável" pelo VRObjectGrabber.
    /// Também destaca o objeto (tinge a cor) quando o jogador está mirando nele.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class Grabbable : MonoBehaviour
    {
        public bool canBeGrabbed = true;

        [Tooltip("Distância para segurar este objeto (0 = usar a padrão do VRObjectGrabber).")]
        public float holdDistanceOverride = 0f;

        [Header("Destaque ao mirar")]
        public Color highlightColor = new Color(1f, 0.85f, 0.45f, 1f);
        [Range(0f, 1f)] public float highlightStrength = 0.4f;

        public Rigidbody Body { get; private set; }
        public Collider[] Colliders { get; private set; }
        public bool IsHeld { get; internal set; }
        public bool IsHighlighted { get; private set; }

        Renderer[] renderers;
        MaterialPropertyBlock block;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
        static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in / legado

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            RefreshCache();
        }

        /// <summary>Atualiza a lista de colliders/renderers (chame se adicionar filhos em runtime).</summary>
        public void RefreshCache()
        {
            Colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
        }

        public void SetHighlighted(bool on)
        {
            if (IsHighlighted == on) return;
            IsHighlighted = on;
            if (renderers == null) return;

            if (block == null) block = new MaterialPropertyBlock();

            foreach (var r in renderers)
            {
                if (r == null) continue;

                if (!on)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }

                var mat = r.sharedMaterial;
                if (mat == null) continue;

                int id = mat.HasProperty(BaseColorId) ? BaseColorId : (mat.HasProperty(ColorId) ? ColorId : -1);
                if (id == -1) continue;

                Color c = Color.Lerp(mat.GetColor(id), highlightColor, highlightStrength);
                r.GetPropertyBlock(block);
                block.SetColor(id, c);
                r.SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            SetHighlighted(false);
        }

        // Compatibilidade com o CardboardReticlePointer (ele usa SendMessage em objetos da layer interativa).
        public void OnPointerEnter() { }
        public void OnPointerExit() { }
        public void OnPointerClick() { }
    }
}
