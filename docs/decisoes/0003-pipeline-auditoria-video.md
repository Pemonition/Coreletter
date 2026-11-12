# 0003 — Pipeline: triagem, agrupamento, auditoria, roteiro e vídeo

Data: 12/11/2026. Estado: **proposta para discussão** (ver issues de discussão ligadas na PR). Visão didática em `docs/PIPELINE.md`; contrato da auditoria em `docs/contratos/comparacao-v1.md`.

## Contexto
O coletor (0002) já traz notícias de fontes aprovadas. A equipe quer que cada notícia passe por um "agente auditor", vire roteiro e depois vídeo com IA, entregue ao cliente conforme o que ele escolheu (#13). Duas perguntas abertas: **a confiabilidade é avaliada por IA ou por programação?** **O auditor também escreve o roteiro?**

## Decisão

### 1. Confiabilidade: programação primeiro, IA depois
| Camada | O que faz | Por quê |
|---|---|---|
| **Triagem (regras)** | Descarta patrocinado/afiliado (ex.: `tecnoblog.net/achados/`), caça-clique, notícia velha, duplicata | Barato, roda em tudo, testável, explicável na banca |
| **Reputação da fonte (dado)** | Atributos definidos pela equipe em `fontes.json`: tipo de veículo, política de correção, separa opinião | Decisão humana, versionada e revisada; não é palpite de IA |
| **Corroboração (contagem)** | Quantos veículos **independentes** publicaram a mesma história (depois do agrupamento) | Sinal mais forte e objetivo que existe |
| **Auditor (IA)** | Extrai afirmações, separa fato de opinião, aponta concordâncias, divergências e o que só uma fonte diz, **sempre com citação e link** | Tarefa de linguagem, onde IA ajuda; só roda no que passou nas regras (custo) |

**Nunca** há nota de "verdade" ou selo de confiável/falso (ARQUITETURA, B04, benchmark #12 sobre o risco do Ground News).

### 2. Auditor e roteirista são etapas separadas
- **Segurança:** o texto das notícias é dado não confiável. O roteirista **não lê a matéria**; recebe só o JSON validado do auditor. Uma frase maliciosa numa notícia não consegue "instruir" o roteiro.
- **Validação automática entre etapas:** toda citação do auditor precisa existir literalmente no título/resumo da fonte; toda cena do roteiro precisa apontar para afirmações do auditor. Saída da IA que não passa é descartada (anti-alucinação determinística).
- **Testes e troca de provedor:** cada etapa é uma **porta** (interface) com adaptadores, como na aula de arquitetura hexagonal/limpa: `ITriagem`, `IAgrupador`, `IAuditor`, `IRoteirista`, `IGeradorVideo`, `IEntregador`. Primeiro adaptador **simulado** (demo sem chave), depois o real (Gemini, HyperFrames…).

### 3. Etapas se comunicam por estado no banco, não por chamada direta
```
FeedItem: coletado → triado | descartado(motivo)
Historia: agrupada → auditada → roteirizada → video_pronto → publicada
Entrega (por cliente): pendente → enviada | falhou
```
Cada etapa é um job que pega o que está no estado anterior. Falha de um provedor deixa o item parado no estado e ele é retomado na próxima execução; nada se perde. É o mesmo modelo que depois vira pods separados no Kubernetes (seção 6).

### 4. Vídeo com HyperFrames, **por história e não por cliente**
- **HyperFrames** (HeyGen, código aberto, Apache 2.0): composição em HTML/CSS/GSAP renderizada em MP4 com Chrome headless + FFmpeg; roda local, em Docker ou CI; sem taxa por render. Hugo já tem conta e instalação.
- A composição **não recebe variáveis no render**; por isso o adaptador `HyperFramesGeradorVideo` **gera o HTML a partir de um modelo da marca**, com todo texto do roteiro **escapado** (texto vindo de notícia nunca vira HTML/JS), e chama `npx hyperframes render` num contêiner Node.
- **Um vídeo por história**, reaproveitado por todos os clientes interessados no tema. Personalização é **seleção** (quais vídeos entram no resumo de cada cliente), não render por pessoa. Render por pessoa multiplica custo e tempo; o próprio material sobre o HyperFrames indica outras ferramentas para personalização por usuário em escala.
- Legenda sempre (acessibilidade e quem assiste sem som). Narração (voz por IA) é decisão aberta: modelos da HeyGen no plano pago, ElevenLabs ou ferramenta local gratuita.
- Rótulo "gerado por IA a partir de N fontes" + lista de fontes no fim. Narrar a matéria de outro veículo é obra derivada: o roteiro resume afirmações e cita fontes, nunca lê o texto original.

### 5. Regra de rentabilidade (exigida pelo Regulamento)
Vídeo custa (minutos de render, voz, armazenamento). Proposta de RN, a confirmar com a equipe:
- Só vira vídeo a história com **≥ 2 fontes independentes** (corroboração), o que também limita volume.
- Vídeos personalizados no resumo só no plano **Plus**, com **quota mensal**; custo **reservado antes** do render e liberado se falhar (mesma política da B07/`UsageBudget`).
- Números (custo por minuto, preço do plano) são **hipóteses** até medir um render real.

### 6. Kubernetes (dica do professor em 09/11)
Primeiro o "hello world" de cluster à parte (kind/minikube, API simples, dois pods se comunicando), seguindo o PDF do professor. O pipeline acima mapeia naturalmente para objetos do Kubernetes **depois** que cada etapa funcionar no monólito:

| Etapa | Objeto Kubernetes |
|---|---|
| API + front | `Deployment` + `Service` |
| Coletor | `CronJob` (a cada 3 h) |
| Triagem/agrupamento/auditoria/roteiro | `Deployment` de worker (escala com a fila) |
| Render de vídeo (Node + Chrome + FFmpeg) | `Job` por lote ou `Deployment` com limite de CPU |
| PostgreSQL | serviço gerenciado (fora do cluster) |

Começar pelo cluster agora atrasaria o produto (prazo de 3 semanas); a separação por etapas já deixa o código pronto para isso.

### 7. HyperFrames também para documentação e apresentação
O mesmo pipeline de vídeo serve para: vídeo explicando a arquitetura para a equipe (e publicado como artigo da própria plataforma), tutoriais curtos para o usuário e vídeo de abertura da apresentação do TCC. Ver issue de discussão.

## Alternativas consideradas
| Opção | Por que não agora |
|---|---|
| IA decide sozinha se a fonte é confiável | Não explicável, não testável, vira "selo de verdade" |
| Um agente único (audita + escreve + gera) | Injeção de instrução vinda das notícias, difícil testar, custo maior |
| Agentes chamando agentes diretamente | Falha no meio perde trabalho; estado no banco permite retomar |
| Vídeo por cliente | Custo e tempo multiplicados pelo número de clientes |
| Microsserviços + Kubernetes desde já | Prazo; ARQUITETURA pede monólito até gargalo medido |

## Limites conhecidos
- O banco guarda só título e trecho de 300 caracteres (0002). A auditoria v1 compara **manchetes e resumos**, não o texto completo. Ler a matéria inteira exigiria baixar a página (raspagem) e emendar a política 0002: decisão aberta.
- Agrupamento automático erra: histórias diferentes com palavras parecidas podem se juntar, e a mesma história com manchetes muito diferentes pode ficar separada. Limiar ajustável e medido com dados reais.
- Publicação no YouTube (API, termos e rótulo de conteúdo sintético) fica fora do compromisso inicial (ARQUITETURA); para o TCC a entrega é no site e por e-mail.
