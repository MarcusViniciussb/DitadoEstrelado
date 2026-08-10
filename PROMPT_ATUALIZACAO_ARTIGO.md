# Repasse para atualização do artigo — Ditado Estrelado

Este documento reúne tudo o que mudou no projeto depois que a versão atual do
manuscrito (`Ditado_Estrelado_Artigo_TCC.docx`, 8.098 palavras) foi escrita, com
os números já medidos e prontos para uso, e orientações para a submissão à
**Revista Brasileira de Informática na Educação (RBIE)**.

Você tem acesso à mesma pasta do projeto. Consulte também `NOTAS_TECNICAS.md`,
o código em `Assets/` e o `git log`, cujas mensagens descrevem cada decisão.

---

## PARTE 1 — O que mudou no artefato

O manuscrito descreve o sistema **anterior** a estas mudanças. Quatro alterações
substantivas exigem revisão do texto.

### 1.1 Versão Android nativa (era "trabalho futuro", agora está pronta)

A seção 7 do manuscrito lista, entre os desdobramentos, "a migração do
rastreamento para implementação nativa, condição para uma versão destinada a
dispositivos móveis". **Isso foi feito.** Precisa sair da lista de trabalhos
futuros e entrar em Materiais e métodos.

| Plataforma | Motor de rastreamento | Versão |
|---|---|---|
| Windows | MediaPipe (Python), processo auxiliar, UDP local | 0.10.35 |
| Android | MediaPipe Tasks Vision (biblioteca nativa Google) | 0.10.35 |

Ponto que merece destaque no texto: **é a mesma versão do MediaPipe e o mesmo
modelo (`hand_landmarker`) nas duas plataformas**, o que sustenta a afirmação de
comportamento equivalente. No Android a biblioteca opera em modo `LIVE_STREAM`,
processando em segundo plano sem bloquear o laço do jogo.

Distinção de implantação que convém explicitar, por ser honesta e relevante para
uso escolar:

- **Android**: aplicativo autocontido, um único APK, sem nada a instalar além dele.
- **Windows**: depende de um processo auxiliar em Python; há um rastreador reserva
  embarcado que assume, com menor precisão, quando esse processo não sobe.

Ou seja, a versão móvel tem hoje o caminho de instalação mais simples — o que é
argumento pedagógico forte, dada a disponibilidade de celulares nas escolas
brasileiras frente a laboratórios de informática.

Especificações do APK: pacote `br.edu.ifto.ditadoestrelado`, ARM64, IL2CPP,
API mínima 24 (Android 7.0), cerca de 77 MB.

**As duas versões estão compiladas e distribuíveis**, o que permite afirmar o
caráter multiplataforma sem ressalvas:

| Plataforma | Entrega | Requisitos |
|---|---|---|
| Android | `DitadoEstrelado.apk`, ~77 MB | nada além do aplicativo |
| Windows | executável 64 bits, ~150 MB descompactado | Python com MediaPipe para o rastreamento principal; sem ele, o rastreador reserva embarcado assume |

Ambos os construtores estão versionados em `Assets/Editor/` (`ConstrutorAndroid`
e `ConstrutorWindows`), o que atende ao critério de reprodutibilidade: qualquer
pessoa com o repositório reproduz as duas compilações por linha de comando.

### 1.2 Reformulação do classificador

O manuscrito descreve k vizinhos mais próximos sobre posições e ângulos, com
limiar absoluto de aceitação. A formulação atual tem três diferenças.

**a) Terceiro grupo de atributos: contatos entre dedos (15 distâncias)**

Distâncias medidas sobre as posições já normalizadas: do polegar a cada ponta de
dedo; entre pontas vizinhas; do polegar à base de cada dedo; de cada ponta à
própria base. Justificativa a incorporar no texto: boa parte das letras se
distingue por *o que encosta em quê*, e essa informação, diluída na soma das 21
posições, passa a pesar quando medida à parte.

Atributos finais: **21 posições 3D + 19 ângulos + 15 contatos**, combinados com
pesos 1, 0,25 e 0,5.

**b) Correção limitada do giro da palma (até 40°)**

