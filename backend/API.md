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
# Coleta privada por questionário (incremento local)

Todos os endpoints `/api/collections/*` exigem autenticação e rate limit da API; o usuário é identificado pelo cookie, nunca por ID recebido no corpo.

- `GET /api/collections/sources`: fontes habilitadas `{id,name,language}`; sem URLs arbitrárias fornecidas pelo cliente. Retorna `[]` quando a coleta global está desativada.
- `GET /api/collections/profile`: perfil da própria conta ou nulo/corpo vazio quando ainda não existe.
- `PUT /api/collections/profile`: `{topic,keywords,excludedKeywords,sourceIds,intervalHours,maxItems,enabled}`. Tema até80; 1–10 termos e até10 exclusões, cada um até60; 1–3 fontes; frequência6/12/24h; limite1–20. Ativação agenda a primeira execução. Alteração preserva o histórico. Para perfil existente, `enabled:false` permite pausar mesmo que o catálogo anterior tenha sido removido; nesse caso os demais campos não são atualizados.
- `GET /api/collections/items`: últimos100 itens privados, incluindo configurações anteriores; título, trecho curto, URL, fonte e datas. Nenhuma publicação automática no feed.
- `GET /api/collections/export`: JSON de evidências com limites; não chama IA nem envia mensagem.

Status: `pending`, `paused`, `success`, `partial`, `failed`. `success` com lista vazia significa coleta válida sem correspondências; falha nunca equivale a ausência de novidades. Execução depende da API e banco ativos. Ver `docs/decisoes/0001-coleta-por-questionario.md` para fonte permitida, rede e esquema aditivo.

