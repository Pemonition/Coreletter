import { Component, Input, OnChanges, OnDestroy, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";

interface Source { id: string; name: string; language: string; }
interface CollectionSettings {
  topic: string;
  keywords: string[];
  excludedKeywords: string[];
  sourceIds: string[];
  intervalHours: number;
  maxItems: number;
  enabled: boolean;
}
interface CollectionProfile extends CollectionSettings {
  nextRunAt: string | null;
  lastRunAt: string | null;
  lastStatus: string | null;
  lastError: string | null;
}
interface CollectedItem {
  id: string;
  sourceId: string;
  sourceName: string;
  url: string;
  title: string;
  summary: string;
  publishedAt: string | null;
  collectedAt: string;
}

@Component({
  selector: "app-collection-radar",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./collection-radar.html",
})
export class CollectionRadar implements OnChanges, OnDestroy {
  @Input({ required: true }) memberId = "";
  sources = signal<Source[]>([]);
  profile = signal<CollectionProfile | null>(null);
  items = signal<CollectedItem[]>([]);
  loading = signal(true);
  refreshing = signal(false);
  busy = signal(false);
  error = signal("");
  notice = signal("");
  topic = "";
  keywords = "";
  excludedKeywords = "";
  sourceIds: string[] = [];
  intervalHours = 24;
  maxItems = 10;
  private controller = new AbortController();
  private timer?: ReturnType<typeof setInterval>;
  private version = 0;
  private lifetime = 0;
  private formLoaded = false;

  ngOnChanges() {
    this.stop();
    this.controller = new AbortController();
    this.sources.set([]);
    this.profile.set(null);
    this.items.set([]);
    this.loading.set(true);
    this.error.set("");
    this.notice.set("");
    this.busy.set(false);
    this.topic = this.keywords = this.excludedKeywords = "";
    this.sourceIds = [];
    this.intervalHours = 24;
    this.maxItems = 10;
    this.formLoaded = false;
    void this.refresh(true);
    this.timer = setInterval(() => {
      if (!this.loading() && !this.busy() && !this.refreshing()) void this.refresh();
    }, 15000);
  }
  ngOnDestroy() { this.stop(); }
  private stop() {
    this.lifetime++;
    this.version++;
    this.controller.abort();
    if (this.timer) clearInterval(this.timer);
  }
  private async api<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await fetch("/api/collections" + path, {
      ...init,
      credentials: "same-origin",
      signal: this.controller.signal,
      headers: { "Content-Type": "application/json", ...init.headers },
    });
    if (!response.ok) {
      const body = await response.json().catch(() => null);
      throw new Error(body?.error || (response.status === 401
        ? "Sua sessão expirou. Entre novamente para acessar seu radar."
        : "Não foi possível atualizar seu radar. Tente novamente."));
    }
    // ASP.NET may return an empty 200 body when no profile exists yet.
    const text = response.status === 204 ? "" : await response.text();
    return text ? JSON.parse(text) as T : null as T;
  }
  async refresh(restoreForm = false) {
    const version = ++this.version;
    this.refreshing.set(true);
    try {
      const [sources, profile, items] = await Promise.all([
        this.api<Source[]>("/sources"),
        this.api<CollectionProfile | null>("/profile"),
        this.api<CollectedItem[]>("/items"),
      ]);
      if (version !== this.version || this.controller.signal.aborted) return;
      this.sources.set(sources);
      this.profile.set(profile);
      this.items.set(items);
      if ((restoreForm || !this.formLoaded) && profile) {
        this.topic = profile.topic;
        this.keywords = profile.keywords.join(", ");
        this.excludedKeywords = profile.excludedKeywords.join(", ");
        this.sourceIds = profile.sourceIds.filter(id => sources.some(source => source.id === id));
        this.intervalHours = profile.intervalHours;
        this.maxItems = profile.maxItems;
      }
      this.formLoaded = true;
      this.error.set("");
    } catch (e) {
      if (version === this.version && !this.controller.signal.aborted) this.fail(e);
    } finally {
      if (version === this.version) {
        this.loading.set(false);
        this.refreshing.set(false);
      }
    }
  }
  selectSource(id: string, selected: boolean) {
    if (selected && !this.sourceIds.includes(id) && this.sourceIds.length >= 3) {
      this.error.set("Selecione até três fontes para cada coleta.");
      return;
    }
    this.sourceIds = selected ? [...new Set([...this.sourceIds, id])]
      : this.sourceIds.filter(source => source !== id);
  }
  private terms(value: string) {
    return [...new Set(value.split(",").map(term => term.trim()).filter(Boolean))];
  }
  async save() {
    const keywords = this.terms(this.keywords);
    const excludedKeywords = this.terms(this.excludedKeywords);
    if (keywords.length < 1 || keywords.length > 10 || excludedKeywords.length > 10
      || [...keywords, ...excludedKeywords].some(term => term.length > 60)) {
      this.error.set("Informe de 1 a 10 expressões de interesse e até 10 exclusões, com até 60 caracteres cada.");
      return;
    }
    await this.persist({
      topic: this.topic.trim(),
      keywords,
      excludedKeywords,
      sourceIds: [...this.sourceIds],
      intervalHours: Number(this.intervalHours),
      maxItems: Number(this.maxItems),
      enabled: true,
    });
  }
  async pause() {
    const current = this.profile();
    if (!current) return;
    // Pause the saved configuration, without silently saving unfinished edits.
    await this.persist({
      topic: current.topic,
      keywords: current.keywords,
      excludedKeywords: current.excludedKeywords,
      sourceIds: current.sourceIds,
      intervalHours: current.intervalHours,
      maxItems: current.maxItems,
      enabled: false,
    });
  }
  private async persist(settings: CollectionSettings) {
    if (this.busy() || this.loading()) return;
    const version = ++this.version;
    const lifetime = this.lifetime;
    this.refreshing.set(false);
    this.busy.set(true);
    this.error.set("");
    this.notice.set("");
    try {
      await this.api("/profile", { method: "PUT", body: JSON.stringify(settings) });
      if (version !== this.version || this.controller.signal.aborted) return;
      this.notice.set(settings.enabled
        ? "Preferências salvas. A primeira coleta será processada automaticamente com o servidor em execução."
        : "Coleta pausada. Os resultados anteriores continuam disponíveis.");
      await this.refresh(settings.enabled);
    } catch (e) {
      if (version === this.version && !this.controller.signal.aborted) this.fail(e);
    } finally {
      if (lifetime === this.lifetime) this.busy.set(false);
    }
  }
  async exportEvidence() {
    if (this.busy()) return;
    const lifetime = this.lifetime;
    this.busy.set(true);
    this.error.set("");
    try {
      const evidence = await this.api<unknown>("/export");
      if (lifetime !== this.lifetime || this.controller.signal.aborted) return;
      const url = URL.createObjectURL(new Blob([JSON.stringify(evidence, null, 2)], {
        type: "application/json;charset=utf-8",
      }));
      const link = document.createElement("a");
      link.href = url;
      link.download = "coreletter-fontes.json";
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      this.notice.set("Arquivo exportado. Você pode anexá-lo a uma conversa para pedir análise das fontes.");
    } catch (e) {
      if (lifetime === this.lifetime && !this.controller.signal.aborted) this.fail(e);
    } finally {
      if (lifetime === this.lifetime) this.busy.set(false);
    }
  }
  private fail(e: unknown) {
    this.error.set(e instanceof Error ? e.message : "Não foi possível conectar ao seu radar.");
  }
  status(value: string | null) {
    return ({ pending: "Aguardando coleta", queued: "Aguardando coleta", running: "Coletando",
      success: "Coleta concluída", partial: "Coleta parcial", failed: "Falha na coleta",
      paused: "Coleta pausada" } as Record<string, string>)[value || ""] || value || "Ainda sem coleta";
  }
  safeUrl(value: string) {
    try {
      const url = new URL(value);
      return ["https:", "http:"].includes(url.protocol) && !url.username && !url.password ? url.href : null;
    } catch { return null; }
  }
}
