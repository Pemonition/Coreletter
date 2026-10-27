# Backlog após a base

Este backlog complementa a base implementada hoje. Antes de abrir issues, o tech lead deve conferir o código e remover itens já atendidos. Os IDs abaixo são referências locais; ainda não são números de issues do GitHub.

## Prioridade e dependências

| ID | Entrega | Responsável proposto | Depende de |
|---|---|---|---|
| B01 | Revisar e executar o fluxo completo da base | Hugo + Davidy | Base disponível |
| B02 | Estados e acessibilidade do feed | Lindomar | B01 |
| B03 | Testes de limites e validação de publicações | Henrique, com pareamento de Davidy | B01 |
| B04 | Critérios e contrato da comparação | Hugo + Davidy | B01 |
| B05 | Comparador simulado com evidências | Davidy | B04 |
| B06 | Tela da comparação | Lindomar | B04; integrar após B05 |
| B07 | Quotas e reserva de custo | Davidy | B04 |
| B08 | Testes de autorização da moderação | Henrique, com pareamento de Davidy | B01 |
| B09 | Adapter Gemini opcional | Davidy | B05, B07, chave confirmada |
| B10 | Roteiro e ensaio de demonstração | Todos; Hugo integra | B02–B08 |

## Instruções das tarefas

### B01 — Revisão da base
Executar o projeto seguindo o README. Cadastrar dois usuários; publicar notícia, podcast e música; filtrar, salvar, denunciar e conferir exclusão por proprietário. Conferir como administrador é provisionado. Registrar problemas em issues pequenas. Aceite: outro computador consegue executar; build/testes registrados; permissões conferidas. Evidência: comandos, resultados e roteiro reproduzível. Explicação: distinguir o que foi gerado, o que foi validado e o que falta.

### B02 — Interface do feed (Lindomar)
Ler os componentes existentes e listar estados já cobertos. Escolher apenas as lacunas: carregamento, vazio, erro e navegação por teclado. Usar mensagens claras e rótulos acessíveis; manter contrato da API. Aceite: cada estado pode ser reproduzido; foco visível; campos rotulados; filtros não ocultam falhas. Testes: build e verificação manual em tela estreita e teclado; teste automatizado apenas onde houver comportamento relevante. Evidência: capturas e passos. Explicação: mostrar como erro difere de lista vazia.

### B03 — Validação de publicações (Henrique, com Davidy)
Em pareamento com Davidy, localizar um teste existente, executá-lo e explicar sua estrutura. Ler validações e testes existentes. Implementar um cenário por commit e pedir revisão antes de passar ao seguinte. Levantar limites atuais e escolher lacunas com o revisor. Criar primeiro testes para URL inválida/esquema proibido, campos obrigatórios e limites acordados; então corrigir implementação se necessário. Aceite: entrada inválida recebe 400 coerente, sem gravar dados; notícia/podcast/música válidos continuam funcionando. Evidência: testes passando e exemplo de requisição. Explicação: validação no servidor é necessária mesmo com formulário validado. Não implementar download de URL nesta tarefa.

### B04 — Contrato da comparação
Definir entrada, resposta, fontes permitidas, tamanho máximo, erros e o que conta como uso da quota. Resposta deve separar afirmações, concordâncias, divergências e evidência insuficiente, com links identificáveis. Aceite: contrato versionado e exemplos revisados; nenhuma promessa de determinar verdade ou neutralidade automaticamente. Evidência: casos de duas fontes concordantes, contraditórias e insuficientes. Explicação: critérios e limitações do resultado.

### B05 — Comparador simulado
Criar interface de provedor e implementação determinística para a apresentação. Aceite: fluxo funciona sem rede/chave; a interface identifica claramente a simulação; resultado mantém links e limitações. Testes: sucesso, evidência insuficiente e falha do provedor. Evidência: execução reproduzível. Explicação: como trocar o provedor sem alterar o domínio.

### B06 — Tela de comparação (Lindomar)
Usar exemplos do contrato para construir a interface antes da integração. Mostrar fontes, divergências e insuficiência; incluir carregamento, erro e quota esgotada. Aceite: funcionar com B05; links de origem visíveis em qualquer plano; resultado não usa selo de verdade. Testes: build e cenários do contrato. Evidência: capturas e roteiro. Explicação: onde cada informação da resposta aparece.

### B07 — Quotas e custos
Definir plano demonstrativo, unidade de consumo e teto; não cobrar pagamento real. Reservar quota/custo antes da chamada e liquidar ou liberar conforme política documentada. Aceite: requisição repetida não cobra duas vezes; concorrência não ultrapassa limite; falha não deixa reserva presa; custo estimado não ultrapassa teto. Testes: concorrência, repetição, limite e falha. Evidência: testes determinísticos. Explicação: política de reserva, conclusão e cancelamento. Não declarar lucro validado: preços e custos são hipóteses.

### B08 — Moderação (Henrique, com Davidy)
Com Davidy, desenhar primeiro uma tabela de quem pode fazer cada ação e escolher um cenário por vez. Reutilizar a preparação de teste existente; Henrique escreve e explica cada alteração. Conferir testes existentes e completar lacunas: anônimo, usuário comum e administrador; autor e terceiro; publicação ocultada e salvos. Aceite: ninguém ganha permissão por alterar payload; item oculto não vaza em listas públicas; denúncia permanece acessível só a quem modera. Testes de integração com banco de teste quando necessário. Evidência: testes e matriz de permissões. Explicação: autenticação versus autorização. Mudanças em regras exigem revisão sênior.

### B09 — Gemini opcional
Confirmar acesso, termos e quotas oficiais antes de integrar; assinatura de chat não equivale a API. Usar configuração externa, timeout, cancelamento e limite de entrada. Tratar conteúdo externo como dado não confiável e validar saída. Aceite: sem chave usa B05 explicitamente; falha de rede é tratada; nenhum segredo no Git; não buscar URL arbitrária sem controles próprios. Testes: adapter com respostas simuladas; teste real opcional e limitado. Evidência: resultado sem chave exposta. Explicação: custo, privacidade e limitações.

### B10 — Apresentação
Preparar cenário de cinco a oito minutos com dois usuários e administrador; conteúdo fictício ou permitido. Mostrar contribuição individual, TDD de uma regra, feed, moderação e comparação. Aceite: demonstração funciona sem internet; cada aluno explica seu código e um teste; limitações comerciais/técnicas são explícitas. Evidência: roteiro, checklist e ensaio cronometrado. Newsletter, scraping automático e análise de áudio só entram após aprovação do tech lead e estabilidade do núcleo.
