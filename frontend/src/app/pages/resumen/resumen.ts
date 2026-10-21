import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InventarioService } from '../../core/inventario.service';
import { Resumen } from '../../core/modelos';
import { MensajeDeError, describirError } from '../../core/problema';

interface BarraDelDia {
  etiqueta: string;     // dd/MM
  x: number;            // inicio del grupo en el viewBox
  altoEntradas: number;
  altoSalidas: number;
  entradas: number;
  salidas: number;
  mostrarEtiqueta: boolean;
}

/** Tablero de inicio: cifras, movimientos por dia, rotacion, bajo minimo y ultimos. */
@Component({
  selector: 'app-resumen',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './resumen.html',
  styleUrl: './resumen.css',
})
export class ResumenPage {
  private readonly servicio = inject(InventarioService);

  protected readonly datos = signal<Resumen | null>(null);
  protected readonly cargando = signal(true);
  protected readonly error = signal<MensajeDeError | null>(null);
  protected readonly dias = signal<7 | 30>(7);
  protected readonly periodos: (7 | 30)[] = [7, 30];

  // Geometria del grafico (unidades del viewBox; el SVG escala al ancho disponible).
  protected readonly ancho = 640;
  protected readonly alto = 180;
  private readonly margenInferior = 22;
  private readonly margenSuperior = 8;

  protected readonly maximo = computed(() => {
    const serie = this.datos()?.porDia ?? [];
    return Math.max(1, ...serie.map((d) => Math.max(d.entradas, d.salidas)));
  });

  protected readonly barras = computed<BarraDelDia[]>(() => {
    const serie = this.datos()?.porDia ?? [];
    if (serie.length === 0) return [];
    const anchoGrupo = this.ancho / serie.length;
    const altoUtil = this.alto - this.margenInferior - this.margenSuperior;
    const cadaCuantas = serie.length > 10 ? Math.ceil(serie.length / 8) : 1;
    return serie.map((d, i) => ({
      etiqueta: d.dia.slice(8, 10) + '/' + d.dia.slice(5, 7),
      x: i * anchoGrupo,
      altoEntradas: (d.entradas / this.maximo()) * altoUtil,
      altoSalidas: (d.salidas / this.maximo()) * altoUtil,
      entradas: d.entradas,
      salidas: d.salidas,
      // Se cuentan desde el final para que hoy siempre lleve etiqueta y no se monte con la anterior.
      mostrarEtiqueta: (serie.length - 1 - i) % cadaCuantas === 0,
    }));
  });

  protected readonly anchoBarra = computed(() => {
    const n = this.datos()?.porDia.length ?? 1;
    return Math.max(3, Math.min(18, (this.ancho / n) * 0.32));
  });

  protected readonly lineaBase = this.alto - this.margenInferior;

  protected readonly maximoRotacion = computed(() =>
    Math.max(1, ...(this.datos()?.masMovidos ?? []).map((p) => p.salidas + p.entradas)));

  constructor() {
    this.cargar();
  }

  cargar() {
    this.cargando.set(true);
    this.error.set(null);
    this.servicio.resumen(this.dias()).subscribe({
      next: (r) => {
        this.datos.set(r);
        this.cargando.set(false);
      },
      error: (e: unknown) => {
        this.error.set(describirError(e));
        this.cargando.set(false);
      },
    });
  }

  elegirPeriodo(d: 7 | 30) {
    if (d === this.dias()) return;
    this.dias.set(d);
    this.cargar();
  }

  /** Alto de una barra en el viewBox, con un minimo visible cuando hay valor. */
  altoVisible(alto: number, valor: number): number {
    return valor > 0 ? Math.max(alto, 2) : 0;
  }

  porcentaje(valor: number): number {
    return Math.round((valor / this.maximoRotacion()) * 100);
  }
}
