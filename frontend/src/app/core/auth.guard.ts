import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_ruta, estado) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.autenticado()) return true;

  // Se guarda a donde iba para volver despues del login.
  return router.createUrlTree(['/login'], { queryParams: { volverA: estado.url } });
};
