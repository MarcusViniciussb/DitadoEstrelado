using UnityEngine;
using UnityEngine.UI;
using MediaPipe.HandLandmark;
using System.Collections;
using System.Collections.Generic;
// Apelidos para não confundir System.Diagnostics.Debug com UnityEngine.Debug
using Processo       = System.Diagnostics.Process;
using InfoDeProcesso = System.Diagnostics.ProcessStartInfo;

// ControladorCamera: conecta a webcam ao detector de mão (HandLandmarkDetector)
// e passa os 21 pontos da mão para o ReconhecedorLibras.
// Este script fica num GameObject que também tem a RawImage da câmera.
public class ControladorCamera : MonoBehaviour
{
    [Header("Referências de Cena")]
    public RawImage fundoDoEcra;         // RawImage que exibe o feed da câmera
    public ResourceSet ficheirosDaIA;    // ScriptableObject do plugin HandLandmark
    public Transform bolinhaDoDedo;      // Objeto 3D que segue a ponta do indicador
    public ReconhecedorLibras reconhecedor;

    [Header("Calibração Visual (posição da bolinha)")]
    public float escalaX = -15f;
    public float escalaY = 10f;
    public float translaX = 0f;
    public float translaY = 0f;

    [Header("Controle do Jogo")]
    public bool MODO_TREINAMENTO = true;
    public GerenciadorDeJogo gerenciador; // Arraste o GameObject que tem GerenciadorDeJogo aqui

    [Header("Rastreador externo (Python/MediaPipe) — opcional")]
    // Se o script RastreadorPython estiver rodando, usa o MediaPipe completo
    // (muito melhor). Senão, cai automaticamente no rastreador interno.
    public RastreadorExterno rastreadorExterno;
    public bool  iniciarRastreadorAutomaticamente = true; // Unity abre o Python sozinho!
    // Tempo máximo aguardando o Python carregar (a 1ª vez pode ser lenta:
    // antivírus + carga do MediaPipe). Enquanto o processo estiver vivo, espera.
    // (campo renomeado de "esperaRastreadorExterno" para o Unity esquecer o
    //  valor antigo de 3s que ficou memorizado na cena)
    public float esperaMaximaRastreador = 45f;
    private bool usandoExterno = false;
    private Processo processoRastreador; // guardamos para fechar junto com o jogo

    [Header("Reconhecimento")]
    public float cooldownReconhecimento = 0.9f; // segundos mínimos entre duas letras reconhecidas
    public float tempoEstabilidade = 0.35f;     // segundos segurando o MESMO sinal para valer
    private float tempoUltimoReconhecimento = -99f;
    private string letraCandidata = "";
    private float tempoLetraCandidata = 0f;

    // Lidos pela interface para mostrar "estou quase reconhecendo a letra X"
    public string LetraCandidata      { get; private set; } = "";
    public float  ProgressoCandidata  { get; private set; } = 0f;

    [Header("Letras dinamicas (com movimento)")]
    public float duracaoGravacaoMovimento   = 1.3f;  // segundos gravados no treinamento
    public float duracaoJanelaMovimento     = 1.4f;  // "memória" de movimento no jogo
    public float intervaloChecagemMovimento = 0.25f; // de quanto em quanto compara

    // Janela deslizante: os últimos ~1,4s de mão (usada pelo DTW)
    private List<Vector3[]> janelaMovimento = new List<Vector3[]>();
    private List<float>     temposJanela    = new List<float>();
    private float tempoUltimaAmostraJanela     = 0f;
    private float tempoUltimaChecagemMovimento = 0f;

    [Header("Exibicao da camera")]
    public bool espelharImagem  = true;  // modo "selfie" (RawImage com escala X = -1)
    public bool inverterVertical = false; // ligue se o esqueleto aparecer de cabeça para baixo

    // Margem de segurança: a tela mostra só o miolo do sensor. Assim a mão no
    // CANTO DA TELA ainda está DENTRO do campo de visão da câmera, com folga
    // para o rastreador. 1.2 = tela mostra ~83% do quadro da câmera.
    [Range(1f, 1.5f)] public float zoomDaTela = 1.2f;
    private float zoomAnterior = 0f;

