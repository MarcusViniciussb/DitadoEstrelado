# Ditado Estrelado

Jogo educativo de soletracao em LIBRAS, desenvolvido para a disciplina de
Visao Computacional do Programa de Pos-Graduacao em Computacao Aplicada
(PPGCA) do IFMA - Campus Sao Luis Monte Castelo.

Um objeto 3D aparece na tela (fruta, animal, veiculo ou comida) e o jogador
precisa soletrar o nome dele fazendo os sinais do alfabeto manual de LIBRAS
para a webcam. O jogo tem 4 fases com dificuldade crescente, sistema de
vidas, tempo por palavra e pontuacao.

## Como executar

Requisitos:

- Unity 2022.3 LTS
- Python 3 com os pacotes mediapipe e opencv-python
  (instalar com: py -m pip install mediapipe opencv-python)

Passos:

1. Abra a pasta do projeto no Unity Hub
2. Abra a cena Assets/Scenes/SampleScene.unity
3. Aperte Play. O rastreador de maos (Python) inicia sozinho em segundo
   plano. Se preferir, execute EXECUTAR_RASTREADOR.bat antes do Play.

Sem o Python instalado o jogo continua funcionando com um rastreador
interno mais simples (menor precisao).

No menu inicial da para escolher a orientacao da tela: celular (retrato)
ou PC/tablet (paisagem).

## Controles

- Os botoes funcionam por toque, mouse ou apontando o dedo por 3 segundos
- Espaco pula a palavra (custa 10 pontos), Tab pula a letra (custa 5),
  Backspace volta uma letra
- Setas esquerda/direita trocam de palavra sem custo (uso em demonstracoes)
- Modo treinamento (senha padrao: 1234): faca o sinal e pressione a tecla
  da letra para gravar; Shift + tecla apaga as amostras daquela letra.
  As letras H, J, K, W, X, Z e C-cedilha sao gravadas como movimento
  (1,3 segundos de gesto).

## Reconhecimento

O rastreamento usa o MediaPipe (Google) rodando localmente, sem internet.
Os 21 pontos da mao sao enviados ao Unity por UDP em 127.0.0.1.

A classificacao das letras estaticas usa kNN (k=3) sobre os pontos
normalizados por tamanho da mao mais os angulos das articulacoes. As
letras com movimento sao comparadas por DTW (Dynamic Time Warping) sobre
janelas de 1,4 segundos.

O arquivo RastreadorPython/importar_dataset.py permite enriquecer o banco
de sinais com fotos de datasets publicos de LIBRAS, para o jogo reconhecer
maos de outras pessoas.

## Estrutura

- Assets/*.cs           scripts do jogo (reconhecimento, interface, fases, audio)
- Assets/Resources/     modelos 3D usados nas fases
- Assets/MeuAlfabeto.asset  banco de sinais gravados
- RastreadorPython/     rastreador de maos e importador de dataset

## Creditos

Autor: Marcus Strabello

Orientacao: Prof. Dr. Alex Martins Santos

Modelos 3D: Quaternius e Kay Lousberg (licencas livres, incluidas nas pastas)