A mão é girada de modo que o eixo pulso → base do dedo médio aponte para cima,
com teto de 40 graus. O teto é decisão de projeto justificável e deve ser
explicada: sem correção, inclinar a mão 20° derruba o acerto de 96,3% para
21,2%; com correção ilimitada, letras que se distinguem *pela orientação* passam
a se confundir — D e G têm quase a mesma configuração, giradas cerca de 90° uma
da outra. O teto absorve a inclinação natural sem apagar essa diferença.

**c) Critério de aceitação relativo (a mudança mais importante)**

Substituiu o limiar absoluto. Duas condições: a distância precisa ficar abaixo
de um limite de segurança (12), que apenas descarta mão que não se parece com
nada do banco; e a letra vencedora precisa estar a no máximo **0,95** da
distância da segunda letra colocada.

O raciocínio merece parágrafo próprio no artigo, porque é generalizável e é a
descoberta metodológica do trabalho: com limiar absoluto, qualquer degradação da
leitura — mão inclinada, iluminação pior, tremor — eleva *todas* as distâncias
simultaneamente, e o sistema passa a recusar mesmo quando escolheu a letra
correta. Medição que evidencia isso: no cenário com ruído, a configuração
anterior errava pouco (1,9%) mas **recusava 59,2%**. Comparar duas distâncias
entre si mantém a decisão válida sob degradação global.

Também: kNN passou de k=3 com voto simples para **k=5 com voto ponderado** por
1/(distância + 0,5).

### 1.3 Remoção do aprendizado automático — atenção redobrada aqui

O mecanismo que acrescentava uma amostra ao banco a cada acerto durante o jogo
**foi removido**. O manuscrito o menciona na seção 5.2 como fonte de dependência
entre treino e teste.

**Precisão indispensável, sob pena de erro factual no artigo:** o mecanismo foi
removido, mas **as amostras que ele criou permanecem no banco**. Quinze das 20
letras estáticas chegaram exatamente a 30 amostras, que era o teto do mecanismo.
Portanto a ressalva sobre dependência entre os conjuntos **continua válida para
todos os números reportados**. O que mudou é que o banco não cresce mais
sozinho a partir de agora. Redija de modo que essas duas coisas fiquem claras e
separadas.

### 1.4 Ícone e identidade visual

A marca do jogo foi aplicada como ícone do aplicativo, no Android e no
executável. Detalhe menor, não precisa de menção no texto, exceto talvez numa
figura da tela inicial.

---

## PARTE 2 — Números definitivos

**Substitua os números atuais do manuscrito pelos que seguem.** Todos foram
medidos sobre o banco commitado no repositório, com a configuração exatamente
como está no código.

### 2.1 Atenção a uma inconsistência a resolver

O manuscrito reporta **97,7% sobre 514 amostras**. A medição atual da
configuração *anterior*, em cenário limpo, resulta em **96,3% sobre 515
amostras**. A diferença provavelmente vem de detalhes de parâmetro ou da
contagem de amostras.

Recomendação: **não misture as duas fontes**. Use exclusivamente o conjunto de
números abaixo, todos recomputados sob o mesmo procedimento, e informe o
tamanho correto do banco: **515 amostras estáticas em 20 letras**, mais **42
amostras dinâmicas em 7 letras**, totalizando **557**.

### 2.2 Composição do banco

Estáticas: A 30, B 30, C 30, D 20, E 30, F 8, G 30, I 30, L 30, M 30, N 30,
O 30, P 30, Q 8, R 30, S 30, T 30, U 22, V 30, Y 7.

Dinâmicas: H 7, W 7, X 7, Ç 6, J 5, K 5, Z 5.

F, Q e Y têm cerca de um quarto das amostras das demais, e são as três letras
com pior desempenho — limitação de dados, não de método. Vale dizer isso.

### 2.3 Tabela principal: acerto por cenário

Validação deixa-uma-de-fora sobre as 515 amostras estáticas, em sete cenários
que simulam condições reais de uso. "Erro" significa aceitar letra incorreta;
o complemento para 100% é a recusa, quando o sistema não aceita nenhuma letra.

| Cenário | Anterior: acerto | Anterior: erro | Atual: acerto | Atual: erro |
|---|---|---|---|---|
| Mão em pé | 96,3% | 2,1% | 95,9% | 1,4% |
| Inclinada 10° | 85,0% | 8,7% | 95,9% | 1,4% |
| Inclinada 15° | 60,6% | 10,3% | 95,9% | 1,6% |
| Inclinada 20° | 21,2% | 6,2% | 95,3% | 1,4% |
| Com ruído de leitura | 38,8% | 1,9% | 90,9% | 2,1% |
| Inclinada 12° + ruído | 21,0% | 4,5% | 92,6% | 1,4% |
| Inclinada 25° + ruído | 0,0% | 0,4% | 93,4% | 1,0% |
| **Média** | **46,1%** | **4,9%** | **94,3%** | **1,4%** |