    [Header("Zoom inteligente (2 estagios, como o MediaPipe original)")]
    // Em vez de mandar o frame INTEIRO espremido para a IA, recorta um quadrado
    // ao redor da última posição da mão — a mão chega grande e nítida ao modelo.
    // Se algo ficar estranho, desligue aqui para voltar ao comportamento antigo.
    public bool zoomInteligente = true;
    [Range(1.5f, 4f)] public float margemDoZoom = 2.4f; // recorte = tamanho da mão × margem

    private RenderTexture texturaRecorte;                    // quadrado enviado à IA
    private Vector2 centroCaixa = new Vector2(0.5f, 0.5f);   // centro do recorte (normalizado)
    private float   ladoCaixaPx = 0f;                        // lado do recorte em px (0 = busca ampla)
    private Vector2 escalaBlit  = Vector2.one;               // último recorte aplicado
    private Vector2 offsetBlit  = Vector2.zero;              //   (para mapear pontos de volta)
    private int     framesSemMao = 0;

    private WebCamTexture minhaCamera;
    private HandLandmarkDetector detetive;
    private bool cameraPronta = false;
    private bool detetivePronto = false;

    // Recorte central aplicado ao feed para NÃO esticar a imagem na tela
    private Rect uvRecorte = new Rect(0, 0, 1, 1);
    private int larguraTelaAnterior = 0;
    private int alturaTelaAnterior = 0;

    // Pontos da mão atuais — lidos pelo VisualizadorMaoUI para desenhar o esqueleto
    public Vector3[] PontosDaMaoAtuais { get; private set; }
    public bool MaoDetectada { get; private set; }

    // Converte um keypoint (0..1 no frame INTEIRO da câmera) para posição na
    // tela em pixels, já descontando o recorte e o espelho.
    // O detector usa a convenção do Unity: Y cresce para CIMA.
    // Todo script que precisa saber onde a mão está na tela deve usar isto.
    public Vector2 PontoParaTela(Vector3 kp)
    {
        float u = (kp.x - uvRecorte.x) / uvRecorte.width;
        float v = (kp.y - uvRecorte.y) / uvRecorte.height;
        if (espelharImagem)   u = 1f - u;
        if (inverterVertical) v = 1f - v;
        return new Vector2(u * Screen.width, v * Screen.height);
    }

    void Start()
    {
        if (rastreadorExterno != null && rastreadorExterno.enabled)
            StartCoroutine(EscolherFonteDeRastreamento());
        else
            IniciarCameraInterna();
    }

