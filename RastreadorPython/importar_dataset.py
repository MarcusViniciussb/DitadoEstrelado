# -*- coding: utf-8 -*-
"""
Importador de dataset de alfabeto em LIBRAS para o DitadoEstrelado.

Pega uma pasta de FOTOS organizadas por letra, passa cada foto pelo
MediaPipe (offline), extrai os 21 pontos da mao e adiciona as amostras
direto no banco do jogo (Assets/MeuAlfabeto.asset).

Resultado: o jogo passa a reconhecer maos DIFERENTES da sua — crucial
para a demo (a banca pode testar com a propria mao!).

USO:
  1) Baixe um dataset de alfabeto em LIBRAS (ex: Kaggle "libras alphabet")
     e descompacte. A pasta deve ter uma SUBPASTA POR LETRA:
         dataset/
           A/  foto1.jpg  foto2.jpg ...
           B/  ...
  2) FECHE o Unity (ou nao deixe o MeuAlfabeto aberto no Inspector)
  3) Rode:   py importar_dataset.py caminho/da/pasta/dataset
     Opcoes:  --max 15      (maximo de amostras novas por letra)
              --espelhar    (tambem adiciona a versao espelhada = outra mao)
  4) Abra o Unity — ele recarrega o banco sozinho

Um backup do MeuAlfabeto.asset e criado automaticamente antes de mexer.
"""

import argparse
import os
import shutil
import sys
import time

import mediapipe as mp
from mediapipe.tasks import python as mp_tasks
from mediapipe.tasks.python import vision

PASTA  = os.path.dirname(os.path.abspath(__file__))
MODELO = os.path.join(PASTA, "hand_landmarker.task")
ASSET  = os.path.normpath(os.path.join(PASTA, "..", "Assets", "MeuAlfabeto.asset"))

# Letras com MOVIMENTO nao podem vir de fotos (foto nao tem movimento)
LETRAS_DINAMICAS = {"H", "J", "K", "W", "X", "Z", "Ç", "C_CEDILHA"}

EXTENSOES = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}


def extrair_pontos(detector, caminho_imagem):
    """Retorna 21 pontos relativos ao pulso (convencao do jogo) ou None."""
    try:
        imagem = mp.Image.create_from_file(caminho_imagem)
    except Exception:
        return None

    resultado = detector.detect(imagem)
    if not resultado.hand_landmarks:
        return None

    marcos = resultado.hand_landmarks[0]
    # Converte para a convencao do jogo: Y para CIMA, relativo ao pulso
    absolutos = [(m.x, 1.0 - m.y, m.z) for m in marcos]
    px, py, pz = absolutos[0]
    return [(x - px, y - py, z - pz) for (x, y, z) in absolutos]


def formatar_amostra(letra, pontos):
    """Gera o bloco YAML de uma amostra no formato do MeuAlfabeto.asset."""
    linhas = [f"  - nome: {letra}", "    pontosNormalizados:"]
    for (x, y, z) in pontos:
        linhas.append(f"    - {{x: {x:.8f}, y: {y:.8f}, z: {z:.8f}}}")
    return "\n".join(linhas)


def principal():
    parser = argparse.ArgumentParser(description="Importa fotos de LIBRAS para o banco do jogo")
    parser.add_argument("pasta", help="pasta do dataset (uma subpasta por letra)")
    parser.add_argument("--max", type=int, default=15, help="maximo de amostras novas por letra")
    parser.add_argument("--espelhar", action="store_true",
                        help="adiciona tambem a versao espelhada (cobre a outra mao)")
    args = parser.parse_args()

    if not os.path.isdir(args.pasta):
        print("ERRO: pasta nao encontrada:", args.pasta)
        return
    if not os.path.exists(ASSET):
        print("ERRO: MeuAlfabeto.asset nao encontrado em:", ASSET)
        return

    # Detector no modo IMAGEM (uma foto por vez)
    opcoes = vision.HandLandmarkerOptions(
        base_options=mp_tasks.BaseOptions(model_asset_path=MODELO),
        running_mode=vision.RunningMode.IMAGE,
        num_hands=1,
        min_hand_detection_confidence=0.5,
    )
    detector = vision.HandLandmarker.create_from_options(opcoes)
    print("[OK] Detector MediaPipe carregado")

    # Percorre as subpastas de letras
    blocos = []
    resumo = {}
    for nome_sub in sorted(os.listdir(args.pasta)):
        sub = os.path.join(args.pasta, nome_sub)
        if not os.path.isdir(sub):
            continue
        letra = nome_sub.strip().upper()
        if len(letra) != 1 or not letra.isalpha():
            print(f"  (pulando pasta '{nome_sub}': nome nao e uma letra)")
            continue
        if letra in LETRAS_DINAMICAS:
            print(f"  (pulando '{letra}': letra com movimento nao vem de foto)")
            continue

        adicionadas = 0
        for arquivo in sorted(os.listdir(sub)):
            if adicionadas >= args.max:
                break
            if os.path.splitext(arquivo)[1].lower() not in EXTENSOES:
                continue

            pontos = extrair_pontos(detector, os.path.join(sub, arquivo))
            if pontos is None:
                continue  # sem mao detectada nessa foto

            blocos.append(formatar_amostra(letra, pontos))
            adicionadas += 1

            if args.espelhar and adicionadas < args.max:
                espelhado = [(-x, y, z) for (x, y, z) in pontos]
                blocos.append(formatar_amostra(letra, espelhado))
                adicionadas += 1

        if adicionadas > 0:
            resumo[letra] = adicionadas
            print(f"  [{letra}] {adicionadas} amostras extraidas")

    if not blocos:
        print("Nenhuma amostra extraida — confira a organizacao da pasta.")
        return

    # Backup e insercao no asset
    backup = ASSET + ".backup-" + time.strftime("%Y%m%d-%H%M%S")
    shutil.copy2(ASSET, backup)
    print("[OK] Backup criado:", os.path.basename(backup))

    with open(ASSET, "r", encoding="utf-8") as f:
        conteudo = f.read()

    novo_texto = "\n".join(blocos) + "\n"

    if "  letrasGravadas: []" in conteudo:
        # Banco vazio: abre a lista
        conteudo = conteudo.replace("  letrasGravadas: []",
                                    "  letrasGravadas:\n" + novo_texto.rstrip("\n"))
    elif "\n  sinaisDinamicos:" in conteudo:
        # Insere as amostras novas no FIM da lista de letras estaticas
        conteudo = conteudo.replace("\n  sinaisDinamicos:",
                                    "\n" + novo_texto + "  sinaisDinamicos:")
    else:
        # Formato antigo sem sinais dinamicos: anexa no final
        conteudo = conteudo.rstrip("\n") + "\n" + novo_texto

    with open(ASSET, "w", encoding="utf-8", newline="\n") as f:
        f.write(conteudo)

    total = sum(resumo.values())
    print()
    print(f"[SUCESSO] {total} amostras adicionadas ao MeuAlfabeto.asset")
    print("Letras:", "   ".join(f"{l}: {n}" for l, n in sorted(resumo.items())))
    print()
    print("Agora abra o Unity — o banco novo carrega sozinho.")
    print("Se algo der errado, restaure o backup:", os.path.basename(backup))


if __name__ == "__main__":
    try:
        principal()
    except KeyboardInterrupt:
        print("\nCancelado.")
