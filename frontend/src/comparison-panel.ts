import { Component, input } from '@angular/core';
import { Claim, Comparison } from './comparison';

@Component({
  selector: 'app-comparison-panel',
  standalone: true,
  templateUrl: './comparison-panel.html',
})
export class ComparisonPanel {
  comparison = input.required<Comparison>();

  statusLabel(status: Claim['status']): string {
    const labels: Record<Claim['status'], string> = {
      concordante: 'Concordante',
      divergente: 'Divergente',
      unica_fonte: 'Única fonte',
      evidencia_insuficiente: 'Evidência insuficiente',
    };
    return labels[status] ?? status;
  }

  sourceName(itemId: string): string {
    return this.comparison().sources.find((s) => s.itemId === itemId)?.sourceName ?? 'Fonte';
  }
}