import { Component, computed, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { TemaSelector } from './tema-selector';

interface Seccion {
  ruta: string;
  nombre: string;
  icono: 'resumen' | 'inventario' | 'movimiento' | 'historial' | 'usuarios';
}

/**
 * Marco de la aplicacion: barra lateral en escritorio, barra superior y
 * pestañas inferiores en movil. Las paginas se pintan en el outlet.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgTemplateOutlet, TemaSelector],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly auth = inject(AuthService);

  private readonly todas: Seccion[] = [
    { ruta: '/resumen', nombre: 'Resumen', icono: 'resumen' },
    { ruta: '/inventario', nombre: 'Inventario', icono: 'inventario' },
    { ruta: '/movimientos', nombre: 'Movimiento', icono: 'movimiento' },
    { ruta: '/historial', nombre: 'Historial', icono: 'historial' },
    { ruta: '/usuarios', nombre: 'Usuarios', icono: 'usuarios' },
  ];

  /** Usuarios solo aparece para ADMIN; con clave temporal no hay navegacion. */
  protected readonly secciones = computed<Seccion[]>(() =>
    this.auth.debeCambiarClave() ? [] : this.todas.filter((s) => s.ruta !== '/usuarios' || this.auth.esAdmin()));

  constructor() {
    // Al abrir la aplicacion con una sesion guardada se confirma con la API que
    // el token sigue valido; un 401 lo atiende el interceptor cerrando la sesion.
    this.auth.validarSesion().subscribe({ error: () => undefined });
  }

  salir() {
    this.auth.cerrarSesion();
  }
}
