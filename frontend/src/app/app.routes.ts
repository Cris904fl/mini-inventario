import { Routes } from '@angular/router';
import { adminGuard, authGuard, claveVigenteGuard } from './core/auth.guard';
import { Shell } from './layout/shell';
import { LoginPage } from './pages/login/login';
import { InventarioPage } from './pages/inventario/inventario';
import { MovimientosPage } from './pages/movimientos/movimientos';
import { HistorialPage } from './pages/historial/historial';
import { UsuariosPage } from './pages/usuarios/usuarios';
import { CambiarClavePage } from './pages/cambiar-clave/cambiar-clave';

export const routes: Routes = [
  { path: 'login', component: LoginPage, title: 'Ingresar · Kárdex' },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],          // sin sesion, ninguna de estas rutas abre
    children: [
      { path: '', redirectTo: 'inventario', pathMatch: 'full' },
      { path: 'cambiar-clave', component: CambiarClavePage, title: 'Cambiar clave · Kárdex' },
      { path: 'inventario', component: InventarioPage, canActivate: [claveVigenteGuard], title: 'Inventario · Kárdex' },
      { path: 'movimientos', component: MovimientosPage, canActivate: [claveVigenteGuard], title: 'Registrar movimiento · Kárdex' },
      { path: 'historial', component: HistorialPage, canActivate: [claveVigenteGuard], title: 'Historial · Kárdex' },
      { path: 'usuarios', component: UsuariosPage, canActivate: [claveVigenteGuard, adminGuard], title: 'Usuarios · Kárdex' },
    ],
  },
  { path: '**', redirectTo: 'inventario' },
];
