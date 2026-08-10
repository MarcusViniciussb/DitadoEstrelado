# Ditado Estrelado — notas técnicas

Registro do funcionamento interno e dos resultados medidos, para consulta na
redação do trabalho. Os números vêm de medições sobre o banco de amostras do
próprio projeto; a seção "Limites do que foi medido" explica exatamente o que
eles significam e o que **não** significam.

---

## 1. Arquitetura

O jogo apresenta um objeto 3D e o jogador soletra o nome dele em LIBRAS
diante da câmera. O sistema lê a mão, classifica a letra e preenche a lacuna.

### Rastreamento da mão

| Plataforma | Motor | Versão |
|---|---|---|
| Computador | MediaPipe (Python), processo externo, comunicação por UDP local | 0.10.35 |
| Android | MediaPipe Tasks Vision (biblioteca nativa do Google) | 0.10.35 |

As duas plataformas usam **a mesma versão do MediaPipe e o mesmo modelo**
(`hand_landmarker`), o que mantém o comportamento equivalente. No Android a
biblioteca roda em modo `LIVE_STREAM`: o envio do quadro não bloqueia o jogo e
o resultado é buscado quando fica pronto.

A saída são 21 pontos por mão, com coordenadas normalizadas no quadro e uma
componente de profundidade relativa.

### Normalização das coordenadas

As duas câmeras têm proporções diferentes (4:3 no computador, 9:16 no celular).
Como as coordenadas são normalizadas por largura e altura separadamente, a mesma
mão produz números diferentes em cada plataforma. A conversão multiplica os
eixos horizontal e de profundidade pela razão entre a proporção da câmera e a
proporção do banco, levando ambas a um espaço comum.

---

## 2. Representação do sinal

Cada mão é descrita por três grupos de características, todos invariantes a
posição na tela e a distância da câmera:

**a) Posições (21 pontos × 3 eixos)**
Tomadas em relação ao pulso e divididas pelo tamanho da mão (distância do pulso
à base do dedo médio). Antes disso a palma é endireitada — ver seção 3.

**b) Ângulos (19 valores)**
Quinze ângulos de articulação, três por dedo, medidos entre os segmentos que
se encontram em cada junta; mais quatro ângulos de abertura entre dedos
vizinhos, medidos a partir do pulso. Ângulos não mudam quando a mão gira ou
muda de tamanho.

**c) Contatos (15 distâncias)**
Distâncias entre pontos que costumam se tocar: do polegar a cada ponta de dedo,
entre pontas vizinhas, do polegar à base de cada dedo e de cada ponta à própria
base. Boa parte das letras se distingue exatamente por isso — o que encosta em
quê. Medidas junto com as 21 posições essas diferenças se diluem; medidas à
parte, com peso próprio, elas pesam no resultado.

A distância entre duas mãos é a soma dos três grupos, com pesos 1, 0,25 e 0,5.

---

## 3. Correção do giro da palma

A mão é girada de modo que o eixo pulso → base do dedo médio aponte para cima,
**limitado a 40 graus**.

O limite é essencial. Sem correção nenhuma, inclinar a mão 20 graus derruba o
acerto de 96% para 21%. Com correção ilimitada, letras que se distinguem
justamente pela orientação passam a se confundir: D e G têm quase a mesma
forma, giradas cerca de 90 graus uma da outra. O teto de 40 graus absorve a
inclinação natural de quem sinaliza sem apagar essa diferença.

---

## 4. Classificação

### Letras paradas

Vizinhos mais próximos (kNN) com **k = 5** e voto ponderado: cada vizinho vota
com força 1/(distância + 0,5), de modo que uma amostra bem parecida pesa mais
que outra que só entrou na lista por falta de concorrência.

### Letras com movimento

Sete letras do alfabeto manual exigem movimento: **H, J, K, W, X, Z e Ç**. Elas
são gravadas como sequência de quadros e comparadas por **DTW** (*Dynamic Time
Warping*), que alinha as duas sequências no tempo antes de compará-las — o mesmo
gesto feito mais rápido ou mais devagar ainda corresponde. O custo é dividido
pelo comprimento das sequências, para não penalizar gestos mais longos.

### Critério de aceitação

Duas condições:

1. **Limite de segurança** — a distância precisa ficar abaixo de 12, o que
   apenas descarta uma mão que não se parece com nada do banco.
2. **Vantagem sobre a rival** — a letra vencedora precisa estar a no máximo
   0,95 da distância da segunda letra colocada.

A segunda condição é a que decide, e é **relativa**. Essa foi a correção mais
importante do projeto. O critério anterior era absoluto: a letra só valia se a
distância ficasse abaixo de um número fixo. Bastava a mão inclinar ou a leitura
piorar para *todas* as distâncias subirem juntas e nada mais ser aceito — o
sistema escolhia a letra certa e a recusava. Comparar as duas distâncias entre
si mantém a decisão válida mesmo quando a leitura inteira piora.

