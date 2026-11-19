# 0005: Catálogo único e curado de fontes

Data: 13/11/2026. Estado: proposta na PR `feat/catalogo-fontes`. Complementa a 0002 (política de ingestão).

## Contexto

- A proposta do produto é "de pessoas para pessoas, sem algoritmo" (`docs/PROPOSTA.md`). O compromisso 2 diz que as fontes são escolhidas por pessoas. Até aqui isso era só uma lista de endereços, sem registro do porquê.
- O coletor público tinha 7 fontes: 6 de tecnologia e 1 geral, sem ciência, espaço ou curiosidades.
- Na branch `Pemonition`, o radar privado ganhou outro catálogo, com 14 feeds no `appsettings.json` (commit dd01b49).
- Esse catálogo foi verificado a partir de uma rede doméstica. Testado a partir de um data center, **7 dos 14 falharam**: BBC (3 feeds), Ars Technica, Le Monde e DW responderam 403, e Nature redirecionou para uma página de login.
- Pelo PC do Hugo, os mesmos 7 responderam 200, inclusive com a identificação do robô. Portanto, **esses sites bloqueiam endereços de data center**, e o coletor roda em data center (GitHub Actions e Render).
- 11 dos 14 eram em inglês. O agrupamento junta histórias por palavras em comum, então manchete em inglês não forma história com manchete em português.

## Decisão

1. **Um catálogo só:** `backend/RadarPlural.Worker/fontes.json`, usado pelo coletor público e, na conciliação, também pelo radar. A curadoria acontece uma vez, revisada por PR.
2. **Curadoria registrada no próprio arquivo.** Cada fonte declara:
   - `topic`, a área: tecnologia, ia, ciencia, espaco, curiosidades, brasil-e-mundo ou economia;
   - `language`, o idioma em 2 letras;
   - `kind`, o tipo: jornalismo, agencia-publica, institucional, universidade ou divulgacao-cientifica;
   - `reason`, o motivo da escolha, com 20 a 280 caracteres.

   O `SourceCatalog` recusa fonte sem esses campos, e o teste falha. O tipo é contexto para o leitor, não nota de confiabilidade.
3. **Validar em condição de produção:** toda fonte nova é testada com `dotnet run --project backend/RadarPlural.Worker -- --fontes <arquivo> --dry-run` a partir de um data center (CI ou nuvem). Uma verificação feita em casa não basta.
4. **Português primeiro:** toda área tem pelo menos uma fonte em português, e pelo menos 75% do catálogo é em português (teste `TheRealCatalogIsValidAndEveryAreaHasASourceInPortuguese`).
5. **Variedade de linhas editoriais** em "Brasil e mundo": um produto que compara coberturas não pode ter uma curadoria de um só enfoque. A descrição de cada fonte é factual, sem rótulo ideológico.
6. **História = mesma notícia em veículos diferentes:** o agrupamento não liga item com item do mesmo veículo. Com 30 fontes, o Space.com sozinho formou uma "história" de 27 manchetes, por repetir NASA e SpaceX em títulos com Quase Toda Palavra Em Maiúscula.

## Resultado medido (13/11/2026, a partir de data center)

| | Antes (7 fontes) | Catálogo do radar (14) | Este catálogo (30) |
|---|---|---|---|
| Fontes que respondem em produção | 7 | 7 | **30** |
| Em português | 7 | 3 | **26** |
| Áreas | 2 | 5 | **7** |
| Itens lidos por coleta | ~200 | 230 | **687** |
| Histórias com 2+ veículos | ~11 | 3 | **32** |

Exemplos de histórias com 5 a 7 veículos:
- o pacote fiscal no Senado, com enfoques bem diferentes entre os veículos;
- a decisão de Zanin sobre o 2º turno no Rio;
- a PGR e a condenação de Bolsonaro;
- a saída de Jensen Huang da Nvidia.

## Testadas e fora (não contornar)

| Fonte | Motivo |
|---|---|
| g1, Folha, Estadão, Poder360, BBC (inclusive Brasil), DW (inclusive Brasil), Superinteressante, Galileu, Ars Technica, Le Monde, Brasil Escola | 403 para robô ou para endereço de data center |
| Nature, CNN Brasil, Mega Curioso, Jornal da Unicamp | Redireciona para outro domínio (login, painel ou outro site) |
| Hypescience | **Redireciona para `127.0.0.1`**: a proteção contra SSRF bloqueou, como deveria |
| Tecnoblog (tag IA), Agência Senado, Revista Oeste, TecMundo Ciência | Resposta que não é RSS válido ou que tem DTD (a política recusa) |
| Canaltech (IA e Espaço), Ciência Hoje, INPE | Feed existe, mas só traz conteúdo antigo |
| Hypeness, NatGeo Brasil, AEB, Apolo11, Brasil de Fato, Olhar Digital (espaço) | 404 ou 410 |
| Space Today | Feed maior que o limite de 2 MB |

## Consequências

- **Áreas fracas em português:** espaço (1, Socientífica) e curiosidades (1, Aventuras na História). Ficam como pendência da issue #23, com novas fontes validadas do mesmo jeito.
- **O radar da branch `Pemonition` deve ler este arquivo** em vez do próprio catálogo no `appsettings` (passo da conciliação, `docs/FRONTEND-CONCILIACAO.md`). Os campos se mapeiam assim:
  - `Category` → `topic`;
  - `Language` → `language`;
  - `SourceType` → `kind`;
  - `Approved` = estar na lista.
- Com 687 itens a cada 3 horas, a coleta leva cerca de 1 minuto e meio (1 s de espera entre fontes, por educação com os servidores).
