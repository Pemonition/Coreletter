# Contrato API
Base `/api`; autenticação cookie HttpOnly (proxy Angular para http://localhost:5080). JSON camelCase. Erros `{error:string}`; validação HTTP400; não autenticado401; sem permissão403.
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
- GET /health -> 200 {status:"ok"} (vivo; não consulta o banco). GET /health/ready -> 200 {status:"ready"} ou 503 {status:"unavailable"} (pronto; consulta o banco em até 2 s). Usados pelas sondas do Kubernetes (deploy/k8s).
- GET /news?page=1&pageSize=20&topic=&source= -> {items:[NewsItem],page,pageSize,total}; público; só notícias aprovadas na triagem; pageSize máx. 50; ordem por publicação (ou coleta) decrescente. topic exato (ex.: tecnologia, geral); source = id da fonte em fontes.json.
- GET /stories?page=1&pageSize=20&topic=&minSources=1 -> {items:[Story],page,pageSize,total}; público; ordem pela notícia mais recente. minSources=2 mostra só histórias corroboradas por 2+ veículos.
Post: {id,url,title,topic,type,comment,authorId,authorName,createdAt,isSaved}.
Story: {id,title,topic,status,sourceCount,itemCount,firstPublishedAt,lastPublishedAt,items:[{id,sourceId,sourceName,url,title,publishedAt|null}]}. title = manchete mais antiga do grupo. Agrupamento automático (docs/decisoes/0003); pode errar.
NewsItem: {id,sourceId,sourceName,topic,url,title,summary,publishedAt|null,collectedAt}. Texto puro, já higienizado; summary até 300 caracteres. Coletado pelo RadarPlural.Worker (docs/decisoes/0002-ingestao.md).
Sem comparação IA nesta entrega. A API não baixa links enviados por usuários; só o Worker acessa a rede, e apenas fontes da lista aprovada.
