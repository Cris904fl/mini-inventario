import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { InventarioService } from '../../core/inventario.service';
import { Producto, RespuestaAjuste, RespuestaMovimiento, TipoMovimiento } from '../../core/modelos';
import { MensajeDeError, describirError } from '../../core/problema';

/** Lo que se muestra tras registrar, sea entrada, salida o ajuste. */
interface Registrado {
  tipo: TipoMovimiento;
  movimientoId: number;
  cantidad: number;      // unidades movidas; en un ajuste, la diferencia con signo
  stockResultante: number;
}

@Component({
  selector: 'app-movimientos',
  imports: [FormsModule, RouterLink],
  templateUrl: './movimientos.html',
  styleUrl: './movimientos.css',
})
export class MovimientosPage {
  private readonly servicio = inject(InventarioService);
  private readonly ruta = inject(ActivatedRoute);
  protected readonly auth = inject(AuthService);

  protected readonly productos = signal<Producto[]>([]);
  protected readonly cargandoProductos = signal(true);

  protected readonly tipo = signal<TipoMovimiento>('ENTRADA');
  protected readonly productoId = signal<number | null>(null);
  /** Unidades que entran o salen; en un ajuste, la cantidad contada. */
  protected readonly cantidad = signal<number | null>(null);
  /** Observacion opcional; en un ajuste es el motivo y es obligatorio. */
  protected readonly observacion = signal('');

  protected readonly enviando = signal(false);
  protected readonly resultado = signal<Registrado | null>(null);
  protected readonly error = signal<MensajeDeError | null>(null);

  protected readonly esAjuste = computed(() => this.tipo() === 'AJUSTE');

  protected readonly producto = computed(() =>
    this.productos().find((p) => p.id === this.productoId()) ?? null);

  /** Stock que quedaria si la API acepta el movimiento. Solo orienta: la regla la aplica la base. */
  protected readonly stockPrevisto = computed(() => {
    const p = this.producto();
    const c = this.cantidad();
    if (!p || c === null || c < 0) return null;
    switch (this.tipo()) {
      case 'ENTRADA': return c > 0 ? p.cantidad + c : null;
      case 'SALIDA': return c > 0 ? p.cantidad - c : null;
      case 'AJUSTE': return c;
    }
  });

  /** En un ajuste, la diferencia que la base registraria (contado menos stock). */
  protected readonly diferenciaPrevista = computed(() => {
    const p = this.producto();
    const c = this.cantidad();
    return this.esAjuste() && p && c !== null && c >= 0 ? c - p.cantidad : null;
  });

  protected readonly excede = computed(() => {
    const previsto = this.stockPrevisto();
    return this.tipo() === 'SALIDA' && previsto !== null && previsto < 0;
  });

  protected readonly listo = computed(() => {
    const c = this.cantidad();
    if (!this.productoId() || c === null || this.enviando()) return false;
    if (this.esAjuste()) return c >= 0 && this.observacion().trim().length >= 5;
    return c > 0;
  });

  constructor() {
    // Permite llegar con ?tipo=SALIDA&productoId=3 desde otras pantallas.
    const q = this.ruta.snapshot.queryParamMap;
    const t = q.get('tipo');
    if (t === 'SALIDA' || (t === 'AJUSTE' && this.auth.esAdmin())) this.tipo.set(t);
    const id = Number(q.get('productoId'));
    if (id > 0) this.productoId.set(id);

    this.cargarProductos();
  }

  cargarProductos() {
    this.cargandoProductos.set(true);
    this.servicio.inventario().subscribe({
      next: (lista) => {
        this.productos.set(lista);
        this.cargandoProductos.set(false);
      },
      error: (e: unknown) => {
        this.error.set(describirError(e));
        this.cargandoProductos.set(false);
      },
    });
  }

  elegirTipo(t: TipoMovimiento) {
    this.tipo.set(t);
    this.resultado.set(null);
    this.error.set(null);
  }

  registrar() {
    const productoId = this.productoId();
    const cantidad = this.cantidad();
    if (!this.listo() || !productoId || cantidad === null) return;

    this.error.set(null);
    this.resultado.set(null);
    this.enviando.set(true);

    const observacion = this.observacion().trim();
    const tipo = this.tipo();
    // Union explicita: los dos observables tienen tipos distintos y subscribe no los combina solo.
    const peticion: Observable<RespuestaMovimiento | RespuestaAjuste> = tipo === 'AJUSTE'
      ? this.servicio.registrarAjuste({ productoId, cantidadContada: cantidad, motivo: observacion })
      : tipo === 'ENTRADA'
        ? this.servicio.registrarEntrada({ productoId, cantidad, observacion: observacion || undefined })
        : this.servicio.registrarSalida({ productoId, cantidad, observacion: observacion || undefined });

    peticion.subscribe({
      next: (r) => {
        this.resultado.set({
          tipo,
          movimientoId: r.movimientoId,
          cantidad: 'diferencia' in r ? r.diferencia : r.cantidad,
          stockResultante: r.stockResultante,
        });
        this.enviando.set(false);
        this.cantidad.set(null);
        this.observacion.set('');
        // Refresca el stock del selector con lo que devolvio la base.
        this.productos.update((lista) =>
          lista.map((p) => (p.id === productoId ? { ...p, cantidad: r.stockResultante } : p)));
        this.cargarProductos();
      },
      error: (e: unknown) => {
        // Aqui aterriza el 409 de stock insuficiente, el 404 de producto y los 400 de validacion.
        this.error.set(describirError(e));
        this.enviando.set(false);
      },
    });
  }

  signo(n: number): string {
    return n > 0 ? `+${n}` : `${n}`;
  }
}
