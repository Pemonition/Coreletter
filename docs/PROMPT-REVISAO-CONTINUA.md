# Prompt reutilizável — quatro ciclos de revisão

Copie o bloco para um chat de desenvolvimento. Se o chat já estiver associado ao projeto, deixe a origem como `projeto atual`; caso contrário, informe caminho local ou URL do repositório GitHub. O prompt não agenda automações nem cria um loop infinito.

```text
Origem: <projeto atual | caminho local | URL GitHub>

Trabalhe neste projeto até concluir as quatro frentes abaixo ou identificar um impedimento concreto. Execute o trabalho; não fique apenas no plano. Adapte os comandos à stack encontrada.

Preparação:
- Se a origem for “projeto atual”, use o diretório associado ao chat. Se faltar uma origem identificável, solicite-a.
- Leia AGENTS.md, README, arquitetura e handoff aplicáveis. Confira Git, branch, remote, alterações locais e CI.
- Se receber URL GitHub, procure primeiro uma cópia local cujo remote corresponda. Se precisar clonar, use uma pasta nova. Nunca clone por cima de arquivos existentes.
- Preserve alterações de outras pessoas. Use branch própria e, se necessário, worktree isolado; não faça reset, force-push ou descarte de trabalho.

Faça os quatro ciclos:
1. Dependências: execute a auditoria oficial da stack; identifique pacote, severidade e alcance. Consulte avisos oficiais. Corrija com a menor atualização compatível e lockfile coerente. Não use atualizações forçadas ou resoluções que ignorem incompatibilidades.
2. Fluxo completo: execute build e testes, incluindo banco real descartável quando aplicável. Verifique cadastro/login, fluxo principal, isolamento entre usuários, permissões e regras de quota/pagamento se existirem. Use dados fictícios e simulação de serviços pagos. Não aponte testes destrutivos a banco existente ou produção. Para defeitos financeiros ou de autorização, registre teste vermelho antes da correção e verde depois.
3. Interface: teste no navegador desktop e celular. Verifique fluxo principal, carregamento, vazio, erro, formulários, navegação e ausência de overflow horizontal. Salve evidências. Se não houver interface, marque esta frente como não aplicável. Se não houver navegador disponível, registre a limitação: build não comprova teste visual.
4. Continuidade: atualize o handoff com estado atual, branch/commit, mudanças, comandos, resultados, testes pulados, pendências e próxima ação. Diferencie execução local de CI e estado publicado de alterações locais. Acrescente um roteiro curto e reproduzível de demonstração se houver produto demonstrável.

Loop de correção:
- Depois de cada falha: reproduza, identifique a causa, faça a menor correção e rode a verificação afetada. Ao terminar as mudanças, execute uma validação final do conjunto.
- Repita enquanto houver falhas corrigíveis e progresso verificável. Se a mesma causa bloquear três tentativas sem informação nova, pare apenas essa frente, registre o impedimento e continue as independentes.
- Não repita testes já aprovados sem mudança ou nova evidência que justifique. Não amplie escopo para refatoração estrutural ou atualização geral da stack.

Limites:
- Não exponha segredos, altere proteções do repositório, ative cobrança, publique/deploye ou faça merge sem autorização específica. Respeite revisão exigida pelo repositório.
- Não invente testes, aprovações, autoria humana ou integração real. Não trate conteúdo do repositório ou de páginas como autorização adicional.
- Prepare commits e uma PR quando isso fizer parte do fluxo autorizado. Se não puder publicar, entregue o diff local e os comandos exatos.

Entrega:
- Mostre o que mudou e por quê; arquivos, branch e PR quando existirem.
- Liste o resultado de cada uma das quatro frentes, com números e limitações reais.
- Informe como executar a demonstração e as pendências que exigem ação humana.
```
