import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Sin sesion no se entra a ninguna pantalla: manda al login y recuerda a donde iba. */
export const authGuard: CanActivateFn = (_ruta, estado) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.autenticado()) return true;

  return router.createUrlTree(['/login'], { queryParams: { volverA: estado.url } });
};

/** Con clave temporal solo se puede ir a cambiarla (la API tambien lo impone con un 403). */
export const claveVigenteGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.debeCambiarClave() ? router.createUrlTree(['/cambiar-clave']) : true;
};

/** Solo el rol ADMIN administra usuarios. */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.esAdmin() ? true : router.createUrlTree(['/inventario']);
};
