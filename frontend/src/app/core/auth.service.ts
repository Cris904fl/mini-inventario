import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { RespuestaLogin } from './modelos';

const CLAVE_SESION = 'kardex.sesion';

interface Sesion {
  token: string;
  expiraEn: string;
  usuario: string;
  nombreCompleto: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly sesion = signal<Sesion | null>(leerSesion());

  readonly usuario = computed(() => this.sesion()?.usuario ?? '');
  readonly nombreCompleto = computed(() => this.sesion()?.nombreCompleto ?? '');
  readonly autenticado = computed(() => {
    const s = this.sesion();
    return !!s && new Date(s.expiraEn).getTime() > Date.now();
  });

  get token(): string | null {
    return this.sesion()?.token ?? null;
  }

  login(usuario: string, clave: string) {
    return this.http
      .post<RespuestaLogin>(`${environment.apiUrl}/auth/login`, { usuario, clave })
      .pipe(tap((r) => this.guardar(r)));
  }

  cerrarSesion(volverA?: string) {
    this.sesion.set(null);
    localStorage.removeItem(CLAVE_SESION);
    this.router.navigate(['/login'], volverA ? { queryParams: { volverA } } : undefined);
  }

  private guardar(r: RespuestaLogin) {
    const sesion: Sesion = { token: r.token, expiraEn: r.expiraEn, usuario: r.usuario, nombreCompleto: r.nombreCompleto };
    this.sesion.set(sesion);
    localStorage.setItem(CLAVE_SESION, JSON.stringify(sesion));
  }
}

function leerSesion(): Sesion | null {
  try {
    const crudo = localStorage.getItem(CLAVE_SESION);
    return crudo ? (JSON.parse(crudo) as Sesion) : null;
  } catch {
    return null;
  }
}
