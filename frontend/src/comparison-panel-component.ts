// src/app/comparison-panel.component.ts
import { CommonModule } from "@angular/common";
import { Component, computed, input, output } from "@angular/core";
import { Post } from "./story-card-component";

export interface ComparisonResult {
  summary?: string;
  outdated?: boolean;
  sourcesCount?: number;
}

@Component({
  selector: "app-comparison-panel",
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (selectedPosts().length > 0 || comparison() || loading() || error()) {
      <section
        class="comparison-panel"
        aria-live="polite"
        aria-label="Painel de comparação de fontes"
      >
        <div class="comparison-header">
          <div>
            <span class="eyebrow">COMPARAÇÃO DE PERSPECTIVAS</span>
            <h2>Comparar fontes selecionadas</h2>
          </div>
          <div class="comparison-actions">
            <button
              type="button"
              class="primary small"
              [disabled]="!canCompare() || loading()"
              (click)="compareClick.emit(selectedPosts())"
            >
              {{ loading() ? "Comparando…" : "Comparar fontes" }}
            </button>
            <button
              type="button"
              class="secondary"
              (click)="clearClick.emit()"
            >
              Limpar
            </button>
          </div>
        </div>

        @if (selectedPosts().length < 2 && !comparison()) {
          <p class="comparison-hint">
            Selecione pelo menos 2 fontes para comparar perspectivas.
          </p>
        }

        <ul class="selected-sources">
          @for (item of selectedPosts(); track item.id) {
            <li>
              <span>{{ item.title }}</span>
              <button
                type="button"
                aria-label="Remover {{ item.title }} da comparação"
                (click)="removePostClick.emit(item.id)"
              >
                ×
              </button>
            </li>
          }
        </ul>

        @if (error()) {
          <div class="message error" role="alert">
            <span>{{ error() }}</span>
          </div>
        }

        @if (loading()) {
          <div class="empty" role="status">
            Analisando fontes selecionadas…
          </div>
        } @else if (comparison(); as data) {
          @if (data.outdated) {
            <div class="message error" role="status">
              <span>Há fontes novas, comparar de novo.</span>
              <button
                type="button"
                class="secondary"
                [disabled]="selectedPosts().length < 2"
                (click)="recompareClick.emit(selectedPosts())"
              >
                Comparar de novo
              </button>
            </div>
          }

          @if (data.summary) {
            <p class="comment">{{ data.summary }}</p>
          }
        }
      </section>
    }
  `,
})
export class ComparisonPanelComponent {
  selectedPosts = input<Post[]>([]);
  comparison = input<ComparisonResult | null>(null);
  loading = input<boolean>(false);
  error = input<string>("");

  compareClick = output<Post[]>();
  recompareClick = output<Post[]>();
  removePostClick = output<string>();
  clearClick = output<void>();

  canCompare = computed(() => this.selectedPosts().length >= 2);
}