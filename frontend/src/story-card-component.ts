// src/app/story-card.component.ts
import { CommonModule } from "@angular/common";
import { Component, computed, input, output } from "@angular/core";

export interface Post {
  id: string;
  url: string;
  title: string;
  topic: string;
  type: string;
  comment: string;
  authorId: string;
  authorName: string;
  createdAt: string;
  isSaved: boolean;
}

@Component({
  selector: "app-story-card",
  standalone: true,
  imports: [CommonModule],
  template: `
    <article class="card">
      <div class="card-meta">
        <span
          class="badge"
          [class.podcast]="post().type === 'podcast'"
          [class.music]="post().type === 'music'"
          >{{ typeLabel() }}</span
        >
        <span class="topic">{{ post().topic }}</span>
      </div>

      <a
        class="post-title"
        [href]="safeUrl()"
        target="_blank"
        rel="noopener noreferrer"
        >{{ post().title }} <span>↗</span></a
      >
      <span class="source">{{ sourceDomain() }}</span>
      <p class="comment">{{ post().comment }}</p>

      <div class="author">
        <span class="avatar">{{ avatarInitial() }}</span>
        <div>
          <strong>{{ post().authorName }}</strong>
          <time>{{ post().createdAt | date : "dd/MM/yyyy · HH:mm" }}</time>
        </div>
      </div>

      <div class="card-actions">
        <button
          type="button"
          (click)="saveClick.emit(post())"
          [attr.aria-pressed]="post().isSaved"
        >
          {{ post().isSaved ? "◆ Salvo" : "◇ Salvar" }}
        </button>
        <button type="button" (click)="reportClick.emit(post())">
          Denunciar
        </button>
        @if (canDelete()) {
          <button
            type="button"
            class="danger"
            (click)="deleteClick.emit(post())"
          >
            Excluir
          </button>
        }
      </div>
    </article>
  `,
})
export class StoryCardComponent {
  post = input.required<Post>();
  currentUserId = input<string | null>(null);

  saveClick = output<Post>();
  reportClick = output<Post>();
  deleteClick = output<Post>();

  canDelete = computed(() => {
    const userId = this.currentUserId();
    return Boolean(userId && userId === this.post().authorId);
  });

  avatarInitial = computed(() =>
    (this.post().authorName || "?").slice(0, 1).toUpperCase(),
  );

  typeLabel = computed(() => {
    const type = this.post().type;
    const labels: Record<string, string> = {
      article: "Notícia",
      podcast: "Podcast",
      music: "Música",
    };
    return labels[type] || type;
  });

  sourceDomain = computed(() => {
    try {
      return new URL(this.post().url).hostname.replace(/^www\./, "");
    } catch {
      return "Fonte externa";
    }
  });

  safeUrl = computed(() => {
    try {
      const parsed = new URL(this.post().url);
      return ["https:", "http:"].includes(parsed.protocol) ? parsed.href : null;
    } catch {
      return null;
    }
  });
}