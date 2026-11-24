
using UnityEngine;

#if !UNITY_EDITOR && !UNITY_STANDALONE
using Google.XR.Cardboard;
#endif

public class ControleIluminacaoNave : MonoBehaviour
{
    [Header("Camera do jogador")]
    public Camera cameraJogador;

    [Header("Refletores da nave")]
    public Light[] refletores;

    [Header("Configuracoes")]
    public float distanciaInteracao = 5f;

    private bool iluminacaoLigada = false;

    void Start()
    {
        AtualizarIluminacao();
    }

    void Update()
    {
        bool acionou = false;

        // Teste no computador: botao esquerdo
#if UNITY_EDITOR || UNITY_STANDALONE
        acionou = Input.GetMouseButtonDown(0);

        // Cardboard: gatilho ou toque na tela
#else
        acionou = Api.IsTriggerPressed;
#endif

        if (!acionou)
            return;

        if (cameraJogador == null)
            cameraJogador = Camera.main;

        if (cameraJogador == null)
            return;

        // Verifica o objeto que esta no centro da visao
        Ray raio = new Ray(
            cameraJogador.transform.position,
            cameraJogador.transform.forward
        );

        RaycastHit impacto;

        if (Physics.Raycast(
            raio,
            out impacto,
            distanciaInteracao))
        {
            // Confere se o jogador esta mirando no botao
            if (impacto.transform == transform ||
                impacto.transform.IsChildOf(transform))
            {
                iluminacaoLigada = !iluminacaoLigada;

                AtualizarIluminacao();

                Debug.Log(
                    iluminacaoLigada
                    ? "REFLETORES LIGADOS!"
                    : "REFLETORES DESLIGADOS!"
                );
            }
        }
    }

    void AtualizarIluminacao()
    {
        foreach (Light luz in refletores)
        {
            if (luz != null)
            {
                luz.enabled = iluminacaoLigada;
            }
        }
    }
}
