using UnityEngine;

// Flutuar: balanço suave, como se o objeto boiasse na água.
// Ele oscila para cima/baixo e para os lados, mas sempre em volta
// do ponto de origem - nunca "vai embora".
// O GerenciadorDeJogo adiciona este componente em toda fruta criada.
public class Flutuar : MonoBehaviour
{
    [Header("Tamanho do balanco (em unidades do mundo)")]
    public float amplitude = 0.25f;

    [Header("Velocidade do balanco")]
    public float velocidade = 1.1f;

    [Header("Gingado de rotacao (graus)")]
    public float anguloBalanco = 4f;

    private Vector3    origem;      // ponto fixo em volta do qual flutua
    private Quaternion rotacaoBase; // rotação original da fruta
    private float      fase;        // cada fruta balança num ritmo próprio

    void Start()
    {
        origem      = transform.position;
        rotacaoBase = transform.rotation;
        fase        = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        float t = Time.time * velocidade + fase;

        // Senos com frequências diferentes: o caminho vira um "oito" orgânico
        Vector3 deslocamento = new Vector3(
            Mathf.Sin(t * 0.63f) * amplitude,
            Mathf.Sin(t)         * amplitude,
            0f);
        transform.position = origem + deslocamento;

        // Leve gingado de rotação, em volta da rotação original
        Quaternion gingado = Quaternion.Euler(
            Mathf.Sin(t * 0.9f) * anguloBalanco,
            Mathf.Sin(t * 0.7f) * anguloBalanco,
            0f);
        transform.rotation = rotacaoBase * gingado;
    }
}
