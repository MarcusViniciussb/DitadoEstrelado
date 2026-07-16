using UnityEngine;
using UnityEngine.UI;

// Cintilar: faz uma imagem de UI pulsar como uma estrela no céu -
// o brilho (alfa) e o tamanho sobem e descem suavemente, cada estrela
// no seu próprio ritmo (velocidade e fase sorteadas).
public class Cintilar : MonoBehaviour
{
    [Range(0.2f, 6f)] public float velocidade  = 2f;
    [Range(0f, 1f)]   public float alfaMinimo  = 0.15f;
    [Range(0f, 1f)]   public float alfaMaximo  = 1f;
    [Range(0f, 1f)]   public float escalaExtra = 0.35f; // quanto "incha" no pico

    private Image   imagem;
    private float   fase;
    private Vector3 escalaBase;

    void Awake()
    {
        imagem     = GetComponent<Image>();
        escalaBase = transform.localScale;
        fase       = Random.Range(0f, Mathf.PI * 2f);
        velocidade *= Random.Range(0.5f, 1.6f); // cada estrela num ritmo próprio
    }

    void Update()
    {
        if (imagem == null) return;

        float s = (Mathf.Sin(Time.unscaledTime * velocidade + fase) + 1f) * 0.5f;

        var cor = imagem.color;
        cor.a = Mathf.Lerp(alfaMinimo, alfaMaximo, s);
        imagem.color = cor;

        transform.localScale = escalaBase * (1f + escalaExtra * s);
    }
}