    // Fluxo de escolha da fonte de rastreamento:
    // 1) Já tem um rastreador Python rodando (aberto na mão)? Usa ele.
    // 2) Senão, o próprio Unity abre o Python invisível em segundo plano.
    // 3) Se nada funcionar, liga o rastreador interno antigo (fallback).
    IEnumerator EscolherFonteDeRastreamento()
    {
        Debug.Log("Procurando rastreador externo (Python/MediaPipe)...");

        // Alguém já abriu o rastreador manualmente?
        float fimVerificacao = Time.time + 0.7f;
        while (Time.time < fimVerificacao)
        {
            if (rastreadorExterno.Ativo)
            {
                usandoExterno = true;
                Debug.Log("Rastreador EXTERNO ja estava rodando — conectado!");
                yield break;
            }
            yield return null;
        }

        // Abre o Python sozinho (invisível); ele é fechado junto com o jogo
        if (iniciarRastreadorAutomaticamente)
            IniciarProcessoDoRastreador();

        // Nenhum processo foi aberto (Python ausente)? Não perde tempo esperando
        if (processoRastreador == null)
        {
            Debug.Log("Usando o rastreador interno.");
            IniciarCameraInterna();
            yield break;
        }

        float inicioEspera   = Time.time;
        float proximoRelato  = Time.time + 4f;
        bool  morreu         = false;
        Debug.Log("Aguardando o rastreador por ate " + esperaMaximaRastreador + "s...");

        while (Time.time - inicioEspera < esperaMaximaRastreador)
        {
            if (rastreadorExterno.Ativo)
            {
                usandoExterno = true;
                Debug.Log("Rastreador EXTERNO conectado! (MediaPipe completo, com profundidade Z)");
                yield break;
            }

            // O Python abriu e morreu? Mostra o erro DELE no Console e desiste já
            try { morreu = processoRastreador.HasExited; } catch { morreu = true; }
            if (morreu)
            {
                string logsErro;
                lock (logsDoRastreador) logsErro = logsDoRastreador.ToString();
                Debug.LogWarning("O rastreador Python fechou sozinho. Mensagens dele:\n" + logsErro);
                break;
            }

            // Mostra o progresso do carregamento (1ª vez pode demorar bastante)
            if (Time.time >= proximoRelato)
            {
                proximoRelato = Time.time + 4f;
                string ultimaLinha = "";
                lock (logsDoRastreador)
                {
                    string tudo = logsDoRastreador.ToString().TrimEnd();
                    int quebra = tudo.LastIndexOf('\n');
                    ultimaLinha = (quebra >= 0) ? tudo.Substring(quebra + 1) : tudo;
                }
                Debug.Log("Aguardando o MediaPipe carregar... (" +
                          Mathf.RoundToInt(Time.time - inicioEspera) + "s) " +
                          (ultimaLinha.Length > 0 ? "| Python: " + ultimaLinha : ""));
            }

            yield return null;
        }

        if (!morreu)
            Debug.Log("Rastreador externo nao respondeu a tempo — usando o rastreador interno.");
        EncerrarProcessoDoRastreador(); // não deixa processo órfão segurando a câmera
        IniciarCameraInterna();
    }

    // Guarda as últimas linhas impressas pelo Python (para mostrar se ele falhar)
    private System.Text.StringBuilder logsDoRastreador = new System.Text.StringBuilder();

    // Executa RastreadorPython/rastreador_maos.py sem abrir janela nenhuma
    void IniciarProcessoDoRastreador()
    {
        // Pasta RastreadorPython: fica na raiz do projeto (no Editor) ou ao
        // lado do executável (num build) — os dois casos são o pai de dataPath
        string raiz   = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string pasta  = System.IO.Path.Combine(raiz, "RastreadorPython");
        string script = System.IO.Path.Combine(pasta, "rastreador_maos.py");

        if (!System.IO.File.Exists(script))
        {
            Debug.LogWarning("Rastreador: script nao encontrado em " + script);
            return;
        }

        foreach (string executavel in CandidatosAPython())
        {
            try
            {
                var info = new InfoDeProcesso
                {
                    FileName               = executavel,
                    Arguments              = "-u rastreador_maos.py",
                    WorkingDirectory       = pasta,
                    UseShellExecute        = false,
                    CreateNoWindow         = true, // invisível!
                    RedirectStandardOutput = true, // capturamos o que ele imprime
                    RedirectStandardError  = true,
                };
                processoRastreador = Processo.Start(info);
                processoRastreador.OutputDataReceived += (s, e) => GuardarLogDoRastreador(e.Data);
                processoRastreador.ErrorDataReceived  += (s, e) => GuardarLogDoRastreador(e.Data);
                processoRastreador.BeginOutputReadLine();
                processoRastreador.BeginErrorReadLine();
                Debug.Log("Rastreador Python iniciado em segundo plano: " + executavel);
                return;
            }
            catch { /* esse executavel nao funcionou — tenta o proximo */ }
        }
        Debug.LogWarning("Rastreador: Python nao encontrado no PC. " +
                         "Abra EXECUTAR_RASTREADOR.bat manualmente ou instale o Python.");
    }

