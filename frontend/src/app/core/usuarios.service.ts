import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { NuevoUsuario, UsuarioEnLista } from './modelos';

@Injectable({ providedIn: 'root' })
export class UsuariosService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/usuarios`;

  listar() {
    return this.http.get<UsuarioEnLista[]>(this.api);
  }

  crear(usuario: NuevoUsuario) {
    return this.http.post<UsuarioEnLista>(this.api, usuario);
  }

  cambiarEstado(id: number, activo: boolean) {
    return this.http.patch<void>(`${this.api}/${id}/estado`, { activo });
  }

  restablecerClave(id: number, claveTemporal: string) {
    return this.http.post<void>(`${this.api}/${id}/restablecer-clave`, { claveTemporal });
  }
}
