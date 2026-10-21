// Contratos que devuelve la API (ver backend/Inventario.Api/Contratos).

export type Estado = 'DISPONIBLE' | 'BAJO' | 'AGOTADO';
export type TipoMovimiento = 'ENTRADA' | 'SALIDA';

export interface Producto {
  id: number;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  unidad: string;
  cantidad: number;
  stockMinimo: number;
  estado: Estado;
  ultimoMovimiento: string | null;
}

export interface NuevoProducto {
  codigo: string;
  nombre: string;
  descripcion?: string;
  unidad: string;
  stockMinimo: number;
  cantidadInicial: number;
}

export interface SolicitudMovimiento {
  productoId: number;
  cantidad: number;
  observacion?: string;
}

export interface RespuestaMovimiento {
  movimientoId: number;
  productoId: number;
  tipo: TipoMovimiento;
  cantidad: number;
  stockResultante: number;
  usuario: string;
}

export interface Movimiento {
  id: number;
  fecha: string;
  productoId: number;
  codigo: string;
  producto: string;
  tipo: TipoMovimiento;
  cantidad: number;
  stockResultante: number;
  usuario: string;
  observacion: string | null;
}

export interface Pagina<T> {
  elementos: T[];
  total: number;
  pagina: number;
  tamano: number;
  totalPaginas: number;
}

export interface RespuestaLogin {
  token: string;
  expiraEn: string;
  usuario: string;
  nombreCompleto: string;
}

/** Cuerpo de error de la API (ProblemDetails, RFC 9457) mas las extensiones propias. */
export interface Problema {
  title?: string;
  detail?: string;
  status?: number;
  codigo?: string;
  stock?: { disponible: number; solicitado: number };
  errors?: Record<string, string[]>;
}
