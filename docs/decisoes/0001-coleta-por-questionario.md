# Coleta automática por questionário

27/10/2026. Pedido de Hugo: implementar primeiro no Coreletter do curso; prospecção comercial fica para etapa posterior. Incremento isolado na branch local `feat/questionnaire-collection`.

## Fluxo

Usuário autenticado responde tema, palavras/expressões de inclusão e exclusão, até três fontes do catálogo, frequência (6/12/24 horas) e limite (1–20 resultados). Ao ativar, o perfil fica pendente no PostgreSQL. Um BackgroundService consulta perfis vencidos a cada 30 segundos enquanto a API está executando. O resultado é privado, com título, trecho curto e link original; não é uma publicação no feed, newsletter enviada ou avaliação de veracidade.

## Política de ingestão implementada

- Somente RSS/Atom XML de URLs HTTPS fixadas na configuração do servidor. Usuário escolhe IDs, nunca envia uma URL para o servidor baixar. Cada fonte precisa de `Approved=true` definido pelo responsável após verificar condições de uso e finalidade. Configuração entregue desativada e catálogo vazio; nenhuma coleta real foi ligada.
- Sem login em sites, cookies, proxies, redirecionamentos, páginas HTML, arquivos de mídia ou reprodução integral de artigos. Não contornar paywall ou CAPTCHA. A inclusão de uma fonte não certifica que ela diz a verdade.
- DNS é resolvido e todos os endereços precisam passar pela política de IP público; o socket usa o endereço já validado, evitando nova resolução entre verificação e conexão. HTTPS mantém a validação de certificado padrão. Base técnica: [Microsoft — ConnectCallback](https://learn.microsoft.com/dotnet/api/system.net.http.socketshttphandler.connectcallback).
- Timeout de 12 segundos por fonte, 512 KiB por resposta, XML sem DTD/entidades externas, no máximo 100 entradas lidas e trechos de até 600 caracteres. Endereços externos e texto são dados não confiáveis, nunca comandos para executar ou instruções para IA.
- Bloqueio de linha `FOR UPDATE SKIP LOCKED` impede processar o mesmo perfil simultaneamente. Transação inclui a coleta limitada a três fontes; isso é uma escolha para a demonstração pequena, não uma fila de produção com alto volume. Não há serviço instalado no Windows nem execução com a API desligada.
- Repetir questionário não libera execução imediata após a primeira coleta: a próxima vence a partir de `LastRunAt`. Pausa persiste. Falha total, parcial e coleta bem-sucedida sem correspondência são estados distintos.
- Histórico preservado; o painel mostra os últimos 100 itens, inclusive de configurações anteriores. Índice único por usuário/URL evita duplicações. Não há limpeza destrutiva automática.

## IA

O fluxo automático funciona sem provedor de IA. Exportação JSON fornece evidências públicas e limites para análise manual nesta conversa ou outra ferramenta. Não há conexão permanente com esta sessão, transmissão automática ao ChatGPT, API key, cobrança ou validação de verdade. Uma integração futura deve implementar adapter separado, consentimento, quota persistente e avaliação de respostas antes de habilitar chamadas.

## Banco existente

Novas tabelas `CollectionProfiles`/`CollectedItems` e índice são criados por SQL aditivo idempotente em Development, depois do EnsureCreated existente. Dados antigos não são apagados. Em outro ambiente, revisar/aplicar `CollectionData.Schema` antes de habilitar a coleta. Isso não converte o projeto para migrações EF; migrações versionadas continuam pendentes para produção.

## Ativar após revisão

Configure uma fonte RSS cuja utilização você tenha verificado, por variáveis de ambiente do processo da API:

```powershell
$env:Collection__Enabled = 'true'
$env:Collection__Sources__0__Id = 'fonte-revisada'
$env:Collection__Sources__0__Name = 'Nome da fonte'
$env:Collection__Sources__0__Url = 'https://dominio-da-fonte.example/feed.xml'
$env:Collection__Sources__0__Language = 'pt'
$env:Collection__Sources__0__Approved = 'true'
```

O domínio acima é ilustrativo, não uma fonte funcional. Reinicie a API, entre na conta e abra **Meu radar automático**. Responda o questionário e ative. Primeira coleta ocorre na próxima passagem do worker; atualizações do painel não disparam coleta. Para parar todas as buscas, defina Collection__Enabled=false e reinicie a API; para pausar apenas uma conta, use o botão no radar.

## Validação

Testes unitários de validação, parsing RSS/Atom, XML malicioso, IPs internos, respostas HTTP, tamanho e correspondência de termos. Testes PostgreSQL opt-in usam base de prefixo `coreletter_test` e esquema aleatório descartável; verificam persistência, duplicações, isolamento dos dados, pausa, falha e lock concorrente. Testes de interface exercitam respostas atrasadas e troca de usuário. Registrar números e limitações reais em HANDOFF.md, sem confundir CI configurada com CI executada.
