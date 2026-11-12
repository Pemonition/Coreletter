# 0002 — Política de ingestão de notícias (coletor RSS/Atom)

Data: 12/11/2026. Estado: proposta na PR `feat/ingestao-rss`. **A aprovação desta política é a revisão da PR**, como exige o AGENTS.md ("não fazer scraping sem política de ingestão aprovada").

## Contexto
O produto evoluiu de "links compartilhados manualmente" para um agregador que compara a cobertura de vários veículos (issue #13 e benchmark da #12). Para isso o sistema precisa coletar notícias sozinho. O AGENTS.md e a ARQUITETURA exigem, antes de qualquer coleta: fontes definidas, prevenção de SSRF, limites de download, tempo limite e validação de redirecionamentos.

## Decisão

### O que é coletado
- **Só feeds RSS/Atom públicos** que os próprios veículos publicam para leitores e agregadores. Não há raspagem de HTML nesta etapa.
- **Só metadados**: título, link, data e um trecho de até 300 caracteres. O texto completo continua no site de origem, aberto pelo link. Isso respeita o direito autoral e mantém o tráfego nos veículos.
- Conteúdo coletado é **dado não confiável**: vira texto puro (sem HTML, sem scripts), tem tamanho limitado e nunca é tratado como instrução para a IA.

### Quais fontes
- Lista fechada em `backend/RadarPlural.Worker/fontes.json`. Incluir ou remover uma fonte = PR revisada.
- Exigências para entrar: https, domínio (não IP), porta padrão, sem credenciais. Validadas por `SourceCatalog` (com testes).
- **Fonte que bloqueia robôs fica fora.** g1 e Folha respondem 403 a robôs identificados (testado em 12/11/2026); por isso não estão na lista. Não contornar bloqueio (por exemplo, fingindo ser navegador).
- Fonte cadastrada por usuário (issue #13, "Minhas Fontes") **não faz parte desta PR**. Quando entrar, precisa passar pelas mesmas travas e por moderação antes de ser coletada.

### Travas técnicas (todas com testes em `IngestionTests.cs`)
| Risco | Trava |
|---|---|
| SSRF por URL | Só hosts da lista de permissão, http/https, porta padrão, sem `usuario:senha@` (`UrlGuard.IsAllowedTarget`) |
| SSRF por DNS (host da lista apontando para rede interna, metadados de nuvem) | IP verificado **no momento da conexão**, depois do DNS: bloqueia loopback, faixas privadas, link-local, CGNAT, multicast, IPv4 mapeado em IPv6 etc. (`SafeHttp`, `UrlGuard.IsPublicAddress`) |
| Redirecionamento malicioso | Redirecionamento automático desligado; cada salto é conferido contra a lista; máximo 3 |
| Arquivo gigante / bomba de compressão | Teto de 2 MB medido no corpo **já descompactado**, com ou sem `Content-Length` |
| Servidor lento | 15 s de tempo limite total por fonte; 10 min por execução |
| XML malicioso (XXE, "billion laughs") | DTD proibido, sem resolvedor externo |
| HTML/script no conteúdo | Tags e scripts removidos; texto decodificado; tamanho limitado |
| Duplicatas | URL canônica (sem `utm_*`, `fbclid`, fragmento) com índice único e `ON CONFLICT DO NOTHING` |
| Data no futuro fixando notícia no topo | Data mais de 1 h à frente da coleta é descartada |
| Abuso dos servidores das fontes | Execução a cada 3 h, fontes em sequência com 1 s de intervalo, User-Agent que identifica o projeto |

### Onde roda
- Projeto `RadarPlural.Worker` (console) que roda uma vez e termina, agendado pelo **GitHub Actions** (`.github/workflows/ingestao.yml`, a cada 3 h).
- Lógica em `RadarPlural.Ingestion` (biblioteca sem dependência de banco ou web), testável isoladamente.
- A **API continua dona do esquema** (migração `ItensFeed`); o Worker só insere. Leitura pública em `GET /api/news`.

## Alternativas consideradas
| Opção | Por que não agora |
|---|---|
| Raspar HTML das páginas | Quebra quando o site muda o layout, costuma violar termos de uso e exige mais travas. Fica como extensão por fonte, se uma fonte importante não tiver feed |
| `BackgroundService` dentro da API | O Render gratuito hiberna a API após ~15 min sem acesso; a coleta pararia junto |
| Background Worker no Render | Plano pago |
| Microsserviço separado com fila | Contraria "monólito e banco único" (ARQUITETURA) sem gargalo medido; o Worker já é separável depois |
| MongoDB para as notícias | Ver 0001: PostgreSQL atende (índice único, `ON CONFLICT`, `jsonb`/`tsvector` se precisar) |

## Consequências e limites
- O agendamento do GitHub só dispara a partir da `main` e pode atrasar alguns minutos. Em repositório público sem atividade por 60 dias, o GitHub desativa o agendamento.
- O Worker precisa do secret `INGESTAO_DATABASE_URL` (External Database URL do Render + `?sslmode=require`). Sem ele, roda só `--dry-run`.
- Ainda não implementado: agrupar a mesma notícia de veículos diferentes (base da comparação), IA auditora/curadora, fontes de usuário, busca textual. Cada um entra em PR própria.
- O Worker depende da migração `ItensFeed`, que depende da PR #11 (migrações versionadas).
