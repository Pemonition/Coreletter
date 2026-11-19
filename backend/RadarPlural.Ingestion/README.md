# RadarPlural.Ingestion: regras da coleta (domínio)

**Camada:** domínio (regras puras) + o download seguro dos feeds. Não conhece banco nem web (`ArchitectureTests` garante).
**Dono proposto:** Hugo.

## O que tem aqui
| Arquivo | Regra |
|---|---|
| `SafeFeedFetcher.cs`, `UrlGuard.cs` | Download com travas: lista de hosts, IP checado na conexão (SSRF), redirecionamento conferido, 2 MB, 15 s |
| `FeedParser.cs` | RSS/Atom → texto puro, sem DTD (XXE), datas em vários formatos |
| `UrlNormalizer.cs` | URL canônica (sem `utm_*`), chave de deduplicação |
| `SourceCatalog.cs` | Valida `fontes.json` (lista de permissão) |
| `Triage.cs` | Descarta oferta/afiliado, resumo do dia e notícia velha; marca caça-clique |
| `StoryClustering.cs` | Agrupa a mesma notícia entre veículos; mantém ids estáveis |

## Por quê
Política em `docs/decisoes/0002-ingestao.md`; triagem e agrupamento em `0003`. Cada regra tem teste com casos reais (`IngestionTests.cs`, `PipelineTests.cs`).

## Como testar
`dotnet test backend/RadarPlural.slnx --filter "FullyQualifiedName~Ingestion|FullyQualifiedName~Triage|FullyQualifiedName~Story"`

## Dívida conhecida
`SafeFeedFetcher` faz HTTP: tecnicamente é adaptador. Separar quando a camada de domínio for extraída (decisão 0004).
