# O que falta para a versão de produção

Levantamento de 12/11/2026, feito no código (branch `feat/video-hyperframes`, que contém tudo da #33) e com o sistema rodando de ponta a ponta com dados reais. A gravação está no vídeo "Coreletter funcionando".

**Resumo:** o produto funciona. Hoje ele roda coleta real, triagem, histórias, comparação, planos com quota, comunidade e moderação. O que falta divide-se em três grupos:
- **juntar o código** que está em PR;
- **tirar a demo do plano gratuito**;
- **o que todo produto com usuários reais precisa**: conta, LGPD, pagamento, IA real e monitoramento.

Tamanho: P = até 1 dia, M = 2 a 4 dias, G = 1 semana ou mais.

## Fase 1: demo estável para a banca

| # | Item | Por quê | Tam. | Quem |
|---|---|---|---|---|
| 1 | Aprovar e juntar #33, #30 e #32 na `main` | Hoje a `main` só tem o deploy; triagem, histórias, comparação e planos estão na #33 | P | Davidy aprova, Hugo junta |
| 2 | Segredo `INGESTAO_DATABASE_URL` e rodar "Coletar notícias" | A demo no Render está no ar, mas vazia | P | Hugo |
| 3 | Agendar também a etapa de auditoria | Só a coleta é agendada; sem auditoria, nenhuma comparação fica pronta (só sob demanda, com login) | P | Hugo ou Davidy |
| 4 | Recriar o banco gratuito antes de 26/11 ou migrar para plano pago | O PostgreSQL gratuito do Render expira | P | Hugo |
| 5 | Conciliar o radar (branch `Pemonition`) e o visual novo (`claude/frontend-visual`) | Radar é o compromisso "você escolhe os assuntos"; o visual traz contraste AA e celular | M | Hugo, Davidy e Lindomar |
| 6 | Atualizar a lista de denúncias depois de ocultar | Visto na gravação: a denúncia continua na lista depois da ocultação (ligado à #17) | P | Henrique |

## Fase 2: produção com usuários reais

### Infraestrutura
| # | Item | Hoje | Tam. |
|---|---|---|---|
| 7 | Plano pago com backup automático do banco | Render gratuito: a API hiberna sem acesso e o banco expira | P |
| 8 | Domínio próprio | `coreletter.onrender.com` (o nome do produto ainda pode mudar) | P |
| 9 | Teste de restauração do backup | Nunca feito | P |
| 10 | Teste de carga contra o ambiente real | Feito só local: ~480 req/s, p95 de 32 ms com 10 usuários | P |

### Conta e segurança
| # | Item | Hoje | Tam. |
|---|---|---|---|
| 11 | Recuperar senha e verificar e-mail | Não existe. Cadastro e login só com senha (exige envio de e-mail) | M |
| 12 | Limite de tentativas por conta | Há limite por IP (60/min), não por conta | P |
| 13 | Cabeçalhos de segurança (CSP, HSTS, `X-Content-Type-Options`) e página de erro padrão | Não configurados | P |
| 14 | Revisão de segurança | Já tem proteção de SSRF, de origem (CSRF), cookies `HttpOnly`/`Strict`/`Secure` e senha com hash. Falta revisão externa e varredura de dependências | M |

### Lei e conteúdo
| # | Item | Hoje | Tam. |
|---|---|---|---|
| 15 | **LGPD:** política de privacidade, termos de uso, excluir e exportar a própria conta | Não existe endpoint para isso | M |
| 16 | Conferir os termos de uso de cada fonte RSS | A política guarda só título, link e até 300 caracteres e não contorna bloqueio; falta a checagem fonte por fonte | P |
| 17 | Critérios públicos das fontes (#23) e página "como escolhemos o que você vê" | Parte da proposta (`docs/PROPOSTA.md`) | M |

### Produto
| # | Item | Hoje | Tam. |
|---|---|---|---|
| 18 | **Comparação com IA real** (B09, Gemini) | A simulada só diz "N fontes noticiam o mesmo" e marca pontos de fonte única. O valor do produto depende da comparação real. O contrato e o validador já estão prontos | G |
| 19 | Medir o custo real por comparação | R$ 0,08 é hipótese; a regra de rentabilidade usa esse número | P (depois da 18) |
| 20 | **Pagamento** do plano Plus (gateway com webhook) | Sem cobrança: o admin troca o plano à mão. As regras RN01–RN04 e a quota já existem | G |
| 21 | Fontes de curiosidades e "sugerir fonte" | 6 de 7 fontes são de tecnologia | M |

### Operação
| # | Item | Hoje | Tam. |
|---|---|---|---|
| 22 | Logs estruturados, erros e alertas | Só o log padrão do ASP.NET; há sondas de saúde e prontidão | M |
| 23 | Painel de saúde da coleta (fontes falhando, descartes, histórias por dia) | Os números aparecem só no log do Worker | M |

## O que já está pronto para produção

- Arquitetura limpa vigiada por teste, migrações versionadas e migração única no deploy.
- API sem estado, que escala horizontalmente.
- Fila com `SKIP LOCKED` em várias réplicas, Kubernetes com teto de custo testado e imagens Docker validadas na CI.
- Coleta segura (SSRF, limites, XML sem DTD) e regra de rentabilidade com quota atômica no banco.
- 247 testes .NET (com PostgreSQL real), 38 verificações de API, 14 testes do gerador de vídeo e CI em toda PR.

## Ordem sugerida

1. Fase 1 inteira, para a banca.
2. Depois: 15 (LGPD), 11 (recuperar senha), 7 e 9 (infra paga com backup), 18 e 19 (IA real e custo), 20 (pagamento), 22 (monitoramento).

Sem 15, 11 e 7 não é seguro abrir para usuários reais. Sem 18 e 20, o produto não se paga.
