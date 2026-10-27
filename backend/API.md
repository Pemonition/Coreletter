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
Post: {id,url,title,topic,type,comment,authorId,authorName,createdAt,isSaved}.
Sem comparação IA nesta entrega. Sem download remoto de links.
