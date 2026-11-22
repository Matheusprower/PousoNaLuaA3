using UnityEngine;

public class InterruptorLuz : MonoBehaviour
{
    [Header("Botões que não atrapalham as pedras")]
    public KeyCode teclaPC = KeyCode.L;
    public KeyCode botaoControleLivre = KeyCode.JoystickButton3; // Botão Y (ou mude para JoystickButton5 = R1)

    [Header("Segurança")]
    public bool apenasDentroDaNave = false; // Se marcar, só funciona quando o jogador entrar na nave
    private bool jogadorEstaDentro = false;

    private Light luz;

    void Start()
    {
        luz = GetComponent<Light>();
    }

    void Update()
    {
        // Se a opção "apenasDentroDaNave" estiver marcada e o jogador estiver fora, não faz nada
        if (apenasDentroDaNave && !jogadorEstaDentro)
            return;

        if (Input.GetKeyDown(teclaPC) || Input.GetKeyDown(botaoControleLivre))
        {
            AlternarLuz();
        }
    }

    public void AlternarLuz()
    {
        if (luz != null)
        {
            luz.enabled = !luz.enabled;
        }
    }

    // Detecta quando o jogador entra na área da nave
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            jogadorEstaDentro = true;
    }

    // Detecta quando o jogador sai da nave
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            jogadorEstaDentro = false;
    }
}