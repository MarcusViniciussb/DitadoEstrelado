# -*- coding: utf-8 -*-
"""
Rastreador de maos com MediaPipe (Google) para o DitadoEstrelado.

Roda 100% OFFLINE: captura a webcam, detecta os 21 pontos da mao com o
pipeline completo do MediaPipe (deteccao de palma + landmarks + rastreamento
temporal) e envia tudo para o Unity por UDP em 127.0.0.1 (comunicacao interna
do proprio PC - nao usa internet).

Como usar: execute EXECUTAR_RASTREADOR.bat (na pasta do projeto) ANTES de
dar Play no Unity. Se este script nao estiver rodando, o jogo usa o
rastreador interno antigo automaticamente.

Pacotes: py -m pip install mediapipe opencv-python
Modelo:  hand_landmarker.task (nesta pasta, ja baixado)
"""

import os
import socket
import struct
import time

import cv2
import mediapipe as mp
from mediapipe.tasks import python as mp_tasks
from mediapipe.tasks.python import vision

# ── Configuracao ─────────────────────────────────────────────────────────────
IP_UNITY        = "127.0.0.1"  # localhost: mesmo PC, sem internet
PORTA_LANDMARKS = 5052         # pontos da mao
PORTA_VIDEO     = 5053         # imagem da camera (JPEG)
LARGURA_CAPTURA = 640         # resolucao pedida a webcam
ALTURA_CAPTURA  = 480
LARGURA_ENVIO   = 640          # video reduzido p/ caber em 1 pacote UDP

PASTA   = os.path.dirname(os.path.abspath(__file__))
MODELO  = os.path.join(PASTA, "hand_landmarker.task")


def principal():
    print("=" * 60)
    print("  RASTREADOR DE MAOS - DitadoEstrelado (MediaPipe/Google)")
    print("=" * 60)

    if not os.path.exists(MODELO):
        print("ERRO: modelo 'hand_landmarker.task' nao encontrado em:", PASTA)
        return

    # Detector no modo VIDEO: usa o resultado do frame anterior para rastrear
    # (mais estavel e rapido do que detectar do zero a cada frame)
    opcoes = vision.HandLandmarkerOptions(
        base_options=mp_tasks.BaseOptions(model_asset_path=MODELO),
        running_mode=vision.RunningMode.VIDEO,
        num_hands=1,
        min_hand_detection_confidence=0.5,
        min_hand_presence_confidence=0.5,
        min_tracking_confidence=0.5,
    )
    detector = vision.HandLandmarker.create_from_options(opcoes)
    print("[OK] Detector MediaPipe carregado (modelo local, offline)")

    # CAP_DSHOW abre a camera mais rapido no Windows
    camera = cv2.VideoCapture(0, cv2.CAP_DSHOW)
    # MJPG: formato comprimido - sem ele, muitas webcams travam em 10fps no 720p
    camera.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))
    camera.set(cv2.CAP_PROP_FRAME_WIDTH,  LARGURA_CAPTURA)
    camera.set(cv2.CAP_PROP_FRAME_HEIGHT, ALTURA_CAPTURA)
    camera.set(cv2.CAP_PROP_FPS, 30)
    if not camera.isOpened():
        print("ERRO: nao consegui abrir a webcam.")
        print("Dica: feche o Unity (se estiver em Play) e outros apps de camera.")
        return
    largura = int(camera.get(cv2.CAP_PROP_FRAME_WIDTH))
    altura  = int(camera.get(cv2.CAP_PROP_FRAME_HEIGHT))
    print(f"[OK] Webcam aberta: {largura}x{altura}")

    soquete = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"[OK] Enviando para o Unity em {IP_UNITY}:{PORTA_LANDMARKS} (pontos) e :{PORTA_VIDEO} (video)")
    print()
    print(">>> Deixe esta janela ABERTA e de Play no Unity. (Ctrl+C encerra) <<<")
    print()

    inicio = time.time()
    quadros = 0
    detecoes = 0
    ultimo_relatorio = time.time()

    while True:
        ok, quadro = camera.read()
        if not ok:
            time.sleep(0.01)
            continue

        # Reduz o quadro ANTES de tudo: o MediaPipe redimensiona internamente
        # de qualquer forma, entao detectar no quadro menor nao perde precisao
        # e dobra a velocidade (os pontos sao normalizados 0-1, independem
        # da resolucao)
        altura_envio = int(LARGURA_ENVIO * altura / largura)
        reduzido = cv2.resize(quadro, (LARGURA_ENVIO, altura_envio))

        # ── Deteccao ────────────────────────────────────────────────────
        rgb = cv2.cvtColor(reduzido, cv2.COLOR_BGR2RGB)
        imagem = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        carimbo_ms = int((time.time() - inicio) * 1000)
        resultado = detector.detect_for_video(imagem, carimbo_ms)

        confianca = 0.0
        pontos = [0.0] * 63  # 21 pontos x (x, y, z)
        if resultado.hand_landmarks:
            marcos = resultado.hand_landmarks[0]
            confianca = (resultado.handedness[0][0].score
                         if resultado.handedness else 1.0)
            for i, marco in enumerate(marcos):
                pontos[i * 3 + 0] = marco.x
                pontos[i * 3 + 1] = 1.0 - marco.y  # Unity usa Y para CIMA
                pontos[i * 3 + 2] = marco.z        # profundidade relativa!
            detecoes += 1

        # Pacote binario: byte magico 'M' + confianca + 63 floats
        pacote = struct.pack("<Bf63f", 0x4D, confianca, *pontos)
        soquete.sendto(pacote, (IP_UNITY, PORTA_LANDMARKS))

        # ── Video para o Unity (JPEG reduzido) ──────────────────────────
        ok_jpg, jpg = cv2.imencode(".jpg", reduzido,
                                   [cv2.IMWRITE_JPEG_QUALITY, 70])
        if ok_jpg and len(jpg) < 65000:  # limite de 1 datagrama UDP
            soquete.sendto(jpg.tobytes(), (IP_UNITY, PORTA_VIDEO))

        # ── Relatorio no console a cada 5s ──────────────────────────────
        quadros += 1
        agora = time.time()
        if agora - ultimo_relatorio >= 5.0:
            fps = quadros / (agora - ultimo_relatorio)
            taxa = 100.0 * detecoes / max(1, quadros)
            print(f"  {fps:5.1f} fps | mao detectada em {taxa:4.1f}% dos quadros")
            quadros = 0
            detecoes = 0
            ultimo_relatorio = agora


if __name__ == "__main__":
    try:
        principal()
    except KeyboardInterrupt:
        print("\nEncerrado pelo usuario. Ate mais!")
