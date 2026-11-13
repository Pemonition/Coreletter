# Arquitetura e limites do MVP

## Objetivo
Coreletter permite compartilhar links de notícias, podcasts e músicas em uma comunidade sobre tecnologia e IA. Usuários consultam um feed, filtram temas, salvam itens e denunciam conteúdo. Comparação textual é uma evolução premium simulada, com evidências e limites explícitos.

## Estrutura
- Angular: interface, formulários e estados de navegação.
- ASP.NET Core / .NET 10 em **arquitetura hexagonal** (decisoes/0003): `Domain` (entidades, regras, portas), `Application` (casos de uso), `Infrastructure` (EF Core/PostgreSQL, hash de senha, migrações) e `Api` (endpoints, sessão, erros). A regra de negócio não conhece HTTP nem banco; é testada com adapters em memória.
- PostgreSQL: usuários, publicações, salvos, denúncias e notícias coletadas; novas entidades de comparação e quota somente após contrato aprovado.
- Worker (console) para coleta RSS/Atom, agendado pelo GitHub Actions ou por CronJob no Kubernetes.
- Provedor de comparação: implementação determinística primeiro; adapter Gemini opcional posteriormente (entra como nova porta no Domain e adapter na Infrastructure).
- Execução: imagem Docker única (Angular servido pela API) no Render (decisoes/0001) e manifestos Kubernetes em `k8s/` (decisoes/0004).

Manter uma aplicação simples, sem microsserviços, filas ou múltiplos agentes em produção por enquanto: um monólito modular, separado por camadas, cabe no prazo e já permite extrair um serviço depois se houver motivo medido. Os agentes de desenvolvimento auxiliam o trabalho da equipe; não são requisito arquitetural do produto.

O contrato concreto da base está em `backend/API.md`. Mudanças devem atualizar contrato, consumidor e testes na mesma PR ou em sequência compatível. O README descreve execução; este documento registra decisões, não substitui o código.

## Fluxos e fronteiras
1. Visitante lê feed público. Usuário autenticado publica, salva e denuncia.
2. API decide permissões pelo usuário autenticado, nunca pelo papel ou ID enviado no corpo.
3. Autor exclui sua publicação; administrador consulta denúncias e oculta conteúdo.
4. Compartilhar URL inicialmente armazena o link e texto informado. Não implica baixar mídia, republicar obra ou analisar áudio.
5. Comparação futura passa por autorização, quota/reserva e provedor; retorna evidências e limitações.

## Decisões
| Decisão | Motivo | Quando rever |
|---|---|---|
| Monólito e banco único | Prazo de três semanas e equipe pequena | Gargalo medido |
| Hexagonal (portas e adapters) em 4 projetos | Regra testável sem banco; menos conflito entre 4 pessoas; visto em aula | Projeto crescer a ponto de pedir Clean Architecture completa |
| Erro esperado como `Result`, exceção só para o inesperado | Fluxo explícito e um único ponto de tradução para HTTP (ProblemDetails) | — |
| Kubernetes por Kustomize, migração em initContainer | Visto em aula; YAML puro; migração uma vez por rollout | Mais de um ambiente remoto (avaliar Helm) |
| Feed com filtros e paginação | Fluxo previsível e demonstrável | Evidência de uso justifica recomendações |
| Compartilhamento manual primeiro | Valida utilidade sem depender de coleta externa | Núcleo estável e fontes permitidas definidas |
| Coleta só de RSS/Atom de fontes aprovadas, por Worker agendado | Política de ingestão (decisoes/0002): feeds públicos, metadados curtos, travas de SSRF e limites | Fonte essencial sem feed, ou fontes de usuário |
| IA simulada primeiro | Demonstração reproduzível sem API | Acesso e limites Gemini confirmados |
| Planos e pagamento simulados | Testar regras sem cobrança real | Validação de mercado e requisitos comerciais |

## Segurança e qualidade
Cookies de autenticação exigem atenção a CSRF, origem/CORS, HTTPS e configuração antes de exposição pública. Rever provisionamento de administrador e persistência de chaves. Limitar tamanho das entradas e requisições; não renderizar HTML externo arbitrário. Nunca registrar senhas, tokens ou chaves de API.

A coleta automática segue `docs/decisoes/0002-ingestao.md` (RSS/Atom de lista aprovada, `RadarPlural.Worker`). Raspagem de HTML ou fonte de usuário continua exigindo fontes e permissões definidas, prevenção de SSRF, limites de download, timeout e validação de redirecionamentos; não faz parte do simples cadastro de links. Conteúdo de fontes e usuários não pode instruir a IA a mudar regras ou expor dados.

TDD é prioritário para regras de negócio e permissões. Casos de uso são testados com adapters em memória (`RadarPlural.Tests/Application`); testes de integração (`RadarPlural.IntegrationTests`, API real + PostgreSQL via Testcontainers) verificam persistência, autorização e isolamento; testes de interface focam comportamento relevante. Testar endpoints apenas em memória não comprova comportamento PostgreSQL: registrar essa limitação quando ocorrer.

## Fora do compromisso inicial
Análise de áudio, integração com plataformas de música, agendamento em redes, pagamentos reais e personalização algorítmica avançada. Newsletter é opcional após concluir o núcleo. Aprovação acadêmica foi relatada por Hugo; a data oficial de entrega ainda precisa ser registrada. A equipe está identificada em `EQUIPE.md`. O repositório público foi autorizado; conteúdo privado não deve ser versionado.
