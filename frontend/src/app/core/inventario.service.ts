import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import {
  Movimiento, NuevoProducto, Pagina, Producto, RespuestaMovimiento, SolicitudMovimiento, TipoMovimiento,
} from './modelos';

export interface FiltroHistorial {
  productoId?: number | null;
  tipo?: TipoMovimiento | null;
  pagina?: number;
  tamano?: number;
}

@Injectable({ providedIn: 'root' })
export class InventarioService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  inventario(busqueda = '', soloBajo = false) {
    let params = new HttpParams();
    if (busqueda.trim()) params = params.set('busqueda', busqueda.trim());
    if (soloBajo) params = params.set('soloBajo', 'true');
    return this.http.get<Producto[]>(`${this.api}/productos`, { params });
  }

  crearProducto(producto: NuevoProducto) {
    return this.http.post<Producto>(`${this.api}/productos`, producto);
  }

  registrarEntrada(solicitud: SolicitudMovimiento) {
    return this.http.post<RespuestaMovimiento>(`${this.api}/movimientos/entradas`, solicitud);
  }

  registrarSalida(solicitud: SolicitudMovimiento) {
    return this.http.post<RespuestaMovimiento>(`${this.api}/movimientos/salidas`, solicitud);
  }

  historial(filtro: FiltroHistorial = {}) {
    let params = new HttpParams()
      .set('pagina', filtro.pagina ?? 1)
      .set('tamano', filtro.tamano ?? 20);
    if (filtro.productoId) params = params.set('productoId', filtro.productoId);
    if (filtro.tipo) params = params.set('tipo', filtro.tipo);
    return this.http.get<Pagina<Movimiento>>(`${this.api}/movimientos`, { params });
  }
}
