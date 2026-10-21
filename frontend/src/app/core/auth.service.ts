import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { RespuestaLogin, Rol, Sesion } from './modelos';

const CLAVE_SESION = 'kardex.sesion';

interface SesionGuardada {
  token: string;
  expiraEn: string;
  usuario: string;
  nombreCompleto: string;
  rol: Rol;
  debeCambiarClave: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = `${environment.apiUrl}/auth`;

  private readonly sesion = signal<SesionGuardada | null>(leerSesion());

  readonly usuario = computed(() => this.sesion()?.usuario ?? '');
  readonly nombreCompleto = computed(() => this.sesion()?.nombreCompleto ?? '');
  readonly rol = computed<Rol | null>(() => this.sesion()?.rol ?? null);
  readonly esAdmin = computed(() => this.rol() === 'ADMIN');
  readonly debeCambiarClave = computed(() => this.sesion()?.debeCambiarClave ?? false);
  readonly autenticado = computed(() => {
    const s = this.sesion();
    return !!s && new Date(s.expiraEn).getTime() > Date.now();
  });

  get token(): string | null {
    return this.sesion()?.token ?? null;
  }

  login(usuario: string, clave: string) {
    return this.http
      .post<RespuestaLogin>(`${this.api}/login`, { usuario, clave })
      .pipe(tap((r) => this.guardar(r)));
  }

  /**
   * Confirma con la API que el token guardado sigue siendo valido (el usuario
   * existe y esta activo) y refresca rol y estado de la clave. Si responde 401,
   * el interceptor cierra la sesion.
   */
  validarSesion() {
    return this.http.get<Sesion>(`${this.api}/sesion`).pipe(
      tap((s) => {
        const actual = this.sesion();
        if (actual) this.guardar({ ...actual, nombreCompleto: s.nombreCompleto, rol: s.rol, debeCambiarClave: s.debeCambiarClave });
      }),
    );
  }

  /** Cambia la clave y guarda el token nuevo, ya sin la marca de clave temporal. */
  cambiarClave(claveActual: string, claveNueva: string) {
    return this.http
      .post<RespuestaLogin>(`${this.api}/cambiar-clave`, { claveActual, claveNueva })
      .pipe(tap((r) => this.guardar(r)));
  }

  cerrarSesion(volverA?: string) {
    this.sesion.set(null);
    localStorage.removeItem(CLAVE_SESION);
    this.router.navigate(['/login'], volverA ? { queryParams: { volverA } } : undefined);
  }

  private guardar(r: SesionGuardada) {
    const sesion: SesionGuardada = {
      token: r.token, expiraEn: r.expiraEn, usuario: r.usuario, nombreCompleto: r.nombreCompleto,
      rol: r.rol, debeCambiarClave: r.debeCambiarClave,
    };
    this.sesion.set(sesion);
    localStorage.setItem(CLAVE_SESION, JSON.stringify(sesion));
  }
}

function leerSesion(): SesionGuardada | null {
  try {
    const crudo = localStorage.getItem(CLAVE_SESION);
    return crudo ? (JSON.parse(crudo) as SesionGuardada) : null;
  } catch {
    return null;
  }
}