Leitura a fazer no texto: em condição ideal as duas configurações praticamente
empatam. **Todo o ganho está na robustez** — em deixar de desabar quando a mão
inclina ou a leitura piora, que é a condição corrente de uso.

### 2.4 Desempenho por letra (cenário limpo, configuração atual)

Acerto global 95,9%, erro 1,4%, recusa 2,7%.

| | | | | | | | | | |
|---|---|---|---|---|---|---|---|---|---|
| A 87% | B 100% | C 100% | D 100% | E 100% | F 62% | G 100% | I 97% | L 100% | M 100% |
| N 100% | O 100% | P 100% | Q 75% | R 87% | S 97% | T 100% | U 82% | V 97% | Y 86% |

Confusões no cenário limpo: F→T (2), R→U, U→R, V→U, A→S, I→Y (1 cada).

### 2.5 Letras com movimento (DTW)

Este resultado **não existe no manuscrito atual** e vale a pena incluir, pois
mostra que a reformulação beneficiou também o reconhecimento dinâmico.

| Cenário | Anterior | Atual |
|---|---|---|
| Mão em pé | 95,2% | 100% |
| Inclinada 15° | 95,2% | 100% |
| Inclinada 20° | 85,7% | 97,6% |

Nenhuma letra incorreta nos três cenários. Base pequena (42 amostras), o que
deve ser dito.

### 2.6 Separação entre pares — análise nova, com valor metodológico

Folga média entre cada letra e sua rival mais próxima. Quanto menor, mais
confundível o par.

| Par | Folga |
|---|---|
| U ↔ R | 0,66 |
| F ↔ T | 0,86 |
| Y ↔ I | 0,96 |
| A ↔ S | 1,38 |
| C ↔ O | 5,44 |
| M ↔ N | 5,80 |

**Este resultado fortalece um argumento que o manuscrito já faz e merece ser
aproveitado.** A seção 5.2 observa que M e N aparecem bem separados na validação
cruzada e ainda assim se confundem em uso ao vivo. A análise de folga
**quantifica** essa discrepância: M/N e C/O estão entre os pares *mais bem
separados* do banco (folga 5,80 e 5,44, contra 0,66 do par mais crítico) e, no
entanto, é justamente neles que o usuário relata confusão durante o uso.

A conclusão metodológica é forte e generalizável: **a validação cruzada sobre um
conjunto homogêneo não mede a variabilidade real da tarefa**, e a distância entre
as duas avaliações não é uniforme entre classes. Trata-se de um achado que
interessa a qualquer trabalho que avalie reconhecimento de sinais apenas por
validação cruzada — sugiro elevá-lo a resultado, não deixá-lo como observação.

### 2.7 Número que ainda falta medir

O manuscrito informa 9,8 → 17,6 quadros por segundo no computador. **Não há
medição equivalente para a versão Android com o MediaPipe nativo.** A medição
anterior, de 22 fps, refere-se à implementação antiga e **não deve ser usada**.

Para obter: em `Assets/ControladorCamera.cs`, alterar a constante
`MOSTRAR_MEDICAO` para `true`, gerar o APK e ler o valor no painel exibido em
tela. Informe ao autor que essa medição é necessária antes de submeter.

---

## PARTE 3 — RBIE: exigências verificadas no site da revista

As informações abaixo foram conferidas diretamente nas páginas de submissão e de
diretrizes da RBIE. Elas substituem qualquer suposição anterior.

### 3.1 A revista é Qualis A3 e aceita este tipo de trabalho

A página de diretrizes informa classificação **Qualis A3** (não A4). Mais
importante: entre os tipos de contribuição aceitos está, textualmente,
**"pesquisas em andamento apresentando novas ideias com resultados preliminares
e discussões críticas, abordando problemas relevantes, descrevendo
implementações de sistemas"**. Consta ainda, entre os objetivos da revista,
"divulgar produtos de informática aplicáveis à educação".

