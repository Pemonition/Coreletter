# Arquitetura e limites do MVP

## Objetivo
Coreletter permite compartilhar links de notícias, podcasts e músicas em uma comunidade sobre tecnologia e IA. Usuários consultam um feed, filtram temas, salvam itens e denunciam conteúdo. Comparação textual é uma evolução premium simulada, com evidências e limites explícitos.

## Estrutura
- Angular: interface, formulários e estados de navegação.
- ASP.NET Core / .NET 10: API, autenticação e regras de autorização, validação e persistência.
- PostgreSQL: usuários, publicações, salvos e denúncias; novas entidades de comparação e quota somente após contrato aprovado.
- Provedor de comparação: implementação determinística primeiro; adapter Gemini opcional posteriormente.

Manter uma aplicação simples, sem microsserviços, filas ou múltiplos agentes em produção por enquanto. Os agentes de desenvolvimento auxiliam o trabalho da equipe; não são requisito arquitetural do produto.

O contrato concreto da base está em `backend/API.md`. Mudanças devem atualizar contrato, consumidor e testes na mesma PR ou em sequência compatível. O README descreve execução; este documento registra decisões, não substitui o código.

## Fluxos e fronteiras
1. Visitante lê feed público. Usuário autenticado publica, salva e denuncia.
2. API decide permissões pelo usuário autenticado, nunca pelo papel ou ID enviado no corpo.
3. Autor exclui sua publicação; administrador consulta denúncias e oculta conteúdo.
4. Compartilhar URL inicialmente armazena o link e texto informado. Não implica baixar mídia, republicar obra ou analisar áudio.
5. Comparação futura passa por autorização, quota/reserva e provedor; retorna evidências e limitações.

## Decisões
| Decisão | Motivo | Quando rever |
|---|---|---|
| Monólito e banco único | Prazo de três semanas e equipe pequena | Gargalo medido |
| Feed com filtros e paginação | Fluxo previsível e demonstrável | Evidência de uso justifica recomendações |
| Compartilhamento manual primeiro | Valida utilidade sem depender de coleta externa | Núcleo estável e fontes permitidas definidas |
| IA simulada primeiro | Demonstração reproduzível sem API | Acesso e limites Gemini confirmados |
| Planos e pagamento simulados | Testar regras sem cobrança real | Validação de mercado e requisitos comerciais |

## Segurança e qualidade
Cookies de autenticação exigem atenção a CSRF, origem/CORS, HTTPS e configuração antes de exposição pública. Rever provisionamento de administrador e persistência de chaves. Limitar tamanho das entradas e requisições; não renderizar HTML externo arbitrário. Nunca registrar senhas, tokens ou chaves de API.

Scraping futuro precisa de fontes e permissões definidas, prevenção de SSRF, limites de download, timeout e validação de redirecionamentos; não faz parte do simples cadastro de links. Conteúdo de fontes e usuários não pode instruir a IA a mudar regras ou expor dados.

TDD é prioritário para regras de negócio e permissões. Testes de integração verificam persistência e isolamento; testes de interface focam comportamento relevante. Testar endpoints apenas em memória não comprova comportamento PostgreSQL: registrar essa limitação quando ocorrer.

## Fora do compromisso inicial
Análise de áudio, integração com plataformas de música, agendamento em redes, pagamentos reais e personalização algorítmica avançada. Newsletter é opcional após concluir o núcleo. Aprovação acadêmica foi relatada por Hugo; a data oficial de entrega ainda precisa ser registrada. A equipe está identificada em `EQUIPE.md`. O repositório público foi autorizado; conteúdo privado não deve ser versionado.
