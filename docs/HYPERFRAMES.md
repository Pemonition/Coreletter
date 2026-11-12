# HyperFrames no Coreletter

Integração local demonstrativa: roteiro JSON → composição HTML → prévia/renderização HyperFrames. Não chama serviços pagos, não usa a conta aberta, não publica vídeo nem conecta IA real. O roteiro do exemplo é didático e autoral, não notícia auditada.

## Executar

Na pasta `video`, use Node 22 ou superior:

```powershell
npm ci --ignore-scripts
npm test
npm run demo
npm run check
npm run preview
npm run render
```

A composição fica em `video/output/index.html`. A geração recusa sobrescrever arquivo existente: para preservar edições do aplicativo, use `node generate.mjs demo.json output-v2` e passe essa pasta ao CLI. `render` depende de Chrome/FFmpeg e gera MP4 local; conta ou assinatura não provam que esse caminho foi validado. CLI fixado no lockfile. Nenhuma instalação de plugins de Claude/Codex é necessária para gerar a composição.

## Contrato demonstrativo

`version=1`, `simulated=true`, título até 100 caracteres, de 1 a 6 cenas com 3–15 segundos, título até 100 e corpo até 400 caracteres, até 3 referências HTTPS sem credenciais por cena. Texto é escapado; não há download de notícias, scripts ou mídia externa. O gerador não é ainda o adaptador do contrato de comparação: não aceita JSON do auditor diretamente e não comprova evidências. Texto só pode entrar após revisão humana.

A tela HTML tem 1920×1080, fontes e composição locais e aviso de demonstração. Não inclui voz, música ou imagens de terceiros. O HyperFrames pode editar a composição gerada; salvar em pasta separada protege o original.

## Evolução completa sugerida

1. Davidy + Hugo: aprovar contrato RoteiroV1 derivado exclusivamente de comparação validada; vincular cada cena a claims e citações. Testar referências inexistentes, divergência e evidência insuficiente antes de ligar ao gerador.
2. Davidy: implementar porta de renderização em worker, executar sem shell com argumentos fixos, limites de CPU/tempo e pasta isolada por job; armazenar hash do roteiro e versão do template. Repetição idempotente e falha devem liberar reservas sem cobrança dupla.
3. Lindomar: tela de prévia, progresso, erro/repetição e fontes; rótulo explícito de simulação; teclado e celular. Não exibir vídeo de outro usuário.
4. Henrique, com revisão: testes de roteiro inválido, HTML malicioso, limites, arquivo existente, falha de render e isolamento. Ampliar cenários explicando cada resultado.
5. Hugo: revisar evidências e direitos de mídia, integrar armazenamento e entrega autenticada. Publicação e serviços pagos dependem de implementação e decisão específicas.

Referência oficial: https://github.com/heygen-com/hyperframes .

## Validação desta sessão

3 testes do gerador aprovados. HyperFrames 0.9.40: check com 4 amostras, runtime/layout/contraste aprovados (17 verificações de contraste), craft desativado nessa rodada. Quatro avisos pedem separar as cenas em subcomposições; permanecem como melhoria editorial. Prévia iniciada em http://localhost:4367/#project/output-final. Render MP4 em andamento ao registrar esta etapa; não considerar concluído sem conferir o resultado. O primeiro check encontrou ausência de timeline: corrigida com GSAP local fixado no lockfile.

No Windows, `pwsh -File Render-Local.ps1` usa FFmpeg/FFprobe já incluídos no HyperFrames desktop quando disponíveis, sem alterar o PATH permanente. Caso ausentes, informa a dependência e não instala nada.
