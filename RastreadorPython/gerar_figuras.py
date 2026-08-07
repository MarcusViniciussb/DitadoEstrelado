# -*- coding: utf-8 -*-
"""
Gera as imagens comparativas (antes/depois) do relatorio de Visao Computacional.

Produz, a partir de UM quadro da webcam (ou de uma foto), um painel com as
etapas do processamento lado a lado, exatamente o que o professor recomendou:
  1) quadro original capturado
  2) quadro reduzido para 640 px de largura (entrada da inferencia)
  3) recorte da regiao da mao (ROI)
  4) esqueleto de 21 pontos sobreposto (saida do rastreamento)

USO:
  py gerar_figuras.py                 -> captura da webcam (aperte ESPACO)
  py gerar_figuras.py foto.jpg        -> usa uma foto existente

Saidas (nesta pasta): fig_1_original.png, fig_2_reduzido.png,
fig_3_recorte.png, fig_4_esqueleto.png e fig_painel.png (as quatro juntas).
"""

import os
import sys
import cv2
import numpy as np
import mediapipe as mp
from mediapipe.tasks import python as mp_tasks
from mediapipe.tasks.python import vision

PASTA  = os.path.dirname(os.path.abspath(__file__))
MODELO = os.path.join(PASTA, "hand_landmarker.task")

# Conexoes do esqueleto (mesmos ossos usados no jogo)
OSSOS = [
    (0,1),(1,2),(2,3),(3,4), (0,5),(5,6),(6,7),(7,8),
    (0,9),(9,10),(10,11),(11,12), (0,13),(13,14),(14,15),(15,16),
    (0,17),(17,18),(18,19),(19,20), (5,9),(9,13),(13,17),
]


def capturar_da_webcam():
    cam = cv2.VideoCapture(0, cv2.CAP_DSHOW)
    cam.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
    cam.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
    print("Enquadre a mao e aperte ESPACO para capturar (ou ESC para sair).")
    quadro = None
    while True:
        ok, q = cam.read()
        if not ok:
            continue
        cv2.imshow("Captura (ESPACO = tirar foto)", q)
        tecla = cv2.waitKey(1) & 0xFF
        if tecla == 32:      # espaco
            quadro = q.copy()
            break
        if tecla == 27:      # esc
            break
    cam.release()
    cv2.destroyAllWindows()
    return quadro


def rotular(img, texto):
    """Escreve um titulo com faixa preta no topo da imagem."""
    img = img.copy()
    cv2.rectangle(img, (0, 0), (img.shape[1], 34), (0, 0, 0), -1)
    cv2.putText(img, texto, (10, 24), cv2.FONT_HERSHEY_SIMPLEX, 0.7,
                (255, 255, 255), 2, cv2.LINE_AA)
    return img


def principal():
    if not os.path.exists(MODELO):
        print("ERRO: modelo hand_landmarker.task nao encontrado.")
        return

    if len(sys.argv) > 1:
        original = cv2.imread(sys.argv[1])
        if original is None:
            print("ERRO: nao consegui abrir a imagem", sys.argv[1])
            return
    else:
        original = capturar_da_webcam()
        if original is None:
            print("Cancelado.")
            return

    # ── Etapa 1: original ────────────────────────────────────────────────────
    h0, w0 = original.shape[:2]

    # ── Etapa 2: reduzido para 640 de largura ────────────────────────────────
    escala = 640.0 / w0
    reduzido = cv2.resize(original, (640, int(h0 * escala)))

    # ── Deteccao dos pontos ──────────────────────────────────────────────────
    detector = vision.HandLandmarker.create_from_options(
        vision.HandLandmarkerOptions(
            base_options=mp_tasks.BaseOptions(model_asset_path=MODELO),
            running_mode=vision.RunningMode.IMAGE, num_hands=1))
    rgb = cv2.cvtColor(reduzido, cv2.COLOR_BGR2RGB)
    resultado = detector.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb))

    esqueleto = reduzido.copy()
    recorte = reduzido.copy()
    if resultado.hand_landmarks:
        m = resultado.hand_landmarks[0]
        hh, ww = reduzido.shape[:2]
        pts = [(int(p.x * ww), int(p.y * hh)) for p in m]

        # ── Etapa 4: esqueleto sobreposto ────────────────────────────────────
        for (a, b) in OSSOS:
            cv2.line(esqueleto, pts[a], pts[b], (230, 220, 0), 2, cv2.LINE_AA)
        for i, (x, y) in enumerate(pts):
            cor = (0, 200, 255) if i == 0 else (255, 255, 255)
            cv2.circle(esqueleto, (x, y), 4, cor, -1, cv2.LINE_AA)

        # ── Etapa 3: recorte da ROI (caixa da mao com margem) ────────────────
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        margem = 30
        x1 = max(0, min(xs) - margem); y1 = max(0, min(ys) - margem)
        x2 = min(ww, max(xs) + margem); y2 = min(hh, max(ys) + margem)
        recorte = reduzido[y1:y2, x1:x2].copy()
    else:
        print("AVISO: nenhuma mao detectada. Repita com a mao bem enquadrada.")

    # ── Salva as quatro imagens ──────────────────────────────────────────────
    f1 = rotular(original, "1) Original " + str(w0) + "x" + str(h0))
    f2 = rotular(reduzido, "2) Reduzido 640 (entrada da IA)")
    f3 = rotular(recorte,  "3) Recorte da mao (ROI)")
    f4 = rotular(esqueleto, "4) 21 pontos (saida)")
    cv2.imwrite(os.path.join(PASTA, "fig_1_original.png"), f1)
    cv2.imwrite(os.path.join(PASTA, "fig_2_reduzido.png"), f2)
    cv2.imwrite(os.path.join(PASTA, "fig_3_recorte.png"), f3)
    cv2.imwrite(os.path.join(PASTA, "fig_4_esqueleto.png"), f4)

    # ── Painel: original e esqueleto lado a lado (mesma altura) ───────────────
    alvo_h = 480
    def redim(img):
        r = alvo_h / img.shape[0]
        return cv2.resize(img, (int(img.shape[1] * r), alvo_h))
    painel = np.hstack([redim(f2), redim(f4)])
    cv2.imwrite(os.path.join(PASTA, "fig_painel.png"), painel)

    print("Imagens geradas nesta pasta:")
    print("  fig_1_original.png, fig_2_reduzido.png, fig_3_recorte.png,")
    print("  fig_4_esqueleto.png e fig_painel.png (entrada + saida lado a lado).")


if __name__ == "__main__":
    principal()
