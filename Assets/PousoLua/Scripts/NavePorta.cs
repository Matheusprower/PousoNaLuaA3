using UnityEngine;

namespace PousoLua
{
    /// <summary>
    /// Abre ou fecha a porta da nave suavemente (girando ou deslizando).
    /// </summary>
    public class NavePorta : MonoBehaviour
    {
        [Header("Tipo de abertura")]
        [Tooltip("Marque se a porta desliza para o lado. Desmarque se ela gira como porta comum.")]
        public bool deslizar = true;

        [Tooltip("Distância que a porta desliza (X, Y, Z) quando deslizar = true.")]
        public Vector3 deslocamentoAberto = new Vector3(1.2f, 0f, 0f);

        [Tooltip("Ângulo que a porta gira (X, Y, Z) quando deslizar = false.")]
        public Vector3 rotacaoAberta = new Vector3(0f, 90f, 0f);

        [Header("Velocidade")]
        public float velocidade = 2f;

        bool aberta = false;
        Vector3 posInicial;
        Quaternion rotInicial;

        void Start()
        {
            posInicial = transform.localPosition;
            rotInicial = transform.localRotation;
        }

        void Update()
        {
            Vector3 posAlvo = aberta ? posInicial + deslocamentoAberto : posInicial;
            Quaternion rotAlvo = aberta ? rotInicial * Quaternion.Euler(rotacaoAberta) : rotInicial;

            if (deslizar)
                transform.localPosition = Vector3.Lerp(transform.localPosition, posAlvo, Time.deltaTime * velocidade);
            else
                transform.localRotation = Quaternion.Slerp(transform.localRotation, rotAlvo, Time.deltaTime * velocidade);
        }

        /// <summary>Alterna entre abrir e fechar a porta (chame isso no botão ou manivela).</summary>
        public void AlternarPorta()
        {
            aberta = !aberta;
            Debug.Log(aberta ? "[Nave] Abrindo porta..." : "[Nave] Fechando porta...");
        }
    }
}