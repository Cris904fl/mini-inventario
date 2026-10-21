import { Component, inject } from '@angular/core';
import { TemaService } from '../core/tema.service';

/** Tres botones: Sistema, Claro, Oscuro. */
@Component({
  selector: 'app-tema-selector',
  template: `
    <div class="tema" role="group" aria-label="Tema visual">
      @for (o of tema.opciones; track o.valor) {
        <button type="button" class="tema-opcion" [class.activa]="tema.tema() === o.valor"
                [attr.aria-pressed]="tema.tema() === o.valor" (click)="tema.elegir(o.valor)">
          {{ o.nombre }}
        </button>
      }
    </div>
  `,
  styles: `
    .tema {
      display: inline-flex;
      border: 1px solid var(--linea-fuerte);
      border-radius: var(--radio);
      overflow: hidden;
    }
    .tema-opcion {
      padding: 4px 9px;
      border: 0;
      background: var(--papel);
      color: var(--tinta-2);
      font-size: 0.78rem;
      cursor: pointer;
    }
    .tema-opcion + .tema-opcion { border-left: 1px solid var(--linea-fuerte); }
    .tema-opcion.activa { background: var(--tinta); color: var(--papel); font-weight: 500; }
  `,
})
export class TemaSelector {
  protected readonly tema = inject(TemaService);
}
