using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

// RastreadorExterno: recebe os pontos da mão e o vídeo enviados pelo script
// Python (RastreadorPython/rastreador_maos.py) por UDP em 127.0.0.1.
// É comunicação interna do próprio PC - funciona 100% offline.
//
// O ControladorCamera consulta este componente: se o Python estiver rodando,
// usa ele (MediaPipe completo); senão, usa o rastreador interno antigo.
public class RastreadorExterno : MonoBehaviour
{
    [Header("Portas (iguais as do script Python)")]
    public int portaLandmarks = 5052;
    public int portaVideo    = 5053;

    [Header("Sem pacotes por este tempo = rastreador desligado")]
    public float tempoMaximoSemSinal = 1.5f;

    // ── O que o resto do jogo lê ─────────────────────────────────────────────
    public bool      Ativo  => Time.realtimeSinceStartup - tempoUltimoPacote < tempoMaximoSemSinal;
    public float     Score  { get; private set; }
    public Vector3[] Pontos { get; private set; } = new Vector3[21];
    public Texture2D Video  { get; private set; }

    // ── Recepção em segundo plano ────────────────────────────────────────────
    private UdpClient udpLandmarks;
    private UdpClient udpVideo;
    private Thread    threadLandmarks;
    private Thread    threadVideo;
    private readonly object trava = new object();
    private byte[] pacoteLandmarks;
    private byte[] pacoteVideo;
    private bool   novoLandmarks;
    private bool   novoVideo;
    private volatile bool rodando;
    private float tempoUltimoPacote = -999f;

    void Awake()
    {
        rodando = true;
        try
        {
            udpLandmarks = new UdpClient(portaLandmarks);
            udpVideo     = new UdpClient(portaVideo);
        }
        catch (Exception e)
        {
            Debug.LogWarning("RastreadorExterno: nao consegui abrir as portas UDP (" + e.Message + ")");
            enabled = false;
            return;
        }

        // Threads de fundo: ficam bloqueadas esperando pacotes, sem travar o jogo
        threadLandmarks = new Thread(() => LacoDeRecepcao(udpLandmarks, true))  { IsBackground = true };
        threadVideo     = new Thread(() => LacoDeRecepcao(udpVideo,     false)) { IsBackground = true };
        threadLandmarks.Start();
        threadVideo.Start();
    }

    void LacoDeRecepcao(UdpClient udp, bool ehLandmarks)
    {
        IPEndPoint origem = new IPEndPoint(IPAddress.Any, 0);
        while (rodando)
        {
            try
            {
                byte[] dados = udp.Receive(ref origem);
                lock (trava)
                {
                    if (ehLandmarks) { pacoteLandmarks = dados; novoLandmarks = true; }
                    else             { pacoteVideo     = dados; novoVideo     = true; }
                }
            }
            catch { /* socket fechado ao sair - normal */ }
        }
    }

    void Update()
    {
        // Copia os pacotes mais recentes (com trava, pois vêm de outra thread)
        byte[] lm = null, vid = null;
        lock (trava)
        {
            if (novoLandmarks) { lm  = pacoteLandmarks; novoLandmarks = false; }
            if (novoVideo)     { vid = pacoteVideo;     novoVideo     = false; }
        }

        // Pacote de pontos: byte 'M' (0x4D) + confiança + 21 × (x,y,z) float32
        if (lm != null && lm.Length >= 1 + 4 + 21 * 12 && lm[0] == 0x4D)
        {
            Score = BitConverter.ToSingle(lm, 1);
            for (int i = 0; i < 21; i++)
            {
                int b = 5 + i * 12;
                Pontos[i] = new Vector3(
                    BitConverter.ToSingle(lm, b),
                    BitConverter.ToSingle(lm, b + 4),
                    BitConverter.ToSingle(lm, b + 8));
            }
            tempoUltimoPacote = Time.realtimeSinceStartup;
        }

        // Pacote de vídeo: bytes de um JPEG
        if (vid != null)
        {
            if (Video == null)
                Video = new Texture2D(2, 2, TextureFormat.RGB24, false);
            Video.LoadImage(vid); // decodifica o JPEG e atualiza a textura
        }
    }

    void OnDestroy()
    {
        rodando = false;
        udpLandmarks?.Close();
        udpVideo?.Close();
    }
}
