import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { InventarioService } from '../../core/inventario.service';
import { NuevoProducto, Producto } from '../../core/modelos';
import { MensajeDeError, describirError } from '../../core/problema';

@Component({
  selector: 'app-inventario',
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  templateUrl: './inventario.html',
  styleUrl: './inventario.css',
})
export class InventarioPage {
  private readonly servicio = inject(InventarioService);
  private readonly busquedas = new Subject<string>();

  protected readonly productos = signal<Producto[]>([]);
  protected readonly cargando = signal(true);
  protected readonly error = signal<MensajeDeError | null>(null);
  protected busqueda = '';
  protected readonly soloBajo = signal(false);

  // Resumen para las fichas de arriba.
  protected readonly resumen = computed(() => {
    const lista = this.productos();
    return {
      total: lista.length,
      bajo: lista.filter((p) => p.estado === 'BAJO').length,
      agotados: lista.filter((p) => p.estado === 'AGOTADO').length,
      unidades: lista.reduce((s, p) => s + p.cantidad, 0),
    };
  });

  // Alta de producto (extra sobre lo pedido).
  protected readonly mostrarAlta = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorAlta = signal<MensajeDeError | null>(null);
  protected readonly creado = signal<Producto | null>(null);
  protected nuevo: NuevoProducto = this.productoVacio();

  constructor() {
    // La busqueda la resuelve la funcion fn_inventario_actual en la base;
    // se espera un momento tras teclear para no disparar una consulta por letra.
    this.busquedas
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.cargar());
    this.cargar();
  }

  cargar() {
    this.cargando.set(true);
    this.error.set(null);
    this.servicio.inventario(this.busqueda, this.soloBajo()).subscribe({
      next: (lista) => {
        this.productos.set(lista);
        this.cargando.set(false);
      },
      error: (e: unknown) => {
        this.error.set(describirError(e));
        this.cargando.set(false);
      },
    });
  }

  alBuscar(texto: string) {
    this.busqueda = texto;
    this.busquedas.next(texto);
  }

  alternarSoloBajo() {
    this.soloBajo.update((v) => !v);
    this.cargar();
  }

  /** Porcentaje de la barra: cantidad frente al doble del minimo (o 1 si no hay minimo). */
  nivel(p: Producto): number {
    const referencia = Math.max(p.stockMinimo * 2, 1);
    return Math.min(100, Math.round((p.cantidad / referencia) * 100));
  }

  abrirAlta() {
    this.nuevo = this.productoVacio();
    this.errorAlta.set(null);
    this.creado.set(null);
    this.mostrarAlta.set(true);
  }

  cerrarAlta() {
    this.mostrarAlta.set(false);
  }

  guardarProducto() {
    this.errorAlta.set(null);
    this.guardando.set(true);
    this.servicio.crearProducto({
      ...this.nuevo,
      codigo: this.nuevo.codigo.trim().toUpperCase(),
      nombre: this.nuevo.nombre.trim(),
      unidad: this.nuevo.unidad.trim() || 'UND',
    }).subscribe({
      next: (p) => {
        this.creado.set(p);
        this.guardando.set(false);
        this.mostrarAlta.set(false);
        this.cargar();
      },
      error: (e: unknown) => {
        this.errorAlta.set(describirError(e));
        this.guardando.set(false);
      },
    });
  }

  private productoVacio(): NuevoProducto {
    return { codigo: '', nombre: '', descripcion: '', unidad: 'UND', stockMinimo: 0, cantidadInicial: 0 };
  }
}
