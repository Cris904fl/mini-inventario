import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InventarioService } from '../../core/inventario.service';
import { Producto, RespuestaMovimiento, TipoMovimiento } from '../../core/modelos';
import { MensajeDeError, describirError } from '../../core/problema';

@Component({
  selector: 'app-movimientos',
  imports: [FormsModule, RouterLink],
  templateUrl: './movimientos.html',
  styleUrl: './movimientos.css',
})
export class MovimientosPage {
  private readonly servicio = inject(InventarioService);
  private readonly ruta = inject(ActivatedRoute);

  protected readonly productos = signal<Producto[]>([]);
  protected readonly cargandoProductos = signal(true);

  protected readonly tipo = signal<TipoMovimiento>('ENTRADA');
  protected readonly productoId = signal<number | null>(null);
  protected readonly cantidad = signal<number | null>(null);
  protected observacion = '';

  protected readonly enviando = signal(false);
  protected readonly resultado = signal<RespuestaMovimiento | null>(null);
  protected readonly error = signal<MensajeDeError | null>(null);

  protected readonly producto = computed(() =>
    this.productos().find((p) => p.id === this.productoId()) ?? null);

  /** Stock que quedaria si la API acepta el movimiento. Solo orienta: la regla la aplica la base. */
  protected readonly stockPrevisto = computed(() => {
    const p = this.producto();
    const c = this.cantidad();
    if (!p || !c || c <= 0) return null;
    return this.tipo() === 'ENTRADA' ? p.cantidad + c : p.cantidad - c;
  });

  protected readonly excede = computed(() => {
    const previsto = this.stockPrevisto();
    return this.tipo() === 'SALIDA' && previsto !== null && previsto < 0;
  });

  protected readonly listo = computed(() =>
    !!this.productoId() && !!this.cantidad() && (this.cantidad() ?? 0) > 0 && !this.enviando());

  constructor() {
    // Permite llegar con ?tipo=SALIDA&productoId=3 desde otras pantallas.
    const q = this.ruta.snapshot.queryParamMap;
    if (q.get('tipo') === 'SALIDA') this.tipo.set('SALIDA');
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
    if (!productoId || !cantidad || cantidad <= 0) return;

    this.error.set(null);
    this.resultado.set(null);
    this.enviando.set(true);

    const solicitud = { productoId, cantidad, observacion: this.observacion.trim() || undefined };
    const peticion = this.tipo() === 'ENTRADA'
      ? this.servicio.registrarEntrada(solicitud)
      : this.servicio.registrarSalida(solicitud);

    peticion.subscribe({
      next: (r) => {
        this.resultado.set(r);
        this.enviando.set(false);
        this.cantidad.set(null);
        this.observacion = '';
        // Refresca el stock del selector con lo que devolvio la base.
        this.productos.update((lista) =>
          lista.map((p) => (p.id === r.productoId ? { ...p, cantidad: r.stockResultante } : p)));
        this.cargarProductos();
      },
      error: (e: unknown) => {
        // Aqui aterriza el 409 de stock insuficiente, el 404 de producto y el 400 de validacion.
        this.error.set(describirError(e));
        this.enviando.set(false);
      },
    });
  }
}
