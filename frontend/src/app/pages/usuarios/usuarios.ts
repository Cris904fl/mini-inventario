import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth.service';
import { NuevoUsuario, UsuarioEnLista } from '../../core/modelos';
import { MensajeDeError, describirError } from '../../core/problema';
import { UsuariosService } from '../../core/usuarios.service';

@Component({
  selector: 'app-usuarios',
  imports: [FormsModule, DatePipe],
  templateUrl: './usuarios.html',
  styleUrl: './usuarios.css',
})
export class UsuariosPage {
  private readonly servicio = inject(UsuariosService);
  protected readonly auth = inject(AuthService);

  protected readonly usuarios = signal<UsuarioEnLista[]>([]);
  protected readonly cargando = signal(true);
  protected readonly error = signal<MensajeDeError | null>(null);
  protected readonly mensaje = signal<string | null>(null);

  // Alta
  protected readonly mostrarAlta = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorAlta = signal<MensajeDeError | null>(null);
  protected nuevo: NuevoUsuario = this.usuarioVacio();

  // Restablecer clave: id del usuario con el cuadro abierto y la clave temporal escrita.
  protected readonly restableciendo = signal<number | null>(null);
  protected claveTemporal = '';
  protected readonly errorRestablecer = signal<MensajeDeError | null>(null);

  // Id del usuario cuya fila esta esperando respuesta de la API.
  protected readonly ocupado = signal<number | null>(null);

  constructor() {
    this.cargar();
  }

  cargar() {
    this.cargando.set(true);
    this.error.set(null);
    this.servicio.listar().subscribe({
      next: (lista) => {
        this.usuarios.set(lista);
        this.cargando.set(false);
      },
      error: (e: unknown) => {
        this.error.set(describirError(e));
        this.cargando.set(false);
      },
    });
  }

  abrirAlta() {
    this.nuevo = this.usuarioVacio();
    this.errorAlta.set(null);
    this.mensaje.set(null);
    this.mostrarAlta.set(true);
  }

  guardarUsuario() {
    this.errorAlta.set(null);
    this.guardando.set(true);
    this.servicio.crear({ ...this.nuevo, nombreUsuario: this.nuevo.nombreUsuario.trim(), nombreCompleto: this.nuevo.nombreCompleto.trim() }).subscribe({
      next: (u) => {
        this.mensaje.set(`Usuario ${u.nombreUsuario} creado con clave temporal; deberá cambiarla en su primer ingreso.`);
        this.guardando.set(false);
        this.mostrarAlta.set(false);
        this.cargar();
      },
      error: (e: unknown) => {
        this.errorAlta.set(describirError(e));
        this.guardando.set(false);
      },
    });
  }

  cambiarEstado(u: UsuarioEnLista) {
    this.error.set(null);
    this.mensaje.set(null);
    this.ocupado.set(u.id);
    this.servicio.cambiarEstado(u.id, !u.activo).subscribe({
      next: () => {
        this.mensaje.set(`${u.nombreUsuario} ${u.activo ? 'desactivado' : 'activado'}.`);
        this.ocupado.set(null);
        this.cargar();
      },
      error: (e: unknown) => {
        // Aqui llega el 409 de "no puedes desactivarte" o "ultimo administrador".
        this.error.set(describirError(e));
        this.ocupado.set(null);
      },
    });
  }

  abrirRestablecer(u: UsuarioEnLista) {
    this.claveTemporal = '';
    this.errorRestablecer.set(null);
    this.mensaje.set(null);
    this.restableciendo.set(u.id);
  }

  cancelarRestablecer() {
    this.restableciendo.set(null);
  }

  restablecer(u: UsuarioEnLista) {
    if (this.claveTemporal.length < 8) {
      this.errorRestablecer.set({ titulo: 'Clave muy corta', detalle: 'La clave temporal debe tener al menos 8 caracteres.' });
      return;
    }
    this.errorRestablecer.set(null);
    this.ocupado.set(u.id);
    this.servicio.restablecerClave(u.id, this.claveTemporal).subscribe({
      next: () => {
        this.mensaje.set(`Clave de ${u.nombreUsuario} restablecida. Entrégale la clave temporal; se le pedirá cambiarla al ingresar.`);
        this.restableciendo.set(null);
        this.ocupado.set(null);
        this.cargar();
      },
      error: (e: unknown) => {
        this.errorRestablecer.set(describirError(e));
        this.ocupado.set(null);
      },
    });
  }

  private usuarioVacio(): NuevoUsuario {
    return { nombreUsuario: '', nombreCompleto: '', rol: 'OPERADOR', claveTemporal: '' };
  }
}
