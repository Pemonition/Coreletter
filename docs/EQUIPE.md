# Equipe e acordo de trabalho

Coreletter é desenvolvido por Hugo (`Pemonition`, tech lead), Davidy (`ddanjos`, sênior), Lindomar (`LindomarSSJ`, júnior A) e Henrique Fragoso Fagundes (`hikmiau`, iniciante/júnior B). Lindomar tem mais experiência que Henrique; o tamanho e o acompanhamento das tarefas refletem essa diferença. O repositório será público por autorização da equipe; não publicar e-mails pessoais, credenciais ou dados privados.

## Responsabilidades

| Papel | Responsabilidade | Revisão obrigatória |
|---|---|---|
| Tech lead — Hugo | Escopo, contratos, arquitetura, integração, distribuição de tarefas e demonstração | Outro integrante revisa suas mudanças; segurança e custos com o sênior |
| Sênior — Davidy | Comparação com IA, quotas/custos, transações, autenticação e segurança | Tech lead |
| Júnior A — Lindomar | Interface, acessibilidade e integração com a API em fatias pequenas | Tech lead ou sênior |
| Iniciante/júnior B — Henrique | Validações e testes pequenos com roteiro e pareamento com Davidy | Tech lead ou sênior |

Davidy combina com Henrique uma sessão inicial de pareamento para localizar os arquivos, escrever e executar o primeiro teste. Henrique envia um cenário por vez para revisão; só amplia a tarefa depois de compreender o resultado. Lindomar integra o frontend ao contrato existente e consulta Hugo antes de alterar endpoints.

Todos devem executar o projeto, escrever código próprio, testar e explicar sua contribuição. Ninguém fica responsável apenas por documentação. Alternar pequenas tarefas entre interface e API após a primeira entrega.

## Rotina de três semanas

- Semana 1: executar e revisar a base gerada; concluir primeiro fluxo completo; corrigir problemas; definir fontes e critérios da comparação.
- Semana 2: implementar comparação demonstrativa, planos e quotas; integrar e testar permissões e falhas.
- Semana 3: congelar novas funcionalidades, corrigir, registrar evidências, preparar ficha e ensaiar apresentação. Newsletter só entra se o núcleo estiver estável.

Encontro diário de 15 minutos: o que terminou, próxima entrega e impedimento. Tech lead limita cada pessoa a uma tarefa em andamento. Criar PR cedo quando houver dúvida; não esperar dias para pedir revisão.

## Como executar uma tarefa

1. Escolher uma issue com critérios de aceite e confirmar os pré-requisitos.
2. Atualizar a branch principal e abrir branch curta, por exemplo `feat/12-feed-empty-state`.
3. Registrar um cenário de teste; para regras de negócio, seguir vermelho → verde → refatorar.
4. Implementar apenas o escopo da issue. Não alterar contrato, dependência ou arquitetura sem discussão.
5. Rodar verificações relevantes e abrir PR com problema, mudança, testes e evidências. Relatar limitações reais.
6. Corrigir revisão, explicar o fluxo em até cinco minutos e aguardar revisão antes do merge.

Commits e PRs devem refletir contribuições reais. O primeiro commit da base automatizada não comprova autoria individual dos quatro alunos. Cada integrante deve fazer e compreender suas contribuições posteriores.

## IA, qualidade e passagem de contexto

Codex e Claude podem ajudar usando os mesmos documentos do repositório. A troca de ferramenta não transfere tokens, assinatura, sessão ou acesso à API. Nunca colocar chaves, senhas ou dados privados no Git ou em prompts.

Antes de implementar, pedir à IA: plano curto, arquivos envolvidos, critérios de aceite e casos de falha. Depois, ler o diff e executar testes. Se não conseguir explicar o código, a tarefa ainda não terminou.

Ao encerrar uma sessão, registrar na issue/PR: objetivo, branch e último commit, arquivos alterados, comandos executados e resultados, pendências e próximo passo. Não registrar raciocínio interno ou segredos. Atualizar documentação apenas quando um contrato ou procedimento realmente mudar; evitar acumular regras sem utilidade.

## Definição de pronto

- Critérios de aceite cumpridos e revisão de outra pessoa.
- Build e testes relevantes passando; teste de falha/permissão quando aplicável.
- Estados de carregamento, vazio e erro contemplados em mudanças de interface.
- Sem segredo, dependência desnecessária ou dado privado no diff.
- Evidência reproduzível na PR e explicação oral do autor.
- Contrato e instruções atualizados se o comportamento mudou.