    // Lista de possíveis executáveis do Python, do mais confiável ao menos.
    // IMPORTANTE: os atalhos "py"/"python" do PATH podem ser aliases especiais
    // do Windows (WindowsApps) que FALHAM quando abertos pelo Unity — por isso
    // procuramos primeiro o python.exe REAL nas pastas de instalação.
    IEnumerable<string> CandidatosAPython()
    {
        string local = System.Environment.GetFolderPath(
                           System.Environment.SpecialFolder.LocalApplicationData);

        // 1) Gerenciador novo do Python (ex: AppData\Local\Python\pythoncore-3.14-64)
        string pastaNova = System.IO.Path.Combine(local, "Python");
        if (System.IO.Directory.Exists(pastaNova))
            foreach (string dir in System.IO.Directory.GetDirectories(pastaNova, "pythoncore-*"))
            {
                string exe = System.IO.Path.Combine(dir, "python.exe");
                if (System.IO.File.Exists(exe)) yield return exe;
            }

        // 2) Instalador clássico do python.org (AppData\Local\Programs\Python\Python3xx)
        string pastaClassica = System.IO.Path.Combine(local, "Programs", "Python");
        if (System.IO.Directory.Exists(pastaClassica))
            foreach (string dir in System.IO.Directory.GetDirectories(pastaClassica, "Python3*"))
            {
                string exe = System.IO.Path.Combine(dir, "python.exe");
                if (System.IO.File.Exists(exe)) yield return exe;
            }

        // 3) Launcher clássico em C:\Windows
        string pyGlobal = @"C:\Windows\py.exe";
        if (System.IO.File.Exists(pyGlobal)) yield return pyGlobal;

        // 4) Por último, os nomes do PATH (podem ser os aliases problemáticos)
        yield return "py";
        yield return "python";
    }

    void GuardarLogDoRastreador(string linha)
    {
        if (string.IsNullOrEmpty(linha)) return;
        lock (logsDoRastreador)
        {
            logsDoRastreador.AppendLine(linha);
            // guarda só as últimas ~2000 letras
            if (logsDoRastreador.Length > 2000)
                logsDoRastreador.Remove(0, logsDoRastreador.Length - 2000);
        }
    }

    void EncerrarProcessoDoRastreador()
    {
        if (processoRastreador == null) return;
        try { if (!processoRastreador.HasExited) processoRastreador.Kill(); } catch { }
        processoRastreador = null;
    }

    void IniciarCameraInterna()
    {
        if (ficheirosDaIA == null)
        {
            Debug.LogError("FATAL: 'ficheirosDaIA' (ResourceSet) nao esta vinculado no Inspector!");
            enabled = false;
            return;
        }

        // 1280x720: imagem mais nítida na tela e mais detalhe para a IA
        // (se a webcam não suportar, o Unity escolhe a resolução mais próxima)
        minhaCamera = new WebCamTexture(1280, 720);
        minhaCamera.wrapMode = TextureWrapMode.Clamp; // fora do quadro = repete a borda
        fundoDoEcra.texture = minhaCamera;
        minhaCamera.Play();

        StartCoroutine(InicializarDetetiveAposCamera());
    }

    // Aguarda câmera ter dimensões reais antes de criar o detector.
    // WebCamTexture começa com 16x16 de placeholder — criar o detector nesse momento causaria crash.
    IEnumerator InicializarDetetiveAposCamera()
    {
        Debug.Log("Aguardando camera inicializar...");

        while (!minhaCamera.isPlaying || minhaCamera.width < 100)
            yield return null;

        yield return new WaitForEndOfFrame();

        try
        {
            detetive = new HandLandmarkDetector(ficheirosDaIA);
            detetivePronto = true;
            Debug.Log("HandLandmarkDetector criado! Resolucao: " + minhaCamera.width + "x" + minhaCamera.height);
        }
        catch (System.Exception e)
        {
            Debug.LogError("FATAL: Falha ao criar HandLandmarkDetector.\n" + e.Message);
            enabled = false;
        }
    }

