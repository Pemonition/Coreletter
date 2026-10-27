# Coreletter — interface Angular

Requer Node.js 24.15 ou superior na linha 24 (ambiente validado: 24.21), npm e API executando em `http://localhost:5080`.

```powershell
npm ci
npm start
```

Abra `http://localhost:4200`. O proxy encaminha `/api` preservando o Host para a proteção de origem da API. Utilize a mesma grafia `localhost` no navegador. Não configure `changeOrigin: true` sem adaptar e testar a proteção CSRF no backend.

```powershell
npm run build
```

O build verifica TypeScript e templates estritos. `npm test` é somente uma verificação de compilação em desenvolvimento; não representa uma suíte de testes funcionais.

## Validação manual integrada

1. Com PostgreSQL e API ativos, crie duas contas diferentes (senha de pelo menos dez caracteres).
2. Compartilhe um link HTTP(S) como a primeira pessoa. Abra o link e confirme o domínio exibido.
3. Saia, entre com a segunda pessoa e confirme que a publicação aparece no feed; filtre por tema e formato.
4. Salve o item, abra Itens salvos e remova o salvamento.
5. Denuncie a publicação com motivo. A segunda pessoa não deve ver o botão de excluir a publicação da primeira.
6. Volte à primeira conta e exclua a própria publicação.
7. Quando uma conta de administrador estiver configurada pelo backend, confira a lista de denúncias e o comando de ocultação.
8. Desligue a API e atualize o feed: deve haver erro explícito, sem dados fictícios substituindo a resposta.

Não há comparação de fontes, cobrança, publicação em redes sociais, scraping ou transcrição de áudio nesta primeira fatia. Essas funções são próximas etapas; a interface informa a ausência de comparação com IA.
