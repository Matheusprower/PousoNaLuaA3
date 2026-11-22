using UnityEngine;
using UnityEngine.Events;

namespace PousoLua
{
    /// <summary>
    /// Torna qualquer botão, alavanca ou manivela clicável pela mira VR.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VRInteractable : MonoBehaviour
    {
        [Header("Visual ao mirar")]
        public Color highlightColor = new Color(0.4f, 0.9f, 1f, 1f);
        [Range(0f, 1f)] public float highlightStrength = 0.5f;

        [Header("O que acontece ao apertar?")]
        public UnityEvent onInteract;

        Renderer[] renderers;
        MaterialPropertyBlock block;
        bool isHighlighted;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        public void SetHighlighted(bool on)
        {
            if (isHighlighted == on) return;
            isHighlighted = on;
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

        public void Interact()
        {
            onInteract?.Invoke();
        }

        void OnDisable() => SetHighlighted(false);

        // Compatibilidade com o CardboardReticlePointer
        public void OnPointerEnter() { }
        public void OnPointerExit() { }
        public void OnPointerClick() => Interact();
    }
}