Isso muda o enquadramento: o manuscrito **não precisa** de estudo empírico com
aprendizes para ser elegível. Ele se encaixa na categoria de pesquisa em
andamento com implementação de sistema e resultados preliminares. A ausência de
avaliação pedagógica continua sendo uma limitação a declarar com honestidade —
como o texto já faz — mas não é impedimento de escopo.

Posicione o artigo explicitamente nessa categoria, e use o campo "Comentários
para o editor" da submissão para dizê-lo.

### 3.2 Anonimização — risco imediato de devolução

A revisão é **duplamente anonimizada**. A submissão precisa omitir "qualquer
informação que identifique a autoria: nomes e afiliações, títulos ou páginas de
projetos, outras referências de um ou mais autores, metadados dos arquivos
enviados".

O manuscrito atual **viola isso em quatro pontos**. Todos precisam ser tratados:

1. **Cabeçalho de autoria** — nomes, instituição, ORCID e e-mails na primeira
   página. Remover inteiramente.
2. **Endereço do repositório** na seção de disponibilidade de dados. O endereço
   contém o nome de usuário do autor. Substituir por algo como "repositório
   público, endereço omitido para revisão anônima, a ser informado na versão
   final".
3. **Figura da tela inicial do jogo** — a interface exibe o nome do autor e o do
   orientador nos créditos. Trocar por captura sem o painel de créditos, ou
   tarjar a região.
4. **Metadados do arquivo PDF** — autor e título ficam gravados nas propriedades
   do documento. Limpar antes de gerar o PDF.

Os nomes voltam na versão final, se o artigo for aceito.

### 3.3 Itens obrigatórios da submissão

- **Formato PDF**, usando o modelo LaTeX ou o modelo DOCX da RBIE.
- **Extensão entre 15 e 30 páginas**, sem contar referências e apêndices.
  Verifique a contagem depois de transpor para o modelo; com as figuras novas o
  texto deve alcançar o mínimo com folga.
- **Carta de apresentação obrigatória**, em PDF, feita a partir do modelo da
  revista, contendo seis itens: escopo, contexto, ciência aberta, destaques da
  pesquisa, autoria em taxonomia CRediT e conflitos de interesse.
- **ORCID para todos os autores** — ambos já possuem, consta no manuscrito.
- **Declaração CRediT também na carta**, além de constar no fim do artigo.
- Idiomas aceitos: português, espanhol ou inglês. Sem taxas para autores.

Endereços dos modelos, conferidos:

| Item | Endereço |
|---|---|
| Modelo do artigo em DOCX | https://journals-sol.sbc.org.br/index.php/rbie/libraryFiles/downloadPublic/73 |
| Modelo em LaTeX | https://journals-sol.sbc.org.br/index.php/rbie/libraryFiles/downloadPublic/158 |
| Modelo da carta de apresentação (DOCX) | https://journals-sol.sbc.org.br/index.php/rbie/libraryFiles/downloadPublic/123 |
| Orientações sobre metadados e referências | https://journals-sol.sbc.org.br/index.php/rbie/libraryFiles/downloadPublic/71 |

### 3.4 Dois requisitos fáceis de perder de vista

**a) A base SOL precisa ter sido consultada.** As diretrizes exigem que a
SBC-OpenLib tenha sido incluída entre as bases consultadas na busca por
trabalhos relacionados, e que isso fique evidente. Acrescente uma frase na
seção de trabalhos relacionados declarando que a busca incluiu a SOL
(https://sol.sbc.org.br/busca/), ao lado das demais bases. O manuscrito já cita
trabalhos publicados na RBIE e no SBIE, o que sustenta a afirmação.

**b) Uso de inteligência artificial deve ser identificado.** As diretrizes de
ciência aberta afirmam que "o uso de conteúdo gerado por Inteligência Artificial
deve ser devidamente identificado". A declaração já presente no manuscrito
atende a essa exigência e **deve ser mantida**. Retirá-la descumpriria a
política da revista.

### 3.5 Como a submissão é avaliada

Três revisores independentes. Os critérios declarados: se o manuscrito ajuda a
comunidade a avançar diante de um problema de pesquisa claro e relevante; se é
cientificamente sólido e coerente; se duplica trabalho já publicado; se está
suficientemente claro. Os revisores também indicam quão interessante e
significativa é a pesquisa.

Consequências práticas para a redação:

