# Evolução do destaque 3D: CSS hoje, GLB do Blender depois?

Estado em 12/11/2026, branch `claude/frontend-visual`. O destaque do topo é uma composição em **CSS 3D**: planeta, anéis, satélite e cartões em perspectiva (`frontend/src/hero.ts` + bloco "Spatial" em `styles.css`). Não usa biblioteca 3D, modelo baixado nem WebGL.

## Recomendação curta

1. **Não adicionar biblioteca 3D agora.** O CSS já entrega a identidade "espacial" com custo quase zero.
2. **Próximo passo, quando houver arte no Blender:** renderizar a cena no próprio Blender como **imagem estática** (WebP/AVIF) e usá-la no lugar do planeta CSS. Fica com a qualidade do Blender (luz, material, sombra) por ~30–80 KB e nenhum JavaScript novo.
3. **WebGL com GLB:** só se a equipe quiser **interação** (girar o planeta com o mouse, reagir ao conteúdo). Mesmo assim, carregado **sob demanda** e com o CSS ou a imagem como reserva. Regras abaixo.

## Números medidos (não estimados)

Medidos em 12/11/2026 baixando os pacotes do npm, sem adicioná-los ao projeto, e comprimindo com gzip -9.

| O quê | Tamanho gzip | Observação |
|---|---|---|
| **Site inteiro hoje** (bundle inicial) | **65,5 KB** | `npm run build` |
| three.js 0.187: renderer, cena, câmera e `GLTFLoader` | **152 KB** | empacotado com esbuild, só o que é usado |
| `@google/model-viewer` 4.4.0 | **282 KB** | componente pronto; já inclui o three |
| Modelo GLB (planeta + anéis, low-poly) | ~50–300 KB | depende das texturas; comprimir com `gltf-transform` (meshopt + texturas WebP de 512 px) |

Conclusão: o 3D em WebGL **multiplica por 3 a 7 o JavaScript da página**, por um elemento decorativo. Por isso ele não pode entrar no carregamento inicial.

## Se for usar GLB: regras para não piorar o site

- **Carregar sob demanda:** `import()` dinâmico do three/loader só depois que a página estiver pronta e o destaque visível (IntersectionObserver). O bundle inicial continua com ~65 KB.
- **Só carregar quando fizer sentido:** não carregar com "reduzir movimento" ativo, no celular (tela < 768 px), sem WebGL, ou com `navigator.connection.saveData`.
- **Reserva sempre presente:** a cena CSS (ou a imagem do Blender) aparece primeiro, no mesmo tamanho. O canvas só substitui quando o modelo terminar de carregar, sem pular o layout. Se der erro, fica a reserva, sem mensagem para o usuário.
- **Orçamento:** GLB ≤ 200 KB; JS 3D ≤ 160 KB gzip, fora do bundle inicial; 60 fps num notebook comum; parar de renderizar quando o destaque sai da tela.
- **Acessibilidade:** canvas com `aria-hidden="true"`, sem foco de teclado, rotação automática desligada com "reduzir movimento".
- **Arquivo do modelo:** em `frontend/public/3d/`, com nome versionado (`planeta-v1.glb`) e a origem/licença registradas. Se a arte não for da equipe, conferir o direito de uso.

## Por que não Spline ou outra ferramenta visual

O runtime do Spline (`@splinetool/runtime`) é ainda mais pesado que o three e prende a cena a um serviço externo. Para um TCC, o caminho Blender → imagem (ou Blender → GLB → three) mantém tudo no repositório e explicável na banca.

## Decisões pendentes (Hugo e equipe)

- Quem faz a arte no Blender e com qual referência visual.
- Se o destaque precisa de interação. Se não precisar, fica a imagem estática e nenhuma dependência nova.
