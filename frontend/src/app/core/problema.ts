import { HttpErrorResponse } from '@angular/common/http';
import { Problema } from './modelos';

export interface MensajeDeError {
  titulo: string;
  detalle: string;
  codigo?: string;
}

/**
 * Convierte cualquier error HTTP en un titulo y un detalle que se pueden
 * mostrar tal cual. Prioriza lo que dice la API (ProblemDetails) y cae en un
 * texto generico solo cuando no hay respuesta (red caida, CORS).
 */
export function describirError(error: unknown): MensajeDeError {
  if (error instanceof HttpErrorResponse) {
    const problema = (typeof error.error === 'object' ? error.error : null) as Problema | null;

    if (problema?.errors) {
      const detalles = Object.values(problema.errors).flat();
      return { titulo: 'Revisa los datos', detalle: detalles.join(' '), codigo: problema.codigo };
    }

    if (problema?.title || problema?.detail) {
      return {
        titulo: problema.title ?? 'No se pudo completar',
        detalle: problema.detail ?? '',
        codigo: problema.codigo,
      };
    }

    if (error.status === 0) {
      return { titulo: 'Sin conexion con la API', detalle: 'Verifica que el backend este corriendo.' };
    }

    if (error.status === 401) {
      return { titulo: 'Sesion no valida', detalle: 'Vuelve a iniciar sesion.' };
    }

    return { titulo: `Error ${error.status}`, detalle: error.statusText || 'Intenta de nuevo.' };
  }

  return { titulo: 'Error inesperado', detalle: 'Intenta de nuevo.' };
}
