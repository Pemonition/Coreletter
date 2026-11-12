import { Component, inject } from "@angular/core";
import { Title } from "@angular/platform-browser";

// Nome exibido do produto: ÚNICO lugar para trocar quando a equipe decidir (Coreletter é provisório; Notiffy em estudo).
// Não renomeia namespaces .NET, pacote npm, banco nem repositório: é só o que a pessoa vê na tela.
// O logo é o nome em duas cores. `destaque` é a parte pintada de verde; deixe "" para o nome numa cor só.
// `monograma` aparece no planeta do topo da página (decorativo).
export const BRAND = {
  nome: "Coreletter",
  destaque: "letter",
  monograma: "c.",
  slogan: "Sua comunidade de descobertas",
} as const;

// Divide o nome para o logo: ["Core", "letter"]. Se o destaque não estiver no fim do nome, usa o nome inteiro numa cor.
export function partesDoLogo(nome: string, destaque: string): [string, string] {
  return destaque && nome.endsWith(destaque) && destaque !== nome ? [nome.slice(0, -destaque.length), destaque] : [nome, ""];
}

@Component({
  selector: "app-brand",
  standalone: true,
  // O index.html tem um <title> fixo só para o primeiro carregamento; este componente o substitui pelo nome central.
  template: `<a class="brand" href="/" [attr.aria-label]="brand.nome + ' início'"
    ><span class="brand-icon" aria-hidden="true">◉</span> {{ logo[0] }}<span>{{ logo[1] }}</span></a
  >`,
})
export class BrandMark {
  readonly brand = BRAND;
  readonly logo = partesDoLogo(BRAND.nome, BRAND.destaque);

  constructor() {
    inject(Title).setTitle(`${BRAND.nome} · ${BRAND.slogan}`);
  }
}
