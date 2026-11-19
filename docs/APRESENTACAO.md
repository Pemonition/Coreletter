# Roteiro da apresentação (B10) — rascunho

Estado: **rascunho** do tech lead para a equipe completar. Duração-alvo: 5 a 8 min de demonstração. Requisito da B10: funcionar **sem internet**, cada aluno explica seu código e um teste, limitações explícitas.

## Princípios
- **Sem internet:** banco local com dados já coletados (snapshot fictício ou de fontes permitidas) e adaptadores **simulados** de IA e vídeo. Nada depende do Render, do Gemini nem do HyperFrames ao vivo.
- **Vídeo pré-renderizado:** o vídeo de exemplo (HyperFrames) é gerado antes e aberto como arquivo.
- **Plano B:** gravação de tela do fluxo inteiro, caso algo falhe.

## Roteiro (minutos aproximados)
| Tempo | Quem | O que mostra | Ponto técnico |
|---|---|---|---|
| 0:00–0:45 | Hugo | Problema: a mesma notícia contada de jeitos diferentes; ninguém compara fontes. Abertura em vídeo (HyperFrames) | Proposta e público |
| 0:45–2:00 | Hugo | Coletor + triagem + agrupamento: `--dry-run` mostrando descartes e histórias com 2+ fontes | Política de ingestão (SSRF, direito autoral), TDD com casos reais |
| 2:00–3:15 | Lindomar | Feed de histórias na interface, estados de carregamento/erro/vazio, acessibilidade | B02, B06 |
| 3:15–4:30 | Davidy | Comparação de uma história: concordâncias, divergências, citações; quota e reserva de custo | B04/B05/B07, regra de rentabilidade com teste |
| 4:30–5:30 | Henrique | Validação e moderação: entrada inválida → 400; leitor não modera; admin oculta | B03/B08, um teste explicado |
| 5:30–6:30 | Hugo | Arquitetura: estado no banco, portas e adaptadores, mapa para Kubernetes (demo local ou slide) | Decisão 0003, aulas |
| 6:30–7:30 | Todos | Limitações e próximos passos | Honestidade técnica |

## Limitações a declarar (não esconder)
- Comparação feita sobre manchetes e resumos, não sobre o texto completo.
- Agrupamento por palavras: não entende sinônimos e pode juntar ou separar errado.
- Concordância entre veículos não prova verdade.
- IA e pagamentos simulados na demo; números de custo são hipóteses.

## Checklist antes do ensaio
- [ ] Banco local com snapshot de notícias carregado (sem dados pessoais)
- [ ] Adaptadores simulados ligados e identificados na tela
- [ ] Vídeo de abertura e vídeo de exemplo renderizados
- [ ] Cada integrante com seu trecho de código e um teste para explicar
- [ ] Gravação de tela de reserva
- [ ] Ensaio cronometrado registrado na issue #10

## Lacunas (dependem de outras tarefas)
B02, B05, B06, B07 e B08 ainda não estão prontos: os trechos de Lindomar, Davidy e Henrique precisam ser confirmados por eles.
