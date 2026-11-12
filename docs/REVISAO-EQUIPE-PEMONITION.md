# Base e sugestões para revisão da equipe

Pedido do tech lead: preparar bases mesmo nas áreas de outros integrantes. Estas mudanças foram preparadas na branch Pemonition; não representam contribuição individual dos colegas. Cada pessoa deve revisar, compreender, melhorar e fazer seus próprios commits. Não houve merge na main nem substituição das branches dos colegas.

| Área | Versão básica localizada/preparada | Sugestão completa e responsável |
|---|---|---|
| Radar privado | Questionário, catálogo RSS/Atom controlado, resultados privados, export JSON; preservado em dee2258 | Hugo + Davidy: conciliar com coletor público e migrações existentes, testar PostgreSQL e concorrência |
| Comparação e quotas | Já existem em feat/comparador-simulado e feat/rentabilidade; preservadas sem cópia | Davidy: revisar contrato e concorrência, isolamento e falhas; só depois adapter IA real |
| Interface de histórias | Já existe em feat/tela-historias; preservada sem cópia | Lindomar: melhorar teclado, responsividade, estados e fontes; integrar radar após conciliar contratos |
| Testes | 55 backend e 6 frontend da base local passaram; 3 PostgreSQL pulados | Henrique com Davidy: executar integração real, ampliar autorização e limites, registrar evidências próprias |
| Vídeo | Gerador local demonstrativo e testes em video/ | Hugo integra; Davidy worker; Lindomar prévia; Henrique falhas e regressões. Detalhes em HYPERFRAMES.md |

**Comentário aos responsáveis:** Davidy, Lindomar e Henrique, por favor revisem as versões básicas das suas áreas e melhorem tudo que conseguirem dentro dos critérios do backlog. Usem a versão completa sugerida como direção, abram commits pequenos e expliquem as escolhas e os testes. Registrem dúvidas e limitações na revisão, sem apresentar simulação como integração real.

## Continuidade com Claude Code

Leia AGENTS.md, este documento, HYPERFRAMES.md e o estado Git antes de editar. A implementação está em uma worktree separada `Coreletter-Pemonition`; a pasta original mantém sua branch e os arquivos locais preservados por commit. Não executar reset, clean, force push nem merge automático das branches avançadas. Elas alteram os mesmos contratos e migrações do radar local: a integração precisa de revisão específica. Conferir o diff e a branch imediatamente antes de cada commit.
