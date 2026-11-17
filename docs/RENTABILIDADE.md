# Regras de rentabilidade (Ficha, Seção 3.3)

Regulamento, seção 5: a regra precisa estar **no código que concentra as regras de negócio**, com **testes de casos limite** e **demonstrada com números**. Código: `backend/RadarPlural.Domain/Billing`. Testes: `ProfitabilityTests.cs` (regras puras) e `PgUsageLedgerTests` (reserva no banco). **Números são hipóteses** até o grupo confirmar na Ficha.

## Hipóteses de custo (`PlanCatalog.Assumptions`)
| Item | Valor | De onde vem |
|---|---|---|
| Custo de IA por comparação | R$ 0,08 | Estimativa de tokens (título + resumo de até 5 fontes); medir com o Gemini (B09) |
| Entrega por assinante pago/mês | R$ 0,50 | E-mail/WhatsApp do resumo |
| Margem mínima | 25% | Decisão de negócio |
| Custo máximo por usuário grátis/mês | R$ 0,30 | Custo de aquisição aceito |
| Custo fixo mensal | R$ 800 | Teto de infraestrutura (`deploy/k8s/custo.json`) |

## Planos (`PlanCatalog`)
| Plano | Preço/mês | Comparações/mês |
|---|---|---|
| Grátis | R$ 0 | 3 |
| Plus | R$ 19,90 | 60 |

## Regras
| ID | Regra | Exemplo com números |
|---|---|---|
| **RN01** | Quota mensal de comparações por plano. A 61ª comparação do Plus no mês é recusada | Plus usou 59 → permite; usou 60 → `quota_esgotada` |
| **RN02** | Margem mínima: nenhum plano pode dar prejuízo no pior caso (cliente usa a quota inteira). Preço ≥ custo ÷ (1 − 25%). **A API não inicia** se um plano violar | Plus: custo máx. = 60 × 0,08 + 0,50 = **R$ 5,30**; margem = (19,90 − 5,30) ÷ 19,90 = **73,4%**; preço mínimo = 5,30 ÷ 0,75 = **R$ 7,07**. A R$ 7,06 a API recusa subir |
| **RN03** | Custo de IA reservado **antes** de chamar o provedor, de forma atômica no banco (vale com várias réplicas); liquidado no sucesso, devolvido na falha; mesma chave não cobra duas vezes | 50 pedidos simultâneos com quota 10 → exatamente 10 aprovados |
| **RN04** | Ponto de equilíbrio: assinantes Plus necessários para pagar o custo fixo | 800 ÷ (19,90 − 5,30) = 54,8 → **55 assinantes** |

## Casos limite testados
- Margem exatamente no mínimo (R$ 7,07 passa), 1 centavo abaixo (R$ 7,06 falha), preço igual ao custo e preço abaixo do custo.
- Aumentar a quota sem aumentar o preço (300 comparações) é bloqueado.
- Plano grátis: 3 comparações (R$ 0,24) passa; 4 (R$ 0,32) estoura o limite de aquisição.
- Preço ou quota negativos são inválidos.
- Ponto de equilíbrio com divisão exata não soma 1; contribuição zero não tem equilíbrio.
- Quota: 59 permite, 60 bloqueia, quota zero nunca permite; o mês é contado em UTC.
- Banco: concorrência, idempotência, devolução única, liquidação, virada de mês.

## Para mostrar na apresentação
`GET /api/plans` devolve todos esses números ao vivo. `GET /api/me/usage` mostra a quota do usuário logado. O administrador troca o plano com `PUT /api/admin/members/{id}/plan`.
