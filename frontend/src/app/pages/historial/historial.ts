import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { InventarioService } from '../../core/inventario.service';
import { Movimiento, Producto, TipoMovimiento } from '../../core/modelos';
import { MensajeDeError, describirError } from '../../core/problema';

@Component({
  selector: 'app-historial',
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './historial.html',
  styleUrl: './historial.css',
})
export class HistorialPage {
  private readonly servicio = inject(InventarioService);

  protected readonly movimientos = signal<Movimiento[]>([]);
  protected readonly productos = signal<Producto[]>([]);
  protected readonly total = signal(0);
  protected readonly totalPaginas = signal(0);
  protected readonly pagina = signal(1);
  protected readonly tamano = 15;
  protected readonly cargando = signal(true);
  protected readonly error = signal<MensajeDeError | null>(null);

  protected readonly tipo = signal<TipoMovimiento | null>(null);
  protected readonly productoId = signal<number | null>(null);

  protected readonly desde = computed(() => (this.total() === 0 ? 0 : (this.pagina() - 1) * this.tamano + 1));
  protected readonly hasta = computed(() => Math.min(this.pagina() * this.tamano, this.total()));

  constructor() {
    this.servicio.inventario().subscribe({ next: (lista) => this.productos.set(lista) });
    this.cargar();
  }

  cargar() {
    this.cargando.set(true);
    this.error.set(null);
    this.servicio
      .historial({ pagina: this.pagina(), tamano: this.tamano, tipo: this.tipo(), productoId: this.productoId() })
      .subscribe({
        next: (p) => {
          this.movimientos.set(p.elementos);
          this.total.set(p.total);
          this.totalPaginas.set(p.totalPaginas);
          this.cargando.set(false);
        },
        error: (e: unknown) => {
          this.error.set(describirError(e));
          this.cargando.set(false);
        },
      });
  }

  filtrarTipo(t: TipoMovimiento | null) {
    this.tipo.set(t);
    this.pagina.set(1);
    this.cargar();
  }

  filtrarProducto(id: number | null) {
    this.productoId.set(id);
    this.pagina.set(1);
    this.cargar();
  }

  irA(p: number) {
    if (p < 1 || p > this.totalPaginas() || p === this.pagina()) return;
    this.pagina.set(p);
    this.cargar();
  }
}
