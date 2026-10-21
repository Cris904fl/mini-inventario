import { Component, inject } from '@angular/core';
import { TemaService } from '../core/tema.service';

/** Boton con sol o luna que alterna entre tema claro y oscuro. */
@Component({
  selector: 'app-tema-selector',
  template: `
    <button type="button" class="boton-icono" (click)="tema.alternar()"
            [attr.aria-label]="tema.efectivo() === 'oscuro' ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro'"
            [title]="tema.efectivo() === 'oscuro' ? 'Tema claro' : 'Tema oscuro'">
      <svg class="icono" viewBox="0 0 20 20" aria-hidden="true">
        @if (tema.efectivo() === 'oscuro') {
          <!-- sol -->
          <circle cx="10" cy="10" r="3.5" />
          <path d="M10 2.5v2M10 15.5v2M2.5 10h2M15.5 10h2M4.7 4.7l1.4 1.4M13.9 13.9l1.4 1.4M4.7 15.3l1.4-1.4M13.9 6.1l1.4-1.4" />
        } @else {
          <!-- luna -->
          <path d="M16 12.2A6.5 6.5 0 0 1 7.8 4a6.5 6.5 0 1 0 8.2 8.2Z" />
        }
      </svg>
    </button>
  `,
})
export class TemaSelector {
  protected readonly tema = inject(TemaService);
}
