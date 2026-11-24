
using UnityEngine;
using System.Collections;

#if !UNITY_EDITOR && !UNITY_STANDALONE
using Google.XR.Cardboard;
#endif

public class ControleAlertaNave : MonoBehaviour
{
    [Header("Camera do jogador")]
    public Camera cameraJogador;

    [Header("Luz do teto")]
    public Light luzTeto;

    [Header("Som do alarme")]
    public AudioSource somAlerta;

    [Header("Configuracoes")]
    public float distanciaInteracao = 5f;
    public float intervaloPiscar = 0.4f;

    private bool alertaAtivo = false;
    private Coroutine rotinaPiscar;

    private Color corOriginal;
    private float intensidadeOriginal;

    void Start()
    {
        if (luzTeto != null)
        {
            corOriginal = luzTeto.color;
            intensidadeOriginal = luzTeto.intensity;
        }
    }

    void Update()
    {
        bool acionou = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        acionou = Input.GetMouseButtonDown(0);
#else
        acionou = Api.IsTriggerPressed;
#endif

        if (!acionou)
            return;

        if (cameraJogador == null)
            cameraJogador = Camera.main;

        if (cameraJogador == null)
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
                AlternarAlerta();
            }
        }
    }

    public void AlternarAlerta()
    {
        alertaAtivo = !alertaAtivo;

        if (alertaAtivo)
        {
            if (rotinaPiscar != null)
                StopCoroutine(rotinaPiscar);

            rotinaPiscar = StartCoroutine(PiscarLuz());

            if (somAlerta != null && somAlerta.clip != null)
                somAlerta.Play();

            Debug.Log("ALERTA ATIVADO!");
        }
        else
        {
            if (rotinaPiscar != null)
            {
                StopCoroutine(rotinaPiscar);
                rotinaPiscar = null;
            }

            if (luzTeto != null)
            {
                luzTeto.enabled = true;
                luzTeto.color = corOriginal;
                luzTeto.intensity = intensidadeOriginal;
            }

            if (somAlerta != null)
                somAlerta.Stop();

            Debug.Log("ALERTA DESATIVADO!");
        }
    }

    IEnumerator PiscarLuz()
    {
        while (alertaAtivo)
        {
            if (luzTeto != null)
            {
                luzTeto.enabled = true;
                luzTeto.color = Color.red;
                luzTeto.intensity = intensidadeOriginal * 1.5f;
            }

            yield return new WaitForSeconds(intervaloPiscar);

            if (luzTeto != null)
                luzTeto.enabled = false;

            yield return new WaitForSeconds(intervaloPiscar);
        }
    }
}
