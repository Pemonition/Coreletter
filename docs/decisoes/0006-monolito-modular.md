# 0006: Monólito modular — uma solução, módulos de negócio em projetos

Data: 17/11/2026. Estado: proposta do Davidy no grupo da equipe, aceita pelo Hugo no mesmo dia; aguarda ciência de Lindomar e Henrique nesta PR. Complementa a 0004 (escala desde o MVP) e substitui a linha "Monólito e banco único" de `docs/ARQUITETURA.md`.

## Contexto

- Antes da próxima fase (Docker, Kubernetes, IA com RAG), o grupo perguntou se o Coreletter deveria virar microsserviços. A resposta do Davidy: seguir com monólito, mas com os principais módulos separados em projetos dentro da solução.
- Isso já é quase o estado atual. A solução `backend/RadarPlural.slnx` tem 7 projetos (`Api`, `Domain`, `Pipeline`, `Infrastructure`, `Ingestion`, `Worker`, `Tests`) e um banco só, o PostgreSQL. No Kubernetes, o mesmo código já roda em **processos separados**:
  - API: Deployment com 2 a 5 réplicas (HPA);
  - coletor: CronJob a cada 3 h;
  - auditoria: Deployment com 0 réplicas até haver auditor real.
- Os projetos estão separados por **camada técnica** (domínio, aplicação, infraestrutura). O `ArchitectureTests` já garante a direção das dependências entre camadas. Nenhuma regra separa os **assuntos de negócio** entre si.
- O ponto que destoa é o projeto `RadarPlural.Api`:
  - o `Program.cs` (405 linhas) tem **todos** os endpoints, e boa parte deles consulta o banco direto, sem passar por caso de uso: conta, publicações, moderação, notícias e histórias;
  - o `Domain.cs` tem o `AppDb` com todas as entidades. A API é a dona do esquema inteiro, e as migrações moram nela.

## Decisão

1. **Monólito modular.** Uma solução, um processo de build, um banco. O código é organizado em **módulos de negócio** com fronteira explícita. As camadas (domínio, aplicação, infraestrutura) continuam valendo **dentro** de cada módulo.

2. **Os módulos:**

   | Módulo | Responsabilidade | Tabelas de que é dono | Onde está o código hoje |
   |---|---|---|---|
   | Comunidade | Conta, sessão, publicações, salvos, denúncias, moderação | `Members`, `Posts`, `Saved`, `Reports` | `Api/Program.cs` e `Api/Domain.cs` (**não modularizado**) |
   | Radar | Coleta RSS, triagem, agrupamento em histórias, leitura de notícias e histórias | `FeedItems`, `Stories` | `Ingestion`, `Pipeline`, `Infrastructure`, `Worker`; leitura HTTP no `Program.cs` |
   | Comparação | Auditor, validação do contrato v1, cache de comparações | `StoryAudits` | `Pipeline/Comparison`, `Infrastructure` |
   | Planos e quota | Planos, rentabilidade (RN01–RN04), reserva de uso | `UsageMonths`, `UsageReservations` | `Domain/Billing`, `Infrastructure/PgUsageLedger` |

3. **Regra entre módulos.** Um módulo usa outro **por interface** (porta ou caso de uso), nunca pela tabela do outro. Exemplo que já segue a regra: a Comparação reserva quota chamando `IUsageLedger`, sem tocar em `UsageReservations`.

4. **Escala por processo, não por serviço.** O mesmo código gera duas imagens (API e Worker) e vários Deployments, cada um escalado separadamente. Isso entrega o principal ganho de microsserviços (escalar só a parte que precisa) sem rede entre serviços.

5. **Esquema único, com dono único das migrações.** Um `DbContext` e uma pasta de migrações. A divisão por módulo é de **posse** de tabela, não de banco.

6. **Quando extrair um módulo para um serviço próprio.** Só com motivo medido e registrado numa nova decisão:
   - gargalo medido que réplicas não resolvem;
   - ritmo de deploy muito diferente do resto;
   - isolamento exigido, por exemplo chave de IA paga ou custo que precisa de teto próprio;
   - pessoa ou time dedicado ao módulo.

   O candidato mais provável é o auditor com IA real (B09). Como ele já é uma porta (`IAuditor`) e roda como etapa separada no Worker, extrair seria uma mudança localizada.

7. **RAG entra pelo mesmo desenho.** Uma porta nova no `Pipeline` para buscar contexto relevante, implementada na `Infrastructure` com pgvector **no mesmo PostgreSQL**. Não precisa de serviço novo nem de banco vetorial separado.

## Alternativas consideradas

| Alternativa | Por que não agora |
|---|---|
| Microsserviços (um serviço e um banco por módulo) | A reserva de quota precisa ser atômica (checar e descontar no mesmo comando). Com bancos separados, viraria saga ou compensação distribuída. Multiplica deploys, CI, contratos entre serviços e observabilidade, para 4 pessoas e 9 dias até a entrega |
| Manter tudo no `Program.cs` | Quatro pessoas editando o mesmo arquivo gera conflito a cada PR. Regra de negócio dentro de endpoint só se testa subindo HTTP e banco |
| Só camadas técnicas, sem módulos de negócio | Funciona hoje, mas nada impede a Comunidade de ler tabela do Radar, ou o contrário. O acoplamento cresce sem ninguém perceber |
| Um banco por módulo dentro do monólito | Mesma perda de transação atômica, sem o ganho de deploy independente |

## Consequências

- **Até a entrega (~26/11): nenhuma mudança estrutural.** Henrique (#35) e Davidy (D1, quota) mexem nos endpoints do `Program.cs` esta semana. Mover arquivos agora geraria conflito nas PRs deles.
- **Depois da banca, nesta ordem:**
  1. Extrair a Comunidade do `Program.cs` e do `Domain.cs` para casos de uso e entidades próprios, com endpoints agrupados por módulo (`MapGroup`).
  2. Fazer as leituras de notícias e histórias passarem por uma porta do Radar, em vez de consultar o `AppDb` direto no endpoint.
  3. Ampliar o `ArchitectureTests` com a regra entre módulos: um módulo não referencia a infraestrutura nem as entidades de outro.
  4. Avaliar mover o `AppDb` e as migrações para a `Infrastructure`, deixando a API só como entrada web.
- **Dívida conhecida, para explicar na banca.** As leituras de tela no `Program.cs` e o `AppDb` na API violam a regra 3 hoje. Elas estão registradas aqui com o plano de saída. Deixar a refatoração para depois foi uma escolha consciente de prazo, não um descuido.
- A linha "Monólito e banco único" de `docs/ARQUITETURA.md` passa a apontar para esta decisão.
