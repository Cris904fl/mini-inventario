import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { Shell } from './layout/shell';
import { LoginPage } from './pages/login/login';
import { InventarioPage } from './pages/inventario/inventario';
import { MovimientosPage } from './pages/movimientos/movimientos';
import { HistorialPage } from './pages/historial/historial';

export const routes: Routes = [
  { path: 'login', component: LoginPage, title: 'Ingresar · Kárdex' },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'inventario', pathMatch: 'full' },
      { path: 'inventario', component: InventarioPage, title: 'Inventario · Kárdex' },
      { path: 'movimientos', component: MovimientosPage, title: 'Registrar movimiento · Kárdex' },
      { path: 'historial', component: HistorialPage, title: 'Historial · Kárdex' },
    ],
  },
  { path: '**', redirectTo: 'inventario' },
];
