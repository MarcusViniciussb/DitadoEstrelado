using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// ChuvaDeConfetes: cria dezenas de papeizinhos coloridos que caem girando
// pela tela (comemoração de vitória). Se destrói sozinho ao terminar.
//
// Uso: ChuvaDeConfetes.Lancar(canvas.transform);
public class ChuvaDeConfetes : MonoBehaviour
{
    class Confete
    {
        public RectTransform rt;
        public float velocidadeQueda;
        public float velocidadeGiro;
        public float balanco;     // deriva horizontal
        public float fase;
    }

    private readonly List<Confete> confetes = new List<Confete>();
    private float alturaTela;
    private float vida = 6f; // segundos até se limpar

    private static readonly Color[] CORES =
    {
        new Color(1f, 0.85f, 0.25f), // amarelo
        new Color(0.30f, 0.75f, 1f), // azul
        new Color(0.35f, 0.85f, 0.4f), // verde
        new Color(1f, 0.45f, 0.55f), // rosa
        new Color(0.75f, 0.55f, 1f), // lilás
        Color.white,
    };

    public static void Lancar(Transform canvas)
    {
        var go = new GameObject("ChuvaDeConfetes", typeof(RectTransform));
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvas, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.SetAsLastSibling();
        go.AddComponent<ChuvaDeConfetes>();
    }

    void Start()
    {
        var meuRect = (RectTransform)transform;
        float largura = meuRect.rect.width;
        alturaTela    = meuRect.rect.height;

        for (int i = 0; i < 60; i++)
        {
            var img = UIFabrica.CriarImagem(transform, "Confete",
                CORES[Random.Range(0, CORES.Length)],
                new Vector2(Random.Range(-largura / 2f, largura / 2f),
                            alturaTela / 2f + Random.Range(20f, 500f)),
                new Vector2(Random.Range(14f, 26f), Random.Range(8f, 14f)));
            img.raycastTarget = false;

            confetes.Add(new Confete
            {
                rt              = img.rectTransform,
                velocidadeQueda = Random.Range(250f, 550f),
                velocidadeGiro  = Random.Range(-360f, 360f),
                balanco         = Random.Range(30f, 90f),
                fase            = Random.Range(0f, Mathf.PI * 2f),
            });
        }
    }

    void Update()
    {
        foreach (var c in confetes)
        {
            Vector2 pos = c.rt.anchoredPosition;
            pos.y -= c.velocidadeQueda * Time.deltaTime;
            pos.x += Mathf.Sin(Time.time * 2.5f + c.fase) * c.balanco * Time.deltaTime;
            c.rt.anchoredPosition = pos;
            c.rt.Rotate(0, 0, c.velocidadeGiro * Time.deltaTime);
        }

        vida -= Time.deltaTime;
        if (vida <= 0f) Destroy(gameObject);
    }
}
