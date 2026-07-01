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
    public bool JogoTerminado    =>
        !aguardandoCelebracao &&
        (listaEmbaralhada == null || indiceFruta >= listaEmbaralhada.Count);

    // ── Inicialização ────────────────────────────────────────────────────────
    void Start()
    {
        if (itens == null || itens.Count == 0)
        {
            Debug.LogError("GerenciadorDeJogo: lista de itens vazia!");
            return;
        }
        listaEmbaralhada = new List<PalavraItem>(itens);
        Embaralhar(listaEmbaralhada);
        ExibirFrutaAtual();
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
        indiceLetra = 0;
        Debug.Log("Palavra: [" + item.palavra + "]");
    }

    // ── Tentativa de letra (chamado pelo ControladorCamera) ──────────────────
    public bool TentarLetra(string letraFeita)
    {
        if (JogoTerminado || aguardandoCelebracao) return false;
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
                OnPalavraCompleta?.Invoke(PalavraAtual);
                StartCoroutine(AvancarAposCelebracao());
            }
            return true;
        }
        return false;
    }

    // ── Ações do jogador ─────────────────────────────────────────────────────

    // Pula a palavra inteira (sem pontos)
    public void PularPalavra()
    {
        if (aguardandoCelebracao) return;
        StopAllCoroutines();
        aguardandoCelebracao = false;
        indiceFruta++;
        ExibirFrutaAtual();
        Debug.Log("Palavra pulada.");
    }

    // Avança para a próxima letra (sem pontos)
    public void PularLetra()
    {
        if (aguardandoCelebracao || string.IsNullOrEmpty(PalavraAtual)) return;
        if (indiceLetra < PalavraAtual.Length - 1)
        {
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
        if (aguardandoCelebracao) return;
        if (indiceLetra > 0)
        {
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
