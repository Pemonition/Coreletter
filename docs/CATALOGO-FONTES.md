# Catálogo inicial — revisão humana

Seleção inicial delegada por Hugo e preparada com assistência em 12/11/2026; não representa revisão individual dos colegas nem classificação objetiva das melhores fontes do mundo.

| ID | Fonte e foco | Tipo / limite | Referência oficial |
|---|---|---|---|
| bbc-tech | BBC News, tecnologia; inglês | Jornalismo; contexto editorial próprio | https://support.bbc.co.uk/platform/feeds/NewsFeeds.htm |
| nasa | NASA, espaço e ciência; inglês | Fonte primária institucional, não jornalismo independente sobre a NASA | https://www.nasa.gov/rss-feeds/ |
| mit-ai | MIT News, IA; inglês | Comunicação institucional de pesquisa; não avaliação independente de seus resultados | https://news.mit.edu/rss |
| agencia-brasil | Agência Brasil, Brasil e notícias gerais; português | Agência pública; incluir outras perspectivas na evolução | https://agenciabrasil.ebc.com.br/feed/ |

Os quatro feeds responderam HTTP 200 com RSS válido na checagem técnica desta sessão. Isso não comprova estabilidade futura, autorização ampla de republicação, ausência de viés ou veracidade de cada notícia. Guardar títulos/trechos limitados e links; revisar termos antes de distribuição pública ou uso comercial. Não copiar matérias, imagens ou ultrapassar paywalls.

Nature também respondeu, mas usa RSS 1.0/RDF; permanece fora do catálogo ativo até validar suporte no parser. Guardian fica como candidato, com restrições de uso do feed a revisar. Reuters/AP/Financial Times podem ser candidatos editoriais futuros, sem inventar feed gratuito nem contornar licenciamento. A seleção inicial é pequena, com viés anglófono e institucional conhecido; ampliar regiões e jornalismo independente é uma tarefa editorial, não automática.

## Controle disponível para a equipe

O catálogo está em `backend/RadarPlural.Api/appsettings.json`, seção `Collection:Sources`. Usuário comum não pode enviar URL arbitrária ao coletor; escolhe até três fontes aprovadas. A equipe controla mudanças pelo repositório e revisão da PR; não foi criado painel administrativo na interface que o Claude está editando.

Na raiz, para desativar uma fonte:

```powershell
pwsh -File scripts/Set-SourceApproval.ps1 -Id nasa -Action disable -Reason 'Revisar cobertura institucional'
```

Para aprovar novamente, use `-Action approve`. Revise o diff, registre a justificativa no commit, peça revisão de outro integrante e reinicie a API. Isso afeta novas coletas; não apaga os itens históricos. Perfis que selecionaram fonte desativada precisarão escolher fontes válidas novamente. O script não acessa a rede nem concede privilégios de administrador.

Novas fontes: adicionar ID único, nome, URL HTTPS oficial, idioma e `Approved=false`; testar o feed com o coletor seguro e registrar foco, tipo de fonte, justificativa e limites nesta tabela antes de aprovar. Nunca trocar URL de uma fonte sem revisão, pois altera a origem confiada.

## Ativação

Catálogo preenchido e fontes inicialmente aprovadas; o interruptor global `Collection:Enabled` continua falso. Para a demonstração local, iniciar a API com `Collection__Enabled=true` no ambiente do processo. Reiniciar após qualquer alteração: esta versão usa opções capturadas ao iniciar. Cada usuário ainda precisa escolher fontes, palavras e ativar seu radar. Pausar é sempre possível.

## Próxima versão sugerida

Painel exclusivo de administrador com lista, proposta/aprovação/desativação, justificativa obrigatória, trilha de quem alterou e data, controle de concorrência e testes 401/403. A aprovação deve ser feita por pessoa autorizada e não por classificação automática de IA. Preservar origem, diversidade e opção de revisão/correção; não chamar esta versão por arquivo de painel já implementado.
