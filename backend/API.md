# Contrato API
Base `/api`; autenticação cookie HttpOnly (proxy Angular para http://localhost:5080). JSON camelCase. Erros `{error:string}` dentro de um ProblemDetails (RFC 9457, `application/problem+json`): `{type,title,status,detail?,error,traceId}`; o front usa `error`, `traceId` liga o erro ao log. Validação 400; não autenticado 401; sem permissão 403; inexistente 404; excesso de requisições 429.
- POST /auth/register {email,password,displayName} -> 200 {id,email,displayName,isAdmin}; senha >=10 caracteres. Login automático.
- POST /auth/login {email,password} -> mesmo DTO.
- POST /auth/logout ->204
- GET /auth/me -> DTO ou401
- GET /posts?page=1&pageSize=20&topic=&type= -> {items:[Post],page,pageSize,total}; público. type: article,podcast,music. topic texto.
- GET /posts/saved -> {items:[Post],page:1,pageSize:100,total}; autenticado.
- POST /posts {url,title,topic,type,comment} ->201 Post
- DELETE /posts/{id} ->204 proprietário
- PUT /posts/{id}/save ->204 (idempotente)
- DELETE /posts/{id}/save ->204
- POST /posts/{id}/reports {reason} ->204
- GET /moderation/reports -> [{id,postId,reason,createdAt}] administrador
- PUT /moderation/posts/{id}/hide ->204 administrador
- GET /news?page=1&pageSize=20&topic=&source= -> {items:[NewsItem],page,pageSize,total}; público; pageSize máx. 50; ordem por publicação (ou coleta) decrescente. topic exato (ex.: tecnologia, geral); source = id da fonte em fontes.json.
- GET /health -> 200 {status:"ok"}; vivacidade do processo, não consulta o banco.
- GET /health/ready -> 200 `Healthy` ou 503 `Unhealthy`; prontidão (consegue falar com o PostgreSQL).
Post: {id,url,title,topic,type,comment,authorId,authorName,createdAt,isSaved}.
NewsItem: {id,sourceId,sourceName,topic,url,title,summary,publishedAt|null,collectedAt}. Texto puro, já higienizado; summary até 300 caracteres. Coletado pelo RadarPlural.Worker (docs/decisoes/0002-ingestao.md).
Sem comparação IA nesta entrega. A API não baixa links enviados por usuários; só o Worker acessa a rede, e apenas fontes da lista aprovada.
Documentação OpenAPI: `/openapi/v1.json` e interface em `/swagger` (fora de `/api`), em Development ou com `OpenApi__Enabled=true`.
