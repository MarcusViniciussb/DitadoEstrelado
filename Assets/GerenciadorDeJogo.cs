using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GerenciadorDeJogo : MonoBehaviour
{
    [System.Serializable]
    public class PalavraItem
    {
        public string palavra;
        public GameObject prefab;
    }

    [Header("Lista de frutas")]
    public List<PalavraItem> itens = new List<PalavraItem>();

    [Header("Ponto de exibicao da fruta")]
    public Transform pontoDeExibicao;

    [Header("Tamanho e rotacao das frutas")]
    public Vector3 escalaFruta  = new Vector3(300f, 300f, 300f);
    public Vector3 rotacaoFruta = new Vector3(-50f, 160f, 0f);

    [Header("Pontuacao")]
    public int pontosPorLetra  = 10;  // acertar uma letra
    public int bonusPorPalavra = 50;  // completar a palavra inteira

    [Header("Tempo de celebracao (s)")]
    public float tempoCelebracao = 2.5f;

    // ── Eventos ─────────────────────────────────────────────────────────────
    public System.Action<string> OnPalavraCompleta;
    public System.Action<int>    OnPontuacaoAtualizada;

    // ── Estado interno ───────────────────────────────────────────────────────
    private List<PalavraItem> listaEmbaralhada;
    private int indiceFruta          = 0;
    private int indiceLetra          = 0;
    private int pontuacao            = 0;
    private bool aguardandoCelebracao = false;
    private GameObject frutaAtual;

    // ── Propriedades públicas ────────────────────────────────────────────────
    public string PalavraAtual
    {
        get
        {
            if (listaEmbaralhada == null || indiceFruta >= listaEmbaralhada.Count) return "";
            return listaEmbaralhada[indiceFruta].palavra;
        }
    }
    public int  IndiceLetraAtual => indiceLetra;
    public int  Pontuacao        => pontuacao;

    // Verdadeiro entre IniciarJogo() e PararJogo() — o menu controla isso
    public bool JogoIniciado { get; private set; }

    public bool JogoTerminado    =>
        JogoIniciado &&
        !aguardandoCelebracao &&
        (listaEmbaralhada == null || indiceFruta >= listaEmbaralhada.Count);

    // ── Controle do jogo (chamado pelo MenuPrincipal) ────────────────────────

    // Começa (ou recomeça) o jogo do zero: embaralha, zera pontos, mostra a 1ª fruta
    public void IniciarJogo()
    {
        if (itens == null || itens.Count == 0)
        {
            Debug.LogError("GerenciadorDeJogo: lista de itens vazia!");
            return;
        }
        listaEmbaralhada = new List<PalavraItem>(itens);
        Embaralhar(listaEmbaralhada);

        indiceFruta = 0;
        indiceLetra = 0;
        pontuacao   = 0;
        OnPontuacaoAtualizada?.Invoke(pontuacao);

        aguardandoCelebracao = false;
        JogoIniciado = true;
        ExibirFrutaAtual();
    }

    // Interrompe o jogo e limpa a fruta da tela (usado ao voltar para o menu)
    public void PararJogo()
    {
        StopAllCoroutines();
        aguardandoCelebracao = false;
        JogoIniciado = false;
        if (frutaAtual != null) Destroy(frutaAtual);
    }

    void Embaralhar(List<PalavraItem> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = lista[i]; lista[i] = lista[j]; lista[j] = tmp;
        }
    }

    void ExibirFrutaAtual()
    {
        if (frutaAtual != null) Destroy(frutaAtual);

        if (indiceFruta >= listaEmbaralhada.Count)
        {
            GerenciadorDeAudio.TocarVitoria();
            Debug.Log("Jogo concluido! Pontuacao final: " + pontuacao);
            return;
        }

        var item = listaEmbaralhada[indiceFruta];
        if (item.prefab == null)
        {
            Debug.LogError("Prefab nao atribuido para: " + item.palavra);
            return;
        }

        Vector3    pos = pontoDeExibicao != null ? pontoDeExibicao.position : Vector3.zero;
        frutaAtual = Instantiate(item.prefab, pos, Quaternion.Euler(rotacaoFruta));
        frutaAtual.transform.localScale = escalaFruta;
        frutaAtual.AddComponent<Flutuar>(); // balanço suave, como se boiasse
        indiceLetra = 0;
        Debug.Log("Palavra: [" + item.palavra + "]");
    }

    // ── Tentativa de letra (chamado pelo ControladorCamera) ──────────────────
    public bool TentarLetra(string letraFeita)
    {
        if (!JogoIniciado || JogoTerminado || aguardandoCelebracao) return false;
        if (string.IsNullOrEmpty(PalavraAtual)) return false;

        if (letraFeita == PalavraAtual[indiceLetra].ToString())
        {
            AdicionarPontos(pontosPorLetra);
            indiceLetra++;
            Debug.Log("ACERTOU [" + letraFeita + "] +" + pontosPorLetra + " pts");

            if (indiceLetra >= PalavraAtual.Length)
            {
                AdicionarPontos(bonusPorPalavra); // bônus por completar a palavra
                aguardandoCelebracao = true;
                GerenciadorDeAudio.TocarVitoria();
                OnPalavraCompleta?.Invoke(PalavraAtual);
                StartCoroutine(AvancarAposCelebracao());
            }
            else
            {
                GerenciadorDeAudio.TocarAcerto();
            }
            return true;
        }
        return false;
    }

    // ── Ações do jogador ─────────────────────────────────────────────────────

    // Pula a palavra inteira (sem pontos)
    public void PularPalavra()
    {
        if (!JogoIniciado || aguardandoCelebracao) return;
        StopAllCoroutines();
        aguardandoCelebracao = false;
        GerenciadorDeAudio.TocarClique();
        indiceFruta++;
        ExibirFrutaAtual();
        Debug.Log("Palavra pulada.");
    }

    // Avança para a próxima letra (sem pontos)
    public void PularLetra()
    {
        if (!JogoIniciado || aguardandoCelebracao || string.IsNullOrEmpty(PalavraAtual)) return;
        if (indiceLetra < PalavraAtual.Length - 1)
        {
            GerenciadorDeAudio.TocarClique();
            indiceLetra++;
            Debug.Log("Letra pulada. Indice: " + indiceLetra);
        }
        else
        {
            // Estava na última letra — pula a palavra
            PularPalavra();
        }
    }

    // Volta uma letra (sem alterar pontos)
    public void VoltarLetra()
    {
        if (!JogoIniciado || aguardandoCelebracao) return;
        if (indiceLetra > 0)
        {
            GerenciadorDeAudio.TocarClique();
            indiceLetra--;
            Debug.Log("Voltou uma letra. Indice: " + indiceLetra);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    void AdicionarPontos(int valor)
    {
        pontuacao += valor;
        OnPontuacaoAtualizada?.Invoke(pontuacao);
    }

    IEnumerator AvancarAposCelebracao()
    {
        yield return new WaitForSeconds(tempoCelebracao);
        aguardandoCelebracao = false;
        indiceFruta++;
        ExibirFrutaAtual();
    }
}
