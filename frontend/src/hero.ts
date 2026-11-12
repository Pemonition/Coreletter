import { Component, input } from "@angular/core";
import { BRAND } from "./brand";

// Destaque do topo: composição espacial em CSS 3D (planeta, anéis e cartões), sem biblioteca 3D nem modelo baixado.
// compacto = true fora do feed: só o título numa faixa curta, para o conteúdo da tela (radar, salvos, moderação)
// aparecer logo. A cena é decorativa (aria-hidden) e só anima sem "reduzir movimento" (regras em styles.css).
// Evolução com modelo GLB do Blender: ver docs/FRONTEND-3D.md (carregar sob demanda, mantendo esta cena como reserva).
@Component({
  selector: "app-hero",
  standalone: true,
  template: `<section class="hero" [class.hero-compact]="compacto()">
    <div class="hero-copy">
      <div class="eyebrow">UM RADAR. MUITAS PERSPECTIVAS.</div>
      <h1>Boas descobertas <span class="hero-line">merecem circular.</span></h1>
      @if (!compacto()) {
        <p>Notícias, podcasts e músicas compartilhados por pessoas curiosas como você.</p>
      }
    </div>
    @if (!compacto()) {
      <div class="discovery-3d" aria-hidden="true">
        <div class="spatial-scene">
          <div class="planet"><span>{{ monograma }}</span></div>
          <div class="planet-ring ring-one"></div>
          <div class="planet-ring ring-two"></div>
          <div class="floating-note note-one">✦ Tecnologia</div>
          <div class="floating-note note-two">◉ Novas perspectivas</div>
          <div class="satellite"></div>
        </div>
      </div>
    }
  </section>`,
})
export class SpatialHero {
  readonly compacto = input(false);
  readonly monograma = BRAND.monograma;
}