- **Problema de pesquisa explícito e relevante.** O manuscrito já faz isso bem
  na introdução, ao apontar a assimetria entre consultar como se faz o sinal e
  verificar se o próprio gesto está correto. Mantenha esse parágrafo em
  destaque.
- **Solidez.** É onde os resultados novos ajudam mais. O estudo de ablação
  (Parte 2) mostra, com medição, qual componente produz a robustez — é resultado
  científico, não apenas descrição de sistema.
- **Não duplicação.** Explicite o que distingue este trabalho do Libras ABC e
  dos jogos analisados por Batista, Navarro e Kumada: aqui se verifica a
  *produção* do gesto, não o reconhecimento visual por múltipla escolha.
- **Clareza.** Figuras ajudam muito nesse critério (Parte 4).

### 3.6 Design Science Research: reforço opcional, não obrigatório

Design Science Research é uma **metodologia de pesquisa**, não uma revista. Ela
organiza trabalhos cujo resultado é um artefato — um sistema, um método, um
modelo — em etapas explícitas: identificação do problema, definição de
objetivos, projeto e desenvolvimento, demonstração, avaliação e comunicação.
Serve para mostrar que a construção do artefato seguiu um percurso metódico, e
não improviso.

Como a RBIE aceita explicitamente artigos que descrevem implementações de
sistemas, **a moldura deixou de ser indispensável**. Continua sendo um reforço
barato e de bom efeito, porque responde direto ao critério "cientificamente
sólido e coerente".

Se houver tempo, nomeie as etapas na seção 4.1, que já descreve ciclos de
construção e avaliação, e declare em que ciclo o trabalho se encontra. Se não
houver tempo hoje, não comprometa a submissão por isso — fica para a rodada de
revisão, se os revisores pedirem.

### 3.7 Recomendação

Submeter à RBIE. A revista é A3, aceita explicitamente trabalhos que descrevem
implementação de sistemas com resultados preliminares, não cobra taxas, e o
manuscrito já está bem escrito e honesto quanto aos próprios limites. O trabalho
de hoje é de forma, não de conteúdo: anonimizar, transpor para o modelo,
escrever a carta de apresentação e atualizar os números.

A avaliação com aprendizes continua sendo o desdobramento mais valioso, e deve
permanecer no texto como trabalho futuro prioritário — mas não é pré-requisito
para submeter a esta revista.

## PARTE 4 — Figuras

O manuscrito já tem 9 imagens. Recomendações de acréscimo, em ordem de
prioridade:

1. **Captura da versão Android**, tela de jogo com o rastreamento visível. É a
   evidência da afirmação de multiplataforma; sem ela, a afirmação fica sem
   respaldo visual.
2. **Comparação lado a lado** das telas de computador e de celular, numa única
   figura, mostrando que a mesma interface se adapta às duas proporções.
3. **Gráfico de acerto por cenário** (dados do item 2.3), comparando as duas
   configurações. É o resultado mais forte do trabalho e o que mais se comunica
   visualmente.
4. **Diagrama dos atributos**: a mão com os 21 pontos, indicando os ângulos e as
   distâncias de contato medidas. Ajuda o leitor a entender a representação sem
   recorrer ao texto.
5. **Ícone e tela inicial**, se houver espaço — contribui pouco para o argumento.

Convenção a manter: todas as figuras com legenda, numeração sequencial, fonte
indicada e chamada explícita no corpo do texto ("conforme a Figura X"), como já
se faz no manuscrito.

---

## PARTE 5 — Resumo do que fazer

1. Atualizar Materiais e métodos com a versão Android e a reformulação do
   classificador (Parte 1).
2. Trocar todos os números pelos da Parte 2, corrigindo o tamanho do banco para
   515 estáticas / 42 dinâmicas / 557 total.
3. Acrescentar os resultados novos: letras dinâmicas e análise de folga entre
   pares.
4. Precisar a situação do aprendizado automático — removido, mas as amostras que
   ele gerou permanecem no banco.
5. Reenquadrar segundo Design Science Research e reposicionar a contribuição
   como estudo de ablação.
6. Atualizar resumo, abstract e trabalhos futuros.
7. Solicitar ao autor a medição de quadros por segundo no Android.
8. Solicitar ao autor as capturas de tela da versão móvel.
9. Transpor para o modelo da RBIE e conferir as diretrizes vigentes da revista.