---

## 5. Banco de amostras

**557 amostras** ao todo: 515 estáticas em 20 letras e 42 dinâmicas em 7 letras.

Estáticas, por letra:

| | | | | | | | | | |
|---|---|---|---|---|---|---|---|---|---|
| A 30 | B 30 | C 30 | D 20 | E 30 | F 8 | G 30 | I 30 | L 30 | M 30 |
| N 30 | O 30 | P 30 | Q 8 | R 30 | S 30 | T 30 | U 22 | V 30 | Y 7 |

Dinâmicas: H 7, W 7, X 7, Ç 6, J 5, K 5, Z 5.

**F, Q e Y têm cerca de um quarto das amostras das demais**, e são justamente as
três letras com pior desempenho. É uma limitação de dados, não de método.

---

## 6. Resultados medidos

### Método

Validação **deixa-uma-de-fora** (*leave-one-out*): cada amostra é classificada
usando todas as outras. Aplicada em sete cenários, que simulam as condições
reais de uso: mão em pé, inclinada em quatro ângulos diferentes (+10°, −15°,
+20°, −25°) e com ruído de leitura.

### Comparação entre as configurações

Média dos sete cenários:

| Configuração | Acerto | Letra errada |
|---|---|---|
| Anterior (kNN k=3, limiar absoluto, sem correção de giro) | 46,1% | 4,9% |
| Com critério relativo e giro corrigido | 94,0% | 2,1% |
| Acrescentando as distâncias de contato | **94,3%** | **1,4%** |

### Por cenário (configuração final)

| Cenário | Anterior | Final |
|---|---|---|
| Mão em pé | 96,3% | 95,7% |
| Inclinada 10° | 85,0% | 95,9% |
| Inclinada 15° | 60,6% | 95,9% |
| Inclinada 20° | 21,2% | 95,5% |
| Com ruído de leitura | 38,8% | 88,9% |

Com a mão perfeitamente em pé as duas praticamente empatam. Todo o ganho está
em deixar de desabar quando a mão inclina ou a leitura piora — que é a situação
comum de uso.

### Letras com movimento

| Cenário | Anterior | Final |
|---|---|---|
| Mão em pé | 95,2% | 100% |
| Inclinada 15° | 95,2% | 100% |
| Inclinada 20° | 85,7% | 97,6% |

Sem nenhuma letra errada nos três cenários.

### Separação entre pares de letras

Folga média entre cada letra e sua rival mais próxima. Quanto menor, mais
confundível:

| Par | Folga |
|---|---|
| U ↔ R | 0,66 |
| F ↔ T | 0,86 |
| Y ↔ I | 0,96 |
| A ↔ S | 1,38 |
| C ↔ O | 5,44 |
| M ↔ N | 5,80 |

Os pares críticos são U/R, F/T e Y/I — todos distinções de contato entre dedos.
C/O e M/N, ao contrário da impressão comum, estão entre os pares mais bem
separados.

---

## 7. Limites do que foi medido

Registro honesto do alcance dos números acima, para que não sejam
sobre-interpretados:

- **Não é um estudo com usuários.** É validação cruzada sobre o banco de
  amostras existente, com perturbações simuladas. Mede o classificador, não a
  experiência de uso nem o aprendizado de quem joga.
- **As amostras não são independentes entre si.** Parte do banco foi preenchida
  por um mecanismo de aprendizado automático, hoje removido, que gravava uma
  nova amostra a cada acerto durante o jogo. Isso produz amostras muito
  parecidas entre si, o que tende a **superestimar** os valores absolutos. A
  *comparação* entre configurações continua válida, porque todas foram avaliadas
  sobre exatamente os mesmos dados.
- **Amostras coletadas por um único participante**, no computador. O
  desempenho com outras pessoas e com a postura de uso no celular não foi
  medido.
- **Letras com poucas amostras são penalizadas pelo método.** Numa letra com
  sete amostras, deixar uma de fora remove um sétimo dos dados disponíveis, e os
  cinco vizinhos precisam sair das seis restantes. O desempenho real dessas
  letras tende a ser melhor que o medido — mas continuam sendo as mais fracas.

---

## 8. Melhorias identificadas e não implementadas

1. **Regravar F, Q e Y** — as três letras com poucas amostras. É o maior ganho
   disponível, e nenhum ajuste de algoritmo o substitui.
2. **Permitir a gravação de amostras pelo celular.** Hoje o banco só é gravado
   no computador, pelo teclado. Amostras colhidas na postura real de uso —
   segurando o aparelho — representariam melhor a condição em que o jogo é
   usado.
3. **Ícone adaptativo do Android**, em duas camadas, para acompanhar o formato
   de cada aparelho. Atualmente é usado o ícone clássico, que o sistema recorta
   sozinho.
4. **Coleta com mais participantes**, que é o que permitiria afirmar algo sobre
   generalização.
