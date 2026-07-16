using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

// UIFabrica: cria elementos de interface por código (botões, textos, painéis)
// e gera sprites simples (cantos arredondados, círculo, gradiente) sem precisar
// de arquivos de imagem no projeto.
public static class UIFabrica
{
    static Sprite spriteArredondado;
    static Sprite spriteCirculo;
    static Sprite spriteCoracao;
    static Sprite spriteAltoFalante;

    // ── Sprites gerados por código ───────────────────────────────────────────

    // Retângulo de cantos arredondados com borda 9-slice
    // (pode esticar em qualquer tamanho sem deformar os cantos)
    public static Sprite Arredondado()
    {
        if (spriteArredondado != null) return spriteArredondado;

        int t = 64; float raio = 20f;
        var tex = new Texture2D(t, t, TextureFormat.ARGB32, false);
        for (int y = 0; y < t; y++)
        for (int x = 0; x < t; x++)
        {
            float dx = Mathf.Max(0f, Mathf.Max(raio - x, x - (t - 1 - raio)));
            float dy = Mathf.Max(0f, Mathf.Max(raio - y, y - (t - 1 - raio)));
            float d  = Mathf.Sqrt(dx * dx + dy * dy);
            float a  = Mathf.Clamp01(raio - d + 0.5f); // borda suavizada
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        spriteArredondado = Sprite.Create(tex, new Rect(0, 0, t, t),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(raio + 2, raio + 2, raio + 2, raio + 2));
        return spriteArredondado;
    }

    // Círculo branco com borda suavizada
    public static Sprite Circulo()
    {
        if (spriteCirculo != null) return spriteCirculo;

        int t = 128; float c = (t - 1) / 2f; float raio = c - 1f;
        var tex = new Texture2D(t, t, TextureFormat.ARGB32, false);
        for (int y = 0; y < t; y++)
        for (int x = 0; x < t; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(raio - d + 0.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        spriteCirculo = Sprite.Create(tex, new Rect(0, 0, t, t), new Vector2(0.5f, 0.5f), 100f);
        return spriteCirculo;
    }

    // Coração (usado para as vidas) - curva implícita clássica do coração
    public static Sprite Coracao()
    {
        if (spriteCoracao != null) return spriteCoracao;

        int t = 64;
        var tex = new Texture2D(t, t, TextureFormat.ARGB32, false);
        for (int py = 0; py < t; py++)
        for (int px = 0; px < t; px++)
        {
            // Converte o pixel para coordenadas -1.4..1.4 (coração cabe nisso)
            float x = (px - t / 2f) / (t * 0.36f);
            float y = (py - t / 2f) / (t * 0.36f) + 0.25f;
            // Equação do coração: (x^2 + y^2 - 1)^3 - x^2·y^3 < 0 -> dentro
            float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
            tex.SetPixel(px, py, new Color(1, 1, 1, f < 0f ? 1f : 0f));
        }
        tex.Apply();
        spriteCoracao = Sprite.Create(tex, new Rect(0, 0, t, t), new Vector2(0.5f, 0.5f), 100f);
        return spriteCoracao;
    }

    // Alto-falante com ondas de som (ícone do botão de música)
    public static Sprite AltoFalante()
    {
        if (spriteAltoFalante != null) return spriteAltoFalante;

        int t = 64;
        var tex = new Texture2D(t, t, TextureFormat.ARGB32, false);
        for (int y = 0; y < t; y++)
        for (int x = 0; x < t; x++)
        {
            bool dentro = false;

            // Caixinha do alto-falante
            if (x >= 8 && x <= 22 && y >= 24 && y <= 40) dentro = true;

            // Cone (triângulo abrindo para a direita)
            if (x > 22 && x <= 36)
            {
                float meiaAltura = 8f + (x - 22f) * 0.9f;
                if (Mathf.Abs(y - 32f) <= meiaAltura) dentro = true;
            }

            // Duas ondas de som (arcos à direita do cone)
            float dist = Mathf.Sqrt((x - 34f) * (x - 34f) + (y - 32f) * (y - 32f));
            if (x >= 42 && (Mathf.Abs(dist - 12f) <= 1.8f || Mathf.Abs(dist - 18f) <= 1.8f))
                dentro = true;

            tex.SetPixel(x, y, new Color(1, 1, 1, dentro ? 1f : 0f));
        }
        tex.Apply();
        spriteAltoFalante = Sprite.Create(tex, new Rect(0, 0, t, t), new Vector2(0.5f, 0.5f), 100f);
        return spriteAltoFalante;
    }

    // Gradiente vertical (usado como fundo do menu)
    public static Sprite Gradiente(Color topo, Color baixo)
    {
        var tex = new Texture2D(1, 256, TextureFormat.ARGB32, false);
        for (int y = 0; y < 256; y++)
            tex.SetPixel(0, y, Color.Lerp(baixo, topo, y / 255f));
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f), 100f);
    }

    // ── Criadores de elementos ───────────────────────────────────────────────

    public static Image CriarImagem(Transform pai, string nome, Color cor,
                                    Vector2 pos, Vector2 tamanho,
                                    Sprite sprite = null, bool fatiado = false)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5; // camada UI
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(pai, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamanho;

        var img = go.GetComponent<Image>();
        img.color = cor;
        if (sprite != null)
        {
            img.sprite = sprite;
            if (fatiado) img.type = Image.Type.Sliced;
        }
        return img;
    }

    public static TextMeshProUGUI CriarTexto(Transform pai, string nome, string texto,
                                             float tamanhoFonte, Color cor,
                                             Vector2 pos, Vector2 tamanho, bool negrito = true)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(pai, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamanho;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = tamanhoFonte;
        tmp.color = cor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = negrito ? FontStyles.Bold : FontStyles.Normal;
        tmp.raycastTarget = false; // texto não bloqueia cliques
        return tmp;
    }

    // Botão completo: fundo arredondado + rótulo + círculo de progresso da mão.
    // Funciona com toque/mouse (Button) E com a mão parada 3s (HoverButton).
    public static Button CriarBotao(Transform pai, string nome, string rotulo, Color corFundo,
                                    Vector2 pos, Vector2 tamanho, float tamanhoFonte,
                                    ControladorCamera controlador, UnityAction acao)
    {
        var img = CriarImagem(pai, nome, corFundo, pos, tamanho, Arredondado(), true);
        var go  = img.gameObject;

        var botao = go.AddComponent<Button>();
        botao.targetGraphic = img;
        if (acao != null) botao.onClick.AddListener(acao);

        CriarTexto(go.transform, "Rotulo", rotulo, tamanhoFonte, Color.white, Vector2.zero, tamanho);

        // Círculo que enche enquanto a mão fica sobre o botão
        float diametro = Mathf.Min(tamanho.x, tamanho.y) * 0.85f;
        var circulo = CriarImagem(go.transform, "CirculoHover",
            new Color(0.2f, 0.8f, 1f, 0.75f), Vector2.zero,
            new Vector2(diametro, diametro), Circulo());
        circulo.type          = Image.Type.Filled;
        circulo.fillMethod    = Image.FillMethod.Radial360;
        circulo.fillOrigin    = (int)Image.Origin360.Top;
        circulo.fillAmount    = 0f;
        circulo.raycastTarget = false;

        var hover = go.AddComponent<HoverButton>();
        hover.controlador   = controlador;
        hover.imagemCirculo = circulo;

        return botao;
    }

    // Prende o elemento em um canto/borda da tela (âncora e pivô juntos)
    public static void Ancorar(Component alvo, Vector2 ancora, Vector2 pivo)
    {
        var rt = alvo.transform as RectTransform;
        rt.anchorMin = ancora;
        rt.anchorMax = ancora;
        rt.pivot     = pivo;
    }
}
