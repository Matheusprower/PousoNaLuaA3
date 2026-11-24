
using UnityEngine;

public class QuebrarGarrafa : MonoBehaviour
{
    [Header("Prefab dos fragmentos")]
    public GameObject garrafaQuebrada;

    [Header("Configuracao da quebra")]
    public float forcaQuebra = 3f;
    public float raioExplosao = 1.5f;

    private bool jaQuebrou = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (jaQuebrou)
            return;

        // Verifica se a colisao foi causada pela pedra
        bool acertouPedra =
            collision.collider.CompareTag("Pedra") ||
            collision.transform.root.CompareTag("Pedra") ||
            (collision.rigidbody != null &&
             collision.rigidbody.CompareTag("Pedra"));

        if (!acertouPedra)
            return;

        if (garrafaQuebrada == null)
        {
            Debug.LogError("Prefab da garrafa quebrada nao foi configurado!");
            return;
        }

        jaQuebrou = true;

        // Local do impacto
        Vector3 pontoImpacto = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        // Cria os fragmentos no lugar da garrafa
        GameObject fragmentos = Instantiate(
            garrafaQuebrada,
            transform.position,
            transform.rotation
        );

        // Aplica forca individual em cada pedaco
        Rigidbody[] pecas =
            fragmentos.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody peca in pecas)
        {
            peca.isKinematic = false;
            peca.useGravity = true;

            peca.AddExplosionForce(
                forcaQuebra,
                pontoImpacto,
                raioExplosao,
                0.2f,
                ForceMode.Impulse
            );

            // Remove o fragmento depois de 8 segundos
            Destroy(peca.gameObject, 8f);
        }

        // Remove a garrafa original
        Destroy(gameObject);
    }
}
