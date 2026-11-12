# De pessoas para pessoas

Nome provisório: Coreletter; Notiffy é um candidato ainda não aprovado.

## Proposta

Uma comunidade de descoberta de notícias, curiosidades e informação em que pessoas selecionam as fontes e cada usuário decide os assuntos que quer acompanhar. A tecnologia organiza e coleta; não deve substituir silenciosamente essa escolha por otimização de engajamento.

Não prometemos ausência de algoritmos: filtros, busca e coleta também são algoritmos. A diferença proposta é controle explícito do usuário e critérios editoriais transparentes, sem feed personalizado para maximizar tempo de tela. Curadoria humana também tem vieses e precisa de revisão e diversidade.

## Estamos cumprindo?

| Princípio | Evidência na branch Pemonition | Lacuna |
|---|---|---|
| Escolher assuntos | Questionário com tema, palavras e exclusões | Tema é descritivo; correspondência usa palavras, não compreensão semântica |
| Escolher fontes | Seleção de 1–3 fontes previamente aprovadas | Catálogo vem vazio; falta processo editorial público e responsáveis |
| Controlar consumo | Frequência de 6/12/24h, limite de 1–20 resultados, pausa | Falta validar experiência com pessoas e explicar por que cada item apareceu |
| Comunidade humana | Publicação manual de links, comentários descritivos, salvamento e denúncia | Não há ainda fila editorial de sugestões e revisão humana de cada notícia |
| Transparência | Fonte, link e limitações; vídeo rotulado como demonstração | Falta política de aprovação, revisão periódica, correções e recurso |
| Comparar perspectivas | Existem branches avançadas de comparação simulada | Não integradas nesta entrega; não certificam verdade nem neutralidade |

Conclusão: aderência parcial. Há controle explícito de fontes e filtros, mas aprovação configurada não comprova curadoria humana realizada. Scraper/RSS não é curador: somente transporta conteúdo. Não dizer que cada notícia passou por humanos quando isso não ocorreu.

## Entrega demonstrável hoje

Base candidata: branch Pemonition, PR #31. Sem merge automático na main.

- Angular: comunidade, salvos, radar privado e moderação conforme permissões; versão visual em revisão com Claude.
- API e PostgreSQL: autenticação, publicações, isolamento de salvos e preferências.
- Radar: coleta controlada desativada por padrão; demonstração sem fonte real deve mostrar claramente o catálogo vazio.
- HyperFrames: composição demonstrativa editável e MP4 local de 26 segundos, sem voz/IA real.
- Evidência: CI 43325623395 aprovada com PostgreSQL descartável e cenários HTTP. Inspeção visual e conciliação das branches avançadas continuam pendentes.

## Roteiro de apresentação — 8 minutos

1. Hugo, 1 min: problema e proposta. Explicar controle do usuário, sem prometer informação neutra ou ausência de algoritmos.
2. Interface, 2 min: cadastrar conta fictícia, publicar link, filtrar e salvar. Mostrar estado vazio/erro se ocorrer, sem esconder falha como dado demonstrativo.
3. Radar, 1 min: tema, palavras, exclusões, fontes, frequência e pausa. Explicar catálogo controlado e coleta desativada.
4. API/testes, 1 min: mostrar CI e exemplo de usuário impedido de excluir conteúdo de terceiro.
5. Vídeo, 1 min: apresentar o MP4 pré-renderizado, como prova de conceito separada do pipeline de notícias.
6. Todos, 2 min: explicar lacunas, próximas tarefas e distinguir contribuição humana, assistência e simulação.

Papéis são sugestões de apresentação, não declaração de autoria individual.

## Divisão com Claude

Claude: branch claude/frontend-visual, frontend visual, hero compacto, contraste, aria-current e nome centralizado. Codex: documentação, roteiro e testes do vídeo. Antes de integrar: conferir status, commits e arquivos em ambas as branches, revisar diff, executar testes e abrir revisão; nunca copiar pasta por cima.

## Próxima entrega completa sugerida

1. Hugo + equipe: catálogo real com justificativa, responsáveis, data de revisão, diversidade e política de correção.
2. Lindomar: mostrar fontes escolhidas e motivo de inclusão; manter ordem previsível e preferência editável.
3. Davidy: conciliar migrations e contratos das branches avançadas, isolamento e idempotência antes de conectar comparação ou renderização.
4. Henrique + Davidy: ampliar testes de limites, autorização e correções com evidência reproduzível.
5. Todos: testar com usuários se conseguem escolher, pausar, mudar fontes e explicar por que receberam um item.

Pedido aos colegas: revisem as bases e ampliem o que puderem, em commits pequenos, registrando entendimento, testes e limites. Não apresentar base gerada como contribuição própria sem revisão e trabalho real.
