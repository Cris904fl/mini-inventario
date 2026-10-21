import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

/**
 * Marco de la aplicacion: barra lateral en escritorio, barra superior y
 * pestañas inferiores en movil. Las paginas se pintan en el outlet.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly auth = inject(AuthService);

  protected readonly secciones = [
    { ruta: '/inventario', nombre: 'Inventario', icono: 'inventario' },
    { ruta: '/movimientos', nombre: 'Movimiento', icono: 'movimiento' },
    { ruta: '/historial', nombre: 'Historial', icono: 'historial' },
  ];

  salir() {
    this.auth.cerrarSesion();
  }
}
