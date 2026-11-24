
using UnityEngine;

#if !UNITY_EDITOR && !UNITY_STANDALONE
using Google.XR.Cardboard;
#endif

public class ControleMapaLunar : MonoBehaviour
{
    [Header("Camera do jogador")]
    public Camera cameraJogador;

    [Header("Monitor do mapa")]
    public GameObject monitorMapa;

    public float distanciaInteracao = 5f;

    private bool triggerAnterior = false;

    void Update()
    {
        bool acionou = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        acionou = Input.GetMouseButtonDown(0);
#else
        bool triggerAtual = Api.IsTriggerPressed;

        acionou = triggerAtual && !triggerAnterior;

        triggerAnterior = triggerAtual;
#endif

        if (!acionou)
            return;

        if (cameraJogador == null)
            cameraJogador = Camera.main;

        if (cameraJogador == null || monitorMapa == null)
            return;

        Ray raio = new Ray(
            cameraJogador.transform.position,
            cameraJogador.transform.forward
        );

        RaycastHit impacto;

        if (Physics.Raycast(raio, out impacto, distanciaInteracao))
        {
            if (impacto.transform == transform ||
                impacto.transform.IsChildOf(transform))
            {
                monitorMapa.SetActive(!monitorMapa.activeSelf);

                Debug.Log("Mapa lunar alternado!");
            }
        }
    }
}
