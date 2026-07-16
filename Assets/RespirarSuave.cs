using UnityEngine;

// RespirarSuave: dá "vida" a modelos SEM animação - o corpo infla e
// desinfla de leve, como uma respiração calma. Usado como plano B
// quando o modelo não tem clipes de animação (ex: Farm Animals).
public class RespirarSuave : MonoBehaviour
{
    [Range(0.01f, 0.1f)] public float intensidade = 0.035f; // 3,5% de variação
    [Range(0.3f, 4f)]    public float velocidade  = 1.6f;

    private Vector3 escalaBase;
    private float   fase;

    void Start()
    {
        escalaBase = transform.localScale; // captura DEPOIS do auto-ajuste
        fase       = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        float s = Mathf.Sin(Time.time * velocidade + fase) * intensidade;

        // Sobe no Y e encolhe de leve nos lados (conserva o "volume")
        transform.localScale = new Vector3(
            escalaBase.x * (1f - s * 0.5f),
            escalaBase.y * (1f + s),
            escalaBase.z * (1f - s * 0.5f));
    }
}