    void Update()
    {
        // Guarda: nenhuma fonte de rastreamento pronta ainda
        if (!usandoExterno && !detetivePronto) return;

        // Input SEMPRE antes de qualquer return.
        // GetKeyDown só existe por 1 frame — se ficar atrás de um return, some para sempre.
        if (MODO_TREINAMENTO)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shift+Tecla = APAGA todas as amostras daquela letra (para regravar do zero)
            // Tecla sozinha = GRAVA nova amostra (pode pressionar várias vezes para acumular)
            // O laço cobre TODAS as letras de A a Z (KeyCode.A até KeyCode.Z são sequenciais)
            for (KeyCode tecla = KeyCode.A; tecla <= KeyCode.Z; tecla++)
            {
                if (!Input.GetKeyDown(tecla)) continue;

                string letra = tecla.ToString(); // "A", "B", ... "Z"
                if (shift)
                    reconhecedor.ApagarLetra(letra);
                else if (reconhecedor.EhLetraDinamica(letra))
                    StartCoroutine(GravarMovimentoComAtraso(letra)); // grava o FILME
                else
                    StartCoroutine(GravarComAtraso(letra));          // grava a FOTO
            }
        }

        // Space     = pula a PALAVRA
        // Tab       = pula a LETRA atual (sem pontos)
        // Backspace = volta à letra anterior
        if (Input.GetKeyDown(KeyCode.Space)     && gerenciador != null) gerenciador.PularPalavra();
        if (Input.GetKeyDown(KeyCode.Tab)       && gerenciador != null) gerenciador.PularLetra();
        if (Input.GetKeyDown(KeyCode.Backspace) && gerenciador != null) gerenciador.VoltarLetra();

        // Mantém a imagem sem distorção (recorte central estilo "cover")
        AjustarRecorteDaCamera();

        if (usandoExterno)
        {
            // Vídeo vem do Python via UDP (o Python é o dono da webcam)
            if (rastreadorExterno.Video != null && fundoDoEcra.texture != rastreadorExterno.Video)
                fundoDoEcra.texture = rastreadorExterno.Video;
            cameraPronta = rastreadorExterno.Video != null;
        }
        else if (minhaCamera.isPlaying && minhaCamera.didUpdateThisFrame)
        {
            // Processa novo frame da câmera (apenas quando houver frame novo — performance)
            ProcessarFrame();
            cameraPronta = true;
        }

        if (!cameraPronta) return;

        // Detecção com histerese: precisa de confiança ALTA (0.6) para começar,
        // mas só solta quando cair BEM (0.45). Isso elimina o pisca-pisca do
        // esqueleto quando a confiança fica oscilando perto do limite.
        float confianca = usandoExterno ? rastreadorExterno.Score : detetive.Score;
        MaoDetectada = MaoDetectada ? (confianca >= 0.45f) : (confianca >= 0.6f);

        if (!MaoDetectada)
        {
            // Mão sumiu: limpa a memória de movimento (evita "emendar"
            // dois pedaços de gesto que não têm nada a ver)
            janelaMovimento.Clear();
            temposJanela.Clear();

            // Perdeu a mão (rastreador interno): depois de ~15 frames volta à
            // "busca ampla" (recorte grande no centro) para reencontrá-la
            if (!usandoExterno)
            {
                framesSemMao++;
                if (framesSemMao > 15)
                {
                    centroCaixa = new Vector2(0.5f, 0.5f);
                    ladoCaixaPx = 0f;
                }
            }
            return;
        }
        framesSemMao = 0;

        Vector3[] pontosDaMao = usandoExterno
            ? (Vector3[])rastreadorExterno.Pontos.Clone()
            : ColetarPontosDaMao();
        PontosDaMaoAtuais = pontosDaMao;

        // Alimenta a memória de movimento (~15 quadros por segundo)
        AlimentarJanelaDeMovimento(pontosDaMao);

        // Reposiciona o recorte para o próximo frame seguir a mão (só no interno;
        // o MediaPipe externo já faz o próprio recorte por dentro)
        if (zoomInteligente && !usandoExterno) AtualizarCaixaDaMao(pontosDaMao, confianca);

        // Índice 8 = Index4 = ponta do dedo indicador
        Vector3 posicaoDaIA = pontosDaMao[8];
        float xNoEcra = ((posicaoDaIA.x - 0.5f) * escalaX) + translaX;
        float yNoEcra = ((posicaoDaIA.y - 0.5f) * escalaY) + translaY;

