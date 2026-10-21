import { HttpErrorResponse } from '@angular/common/http';
import { describirError } from './problema';

describe('describirError', () => {
  it('usa el titulo y el detalle del ProblemDetails de la API', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Stock insuficiente', detail: 'hay 5 y se intentan retirar 9', codigo: 'IN001' },
    });

    expect(describirError(error)).toEqual({
      titulo: 'Stock insuficiente',
      detalle: 'hay 5 y se intentan retirar 9',
      codigo: 'IN001',
    });
  });

  it('junta los errores de validacion en un solo texto', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { errors: { Cantidad: ['La cantidad debe ser mayor que cero.'] } },
    });

    expect(describirError(error).detalle).toContain('mayor que cero');
  });

  it('explica cuando no hay conexion con la API', () => {
    expect(describirError(new HttpErrorResponse({ status: 0 })).titulo).toBe('Sin conexion con la API');
  });
});
