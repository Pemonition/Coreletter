import { Component, signal } from "@angular/core";
import { bootstrapApplication } from "@angular/platform-browser";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";

interface User {
  id: string;
  email: string;
  displayName: string;
  isAdmin: boolean;
}
interface Post {
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
interface Report {
  id: string;
  postId: string;
  reason: string;
  createdAt: string;
}
@Component({
  selector: "app-root",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./app.html",
})
class App {
  user = signal<User | null>(null);
  posts = signal<Post[]>([]);
  reports = signal<Report[]>([]);
  loading = signal(false);
  busy = signal(false);
  error = signal("");
  notice = signal("");
  authOpen = false;
  register = false;
  shareOpen = false;
  view = "feed";
  email = "";
  password = "";
  displayName = "";
  topic = "";
  type = "";
  page = 1;
  total = 0;
  pageSize = 12;
  draft = {
    url: "",
    title: "",
    topic: "Tecnologia e IA",
    type: "article",
    comment: "",
  };
  constructor() {
    void this.init();
  }
  async api<T>(path: string, init: RequestInit = {}): Promise<T> {
    let response: Response;
    try {
      response = await fetch("/api" + path, {
        ...init,
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", ...init.headers },
      });
    } catch {
      throw new Error(
        "Não foi possível conectar à API. Verifique se o backend está em execução.",
      );
    }
    if (!response.ok) {
      let body: any;
      try {
        body = await response.json();
      } catch {}
      throw new Error(
        body?.error ||
          (response.status === 401
            ? "Entre na sua conta para continuar."
            : "Não foi possível concluir a solicitação."),
      );
    }
    return response.status === 204 ? (undefined as T) : response.json();
  }
  async init() {
    try {
      this.user.set(await this.api<User>("/auth/me"));
    } catch {}
    await this.load();
  }
  async load() {
    this.loading.set(true);
    this.error.set("");
    try {
      if (this.view === "moderation") {
        this.reports.set(await this.api<Report[]>("/moderation/reports"));
        return;
      }
      const path =
        this.view === "saved"
          ? "/posts/saved"
          : "/posts?" +
            new URLSearchParams({
              page: String(this.page),
              pageSize: String(this.pageSize),
              topic: this.topic,
              type: this.type,
            });
      const result = await this.api<{ items: Post[]; total: number }>(path);
      this.posts.set(result.items);
      this.total = result.total;
    } catch (e) {
      this.fail(e);
    } finally {
      this.loading.set(false);
    }
  }
  fail(e: unknown) {
    this.error.set(
      e instanceof Error ? e.message : "Ocorreu um erro inesperado.",
    );
  }
  async authenticate() {
    this.busy.set(true);
    this.error.set("");
    try {
      this.user.set(
        await this.api<User>(
          "/auth/" + (this.register ? "register" : "login"),
          {
            method: "POST",
            body: JSON.stringify({
              email: this.email,
              password: this.password,
              displayName: this.displayName,
            }),
          },
        ),
      );
      this.password = "";
      this.authOpen = false;
      await this.load();
    } catch (e) {
      this.fail(e);
    } finally {
      this.busy.set(false);
    }
  }
  async logout() {
    try {
      await this.api("/auth/logout", { method: "POST" });
      this.user.set(null);
      this.view = "feed";
      await this.load();
    } catch (e) {
      this.fail(e);
    }
  }
  select(view: string) {
    if (view !== "feed" && !this.user()) {
      this.authOpen = true;
      return;
    }
    this.view = view;
    this.page = 1;
    void this.load();
  }
  filter() {
    this.page = 1;
    void this.load();
  }
  move(delta: number) {
    this.page += delta;
    void this.load();
  }
  share() {
    if (!this.user()) {
      this.authOpen = true;
      return;
    }
    this.shareOpen = true;
  }
  async publish() {
    this.busy.set(true);
    this.error.set("");
    try {
      const url = new URL(this.draft.url);
      if (!["https:", "http:"].includes(url.protocol))
        throw new Error("Informe um link http ou https.");
      await this.api("/posts", {
        method: "POST",
        body: JSON.stringify(this.draft),
      });
      this.shareOpen = false;
      this.draft = {
        url: "",
        title: "",
        topic: "Tecnologia e IA",
        type: "article",
        comment: "",
      };
      this.notice.set("Sua descoberta foi publicada na comunidade.");
      this.select("feed");
    } catch (e) {
      this.fail(e);
    } finally {
      this.busy.set(false);
    }
  }
  async save(post: Post) {
    if (!this.user()) {
      this.authOpen = true;
      return;
    }
    try {
      await this.api("/posts/" + post.id + "/save", {
        method: post.isSaved ? "DELETE" : "PUT",
      });
      if (this.view === "saved") await this.load();
      else
        this.posts.update((items) =>
          items.map((p) =>
            p.id === post.id ? { ...p, isSaved: !p.isSaved } : p,
          ),
        );
    } catch (e) {
      this.fail(e);
    }
  }
  async remove(post: Post) {
    if (!confirm("Excluir esta publicação?")) return;
    try {
      await this.api("/posts/" + post.id, { method: "DELETE" });
      await this.load();
    } catch (e) {
      this.fail(e);
    }
  }
  async report(post: Post) {
    if (!this.user()) {
      this.authOpen = true;
      return;
    }
    const reason = prompt(
      "Descreva o motivo da denúncia (mínimo 5 caracteres):",
    );
    if (!reason) return;
    if (reason.trim().length < 5) {
      this.error.set("Descreva o motivo em pelo menos 5 caracteres.");
      return;
    }
    try {
      await this.api("/posts/" + post.id + "/reports", {
        method: "POST",
        body: JSON.stringify({ reason }),
      });
      this.notice.set("Denúncia enviada para análise.");
    } catch (e) {
      this.fail(e);
    }
  }
  async hide(id: string) {
    if (!confirm("Ocultar esta publicação do feed?")) return;
    try {
      await this.api("/moderation/posts/" + id + "/hide", { method: "PUT" });
      this.notice.set("Publicação ocultada.");
      await this.load();
    } catch (e) {
      this.fail(e);
    }
  }
  label(type: string) {
    return (
      (
        { article: "Notícia", podcast: "Podcast", music: "Música" } as Record<
          string,
          string
        >
      )[type] || type
    );
  }
  domain(url: string) {
    try {
      return new URL(url).hostname.replace(/^www\./, "");
    } catch {
      return "Fonte externa";
    }
  }
  safeUrl(url: string) {
    try {
      const parsed = new URL(url);
      return ["https:", "http:"].includes(parsed.protocol) ? parsed.href : null;
    } catch {
      return null;
    }
  }
}
bootstrapApplication(App).catch(console.error);
