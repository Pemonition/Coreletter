# Catálogo inicial — revisão humana

Seleção inicial delegada por Hugo e preparada com assistência em 12/11/2026; não representa revisão individual dos colegas nem classificação objetiva das melhores fontes do mundo.

| ID | Fonte e foco | Tipo / limite | Referência oficial |
|---|---|---|---|
| bbc-tech | BBC News, tecnologia; inglês | Jornalismo; contexto editorial próprio | https://support.bbc.co.uk/platform/feeds/NewsFeeds.htm |
| nasa | NASA, espaço e ciência; inglês | Fonte primária institucional, não jornalismo independente sobre a NASA | https://www.nasa.gov/rss-feeds/ |
| mit-ai | MIT News, IA; inglês | Comunicação institucional de pesquisa; não avaliação independente de seus resultados | https://news.mit.edu/rss |
| agencia-brasil | Agência Brasil, Brasil e notícias gerais; português | Agência pública; incluir outras perspectivas na evolução | https://agenciabrasil.ebc.com.br/feed/ |

Os quatro feeds responderam HTTP 200 com RSS válido na checagem técnica desta sessão. Isso não comprova estabilidade futura, autorização ampla de republicação, ausência de viés ou veracidade de cada notícia. Guardar títulos/trechos limitados e links; revisar termos antes de distribuição pública ou uso comercial. Não copiar matérias, imagens ou ultrapassar paywalls.

Nature usa RSS 1.0/RDF, formato que o parser atual suporta por nomes locais dos elementos; foi incluída na ampliação. Guardian fica como candidato, com restrições de uso do feed a revisar. Reuters/AP/Financial Times podem ser candidatos editoriais futuros, sem inventar feed gratuito nem contornar licenciamento. A seleção inicial é pequena, com viés anglófono e institucional conhecido; ampliar regiões e jornalismo independente é uma tarefa editorial, não automática.

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

## Catálogo ampliado por área — 12/11/2026

Área e tipo estão disponíveis como category e sourceType no endpoint de fontes. A interface atual pode ignorar esses campos até a revisão visual.

| Área | Fonte | Idioma | Tipo | Feed oficial |
|---|---|---|---|---|
| Tecnologia | BBC News — Tecnologia | en | jornalismo | https://feeds.bbci.co.uk/news/technology/rss.xml |
| Espaço | NASA — Ciência e espaço (institucional) | en | institucional | https://www.nasa.gov/feed/ |
| Inteligência artificial | MIT News — IA (institucional) | en | institucional | https://news.mit.edu/rss/topic/artificial-intelligence2 |
| Mundo e Brasil | Agência Brasil — Últimas notícias | pt | agência pública | https://agenciabrasil.ebc.com.br/rss/ultimasnoticias/feed.xml |
| Tecnologia | Tecnoblog | pt | jornalismo | https://tecnoblog.net/feed/ |
| Tecnologia | Ars Technica | en | jornalismo | https://feeds.arstechnica.com/arstechnica/index |
| Ciência | ScienceDaily | en | divulgação | https://www.sciencedaily.com/rss/all.xml |
| Ciência | Nature — Pesquisa | en | publicação científica | https://www.nature.com/nature.rss |
| Ciência | BBC — Ciência e ambiente | en | jornalismo | https://feeds.bbci.co.uk/news/science_and_environment/rss.xml |
| Inteligência artificial | Le Monde — IA | en | jornalismo | https://www.lemonde.fr/en/artificial-intelligence/rss_full.xml |
| Mundo | BBC — Mundo | en | jornalismo | https://feeds.bbci.co.uk/news/world/rss.xml |
| Mundo | DW — Mundo | en | jornalismo | https://rss.dw.com/rdf/rss-en-world |
| Ciência | Radioagência — Pesquisa e inovação | pt | agência pública | https://agenciabrasil.ebc.com.br/radioagencia-nacional/rss/pesquisa-e-inovacao/feed.xml |
| Espaço | Space.com | en | jornalismo | https://www.space.com/feeds/all |

Todos os novos feeds incluídos retornaram HTTP 200 e itens RSS/RDF nesta sessão. A checagem técnica não revisa direitos de distribuição pública nem garante disponibilidade futura. ESA foi adiada por falha de validação do certificado nesta máquina; não houve contorno de TLS. O feed candidato MIT Ciência retornou 404 e foi excluído. Veículos de um mesmo grupo não contam como fontes independentes só por terem feeds diferentes. ScienceDaily agrega divulgação científica; Nature contém artigos e conteúdo com acesso limitado. Não transformar resumo de pesquisa em orientação médica ou conclusão científica definitiva.