        if (bolinhaDoDedo != null)
            bolinhaDoDedo.localPosition = new Vector3(xNoEcra, yNoEcra, 4f);

        // Modo Jogo: reconhece letra e envia para o GerenciadorDeJogo
        if (!MODO_TREINAMENTO && reconhecedor != null && gerenciador != null)
        {
            // Cooldown: ignora reconhecimentos muito rápidos (evita registrar a mesma letra várias vezes)
            if (Time.time - tempoUltimoReconhecimento < cooldownReconhecimento)
            {
                LimparCandidata();
                return;
            }

            // Letras com MOVIMENTO: compara os últimos ~1,4s de mão com os
            // movimentos gravados (DTW). Roda algumas vezes por segundo.
            if (Time.time - tempoUltimaChecagemMovimento >= intervaloChecagemMovimento)
            {
                tempoUltimaChecagemMovimento = Time.time;
                string letraMovimento = reconhecedor.ClassificarSinalDinamico(janelaMovimento);
                if (letraMovimento != "Desconhecido" && gerenciador.TentarLetra(letraMovimento))
                {
                    // (sem aprendizado automático aqui: letra de MOVIMENTO não
                    //  deve virar amostra estática — poluiria o banco de fotos)
                    tempoUltimoReconhecimento = Time.time;
                    janelaMovimento.Clear();
                    temposJanela.Clear();
                    LimparCandidata();
                    return;
                }
            }

            string letraFeita = reconhecedor.ClassificarLetra(pontosDaMao);

            if (letraFeita == "Desconhecido" || letraFeita == "Nenhuma")
            {
                LimparCandidata();
                return;
            }

            // Filtro de estabilidade: a MESMA letra precisa ser vista por
            // alguns instantes seguidos antes de valer. Elimina falsos
            // positivos enquanto a mão troca de um sinal para outro.
            if (letraFeita != letraCandidata)
            {
                letraCandidata      = letraFeita;
                tempoLetraCandidata = Time.time;
            }

            // Sinal MUITO parecido com o gravado? Aceita na metade do tempo.
            float tempoNecessario = (reconhecedor.UltimaDistancia < reconhecedor.toleranciaDeErro * 0.5f)
                                    ? tempoEstabilidade * 0.5f
                                    : tempoEstabilidade;

            // A interface lê isto para mostrar a barrinha de progresso do sinal
            LetraCandidata     = letraCandidata;
            ProgressoCandidata = Mathf.Clamp01((Time.time - tempoLetraCandidata) / tempoNecessario);

            if (ProgressoCandidata < 1f) return;

            bool acertou = gerenciador.TentarLetra(letraFeita);
            if (acertou)
            {
                // Aprendizado automático: cada acerto vira nova amostra de treinamento
                reconhecedor.AprendizagemAutomatica(letraFeita, pontosDaMao);
                tempoUltimoReconhecimento = Time.time;
                LimparCandidata();
            }
        }
        else
        {
            LimparCandidata();
        }
    }

    void LimparCandidata()
    {
        letraCandidata     = "";
        LetraCandidata     = "";
        ProgressoCandidata = 0f;
    }

    // Ajusta o pedaço visível do feed (uvRect) para preencher a tela SEM esticar:
    // recorta o centro da imagem, como os apps de câmera de celular fazem.
    void AjustarRecorteDaCamera()
    {
        // Dimensões da fonte de vídeo atual (webcam interna OU vídeo do Python)
        float larguraFonte, alturaFonte;
        if (usandoExterno)
        {
            if (rastreadorExterno.Video == null) return;
            larguraFonte = rastreadorExterno.Video.width;
            alturaFonte  = rastreadorExterno.Video.height;
        }
        else
        {
            if (minhaCamera == null || minhaCamera.width < 100) return;
            larguraFonte = minhaCamera.width;
            alturaFonte  = minhaCamera.height;
        }

        if (Screen.width == larguraTelaAnterior && Screen.height == alturaTelaAnterior &&
            zoomDaTela == zoomAnterior) return;
        larguraTelaAnterior = Screen.width;
        alturaTelaAnterior  = Screen.height;
        zoomAnterior        = zoomDaTela;

        float aspectoTela   = (float)Screen.width / Screen.height;
        float aspectoCamera = larguraFonte / alturaFonte;

        // Fração do quadro visível em cada eixo (recorte "cover", sem esticar)
        float w, h;
        if (aspectoCamera > aspectoTela)
        {
            w = aspectoTela / aspectoCamera; // corta as laterais
            h = 1f;
        }
        else
        {
            w = 1f;
            h = aspectoCamera / aspectoTela; // corta topo e base
        }

        // Aplica a margem de segurança (mostra menos → sobra folga nas bordas)
        w /= zoomDaTela;
        h /= zoomDaTela;

        uvRecorte = new Rect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h);
        fundoDoEcra.uvRect = uvRecorte;
    }

    // Prepara a imagem e envia para a IA.
    // Com zoom inteligente: recorta um quadrado ao redor da mão (Graphics.Blit)
    // e envia só ele — a mão ocupa a imagem inteira e o modelo enxerga MUITO melhor.
    void ProcessarFrame()
    {
        if (!zoomInteligente)
        {
            escalaBlit = Vector2.one;
            offsetBlit = Vector2.zero;
            detetive.ProcessImage(minhaCamera);
            return;
        }

        if (texturaRecorte == null)
            texturaRecorte = new RenderTexture(320, 320, 0);

        float w = minhaCamera.width;
        float h = minhaCamera.height;

        // Sem mão rastreada: quadrado grande no centro (área visível da tela).
        // Com mão rastreada: quadrado do tamanho da mão × margem.
        float lado = (ladoCaixaPx > 0f) ? ladoCaixaPx : Mathf.Min(w, h);
        lado = Mathf.Clamp(lado, 160f, Mathf.Min(w, h));

        // Converte centro+lado do recorte em escala/deslocamento normalizados.
        // O recorte pode "vazar" até 30% para fora do quadro: a borda é repetida
        // (wrap Clamp), mas a mão continua no CENTRO do que a IA analisa —
        // muito melhor do que empurrar o recorte e descentralizar a mão.
        const float folga = 0.3f;
        Vector2 escala = new Vector2(lado / w, lado / h);
        Vector2 offset = new Vector2(centroCaixa.x - escala.x * 0.5f,
                                     centroCaixa.y - escala.y * 0.5f);
        offset.x = Mathf.Clamp(offset.x, -escala.x * folga, 1f - escala.x * (1f - folga));
        offset.y = Mathf.Clamp(offset.y, -escala.y * folga, 1f - escala.y * (1f - folga));

        Graphics.Blit(minhaCamera, texturaRecorte, escala, offset);
        detetive.ProcessImage(texturaRecorte);

        escalaBlit = escala;
        offsetBlit = offset;
    }

    // Recalcula a caixa de recorte a partir da mão detectada (com suavização,
    // para o recorte deslizar atrás da mão em vez de pular)
    void AtualizarCaixaDaMao(Vector3[] pontos, float confianca)
    {
        float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
        for (int i = 0; i < 21; i++)
        {
            if (pontos[i].x < minX) minX = pontos[i].x;
            if (pontos[i].x > maxX) maxX = pontos[i].x;
            if (pontos[i].y < minY) minY = pontos[i].y;
            if (pontos[i].y > maxY) maxY = pontos[i].y;
        }

        Vector2 centro = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        float larguraPx = (maxX - minX) * minhaCamera.width;
        float alturaPx  = (maxY - minY) * minhaCamera.height;

        // Confiança caindo (mão rápida ou perto da borda)? Abre o recorte
        // para dar mais contexto ao modelo ANTES de perder o rastreio.
        float margem = margemDoZoom * ((confianca < 0.6f) ? 1.5f : 1f);
        float alvoLado = Mathf.Max(larguraPx, alturaPx) * margem;

        centroCaixa = Vector2.Lerp(centroCaixa, centro, 0.35f);
        ladoCaixaPx = (ladoCaixaPx <= 0f) ? alvoLado : Mathf.Lerp(ladoCaixaPx, alvoLado, 0.25f);
    }

    // Converte os 21 landmarks do enum para Vector3[].
    // HandLandmarkDetector.GetKeyPoint() retorna Vector2 (x,y normalizados 0-1)
    // DENTRO DO RECORTE — aqui mapeamos de volta para o frame inteiro, assim
    // todo o resto do jogo (esqueleto, hover, reconhecimento) continua igual.
    // Definimos z=0 pois o reconhecimento de LIBRAS usa apenas posição 2D.
    Vector3[] ColetarPontosDaMao()
    {
        Vector3[] pontos = new Vector3[21];
        var valores = (HandLandmarkDetector.KeyPoint[])System.Enum.GetValues(typeof(HandLandmarkDetector.KeyPoint));
        for (int i = 0; i < valores.Length; i++)
        {
            Vector2 v2 = detetive.GetKeyPoint(valores[i]);
            pontos[i] = new Vector3(offsetBlit.x + v2.x * escalaBlit.x,
                                    offsetBlit.y + v2.y * escalaBlit.y, 0f);
        }
        return pontos;
    }

    // Adiciona o quadro atual à "memória de movimento" (a ~15 quadros/s,
    // independente do FPS do jogo) e descarta o que passou da janela
    void AlimentarJanelaDeMovimento(Vector3[] pontos)
    {
        if (Time.time - tempoUltimaAmostraJanela < 1f / 15f) return;
        tempoUltimaAmostraJanela = Time.time;

        janelaMovimento.Add((Vector3[])pontos.Clone());
        temposJanela.Add(Time.time);

        while (temposJanela.Count > 0 && temposJanela[0] < Time.time - duracaoJanelaMovimento)
        {
            temposJanela.RemoveAt(0);
            janelaMovimento.RemoveAt(0);
        }
    }

    // Treinamento de letra DINÂMICA: conta 3s e grava um "filme" de ~1,3s
    IEnumerator GravarMovimentoComAtraso(string letra)
    {
        Debug.Log("Letra com MOVIMENTO [" + letra + "]! Prepare-se... gravando em 3 segundos!");
        yield return new WaitForSeconds(3f);

        Debug.Log(">>> FACA O MOVIMENTO DE [" + letra + "] AGORA! <<<");
        var quadros = new List<Vector3[]>();
        float fim = Time.time + duracaoGravacaoMovimento;
        float proximaAmostra = 0f;

        while (Time.time < fim)
        {
            if (MaoDetectada && PontosDaMaoAtuais != null && Time.time >= proximaAmostra)
            {
                quadros.Add((Vector3[])PontosDaMaoAtuais.Clone());
                proximaAmostra = Time.time + 1f / 15f; // ~15 quadros/s
            }
            yield return null;
        }

        if (quadros.Count < 8)
        {
            Debug.LogError("Poucos quadros com a mao visivel (" + quadros.Count +
                           "). Mantenha a mao no quadro e tente de novo.");
            yield break;
        }
        reconhecedor.GravarSinalDinamico(letra, quadros);
    }

    IEnumerator GravarComAtraso(string letra)
    {
        Debug.Log("Prepare a letra [" + letra + "]! Gravando em 3 segundos...");
        yield return new WaitForSeconds(3f);

        float confianca = usandoExterno
            ? rastreadorExterno.Score
            : (detetivePronto ? detetive.Score : 0f);

        if (confianca < 0.5f)
        {
            Debug.LogError("Falhou! A IA nao detectou sua mao. Tente novamente.");
            yield break;
        }

        Vector3[] pontosAgora = usandoExterno
            ? (Vector3[])rastreadorExterno.Pontos.Clone()
            : ColetarPontosDaMao();
        reconhecedor.GravarLetra(letra, pontosAgora);
    }

    void OnDestroy()
    {
        EncerrarProcessoDoRastreador(); // fecha o Python junto com o jogo
        detetive?.Dispose();
        if (texturaRecorte != null) texturaRecorte.Release();
        if (minhaCamera != null && minhaCamera.isPlaying)
            minhaCamera.Stop();
    }
}
