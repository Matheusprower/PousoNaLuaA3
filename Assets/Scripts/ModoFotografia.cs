
using UnityEngine;
using System.IO;
using System.Collections;

public class ModoFotografia : MonoBehaviour
{
    private bool modoFotoAtivo = false;
    private bool mostrarConfirmacao = false;

    private float tempoMensagem = 0f;

    void Update()
    {
        // F no teclado ou B na manete
        bool apertouBotao =
            Input.GetKeyDown(KeyCode.F) ||
            Input.GetKeyDown(KeyCode.JoystickButton1);

        if (apertouBotao)
        {
            if (!modoFotoAtivo)
            {
                // Primeiro aperto: ativa o modo foto
                modoFotoAtivo = true;
                mostrarConfirmacao = false;

                Debug.Log("Modo fotografia ativado!");
            }
            else
            {
                // Segundo aperto: tira a foto
                StartCoroutine(TirarFoto());
            }
        }

        if (mostrarConfirmacao)
        {
            tempoMensagem -= Time.deltaTime;

            if (tempoMensagem <= 0)
            {
                mostrarConfirmacao = false;
            }
        }
    }

    IEnumerator TirarFoto()
    {
        modoFotoAtivo = false;

        yield return new WaitForEndOfFrame();

        Texture2D foto = ScreenCapture.CaptureScreenshotAsTexture();

        byte[] imagem = foto.EncodeToPNG();

        string nome = "FotoLunar_" +
            System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

        Destroy(foto);

        #if UNITY_ANDROID && !UNITY_EDITOR

        // Salva diretamente na Galeria do Android
        NativeGallery.SaveImageToGallery(
            imagem,
            "Fotos Lunares",
            nome,
            (sucesso, caminho) =>
            {
                Debug.Log("Salvamento na galeria: " + sucesso);
                Debug.Log("Local da foto: " + caminho);
            }
        );

        #else

        // Salva na Área de Trabalho do Windows
        string desktop = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.DesktopDirectory
        );

        string caminho = Path.Combine(desktop, nome);

        File.WriteAllBytes(caminho, imagem);

        Debug.Log("Foto salva na Área de Trabalho: " + caminho);

        #endif

        mostrarConfirmacao = true;
        tempoMensagem = 3f;

        Debug.Log("Foto registrada!");
    }

    void OnGUI()
    {
        if (modoFotoAtivo)
        {
            GUIStyle estilo = new GUIStyle(GUI.skin.label);

            estilo.fontSize = 28;
            estilo.alignment = TextAnchor.MiddleCenter;
            estilo.normal.textColor = Color.white;

            GUI.Label(
                new Rect(0, Screen.height * 0.15f, Screen.width, 50),
                "MODO FOTOGRAFIA - APERTE F OU B PARA FOTOGRAFAR",
                estilo
            );

            // Mira no centro da tela
            GUI.Label(
                new Rect(
                    Screen.width / 2 - 15,
                    Screen.height / 2 - 25,
                    30,
                    50
                ),
                "+",
                estilo
            );
        }

        if (mostrarConfirmacao)
        {
            GUIStyle estilo = new GUIStyle(GUI.skin.label);

            estilo.fontSize = 35;
            estilo.fontStyle = FontStyle.Bold;
            estilo.alignment = TextAnchor.MiddleCenter;
            estilo.normal.textColor = Color.green;

            GUI.Label(
                new Rect(0, Screen.height - 120, Screen.width, 60),
                "FOTO REGISTRADA!",
                estilo
            );
        }
    }
}
