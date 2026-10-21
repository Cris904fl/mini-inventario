import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { describirError } from '../../core/problema';
import { TemaSelector } from '../../layout/tema-selector';

@Component({
  selector: 'app-login',
  imports: [FormsModule, TemaSelector],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);

  protected usuario = '';
  protected clave = '';
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);

  ingresar() {
    if (!this.usuario.trim() || !this.clave) {
      this.error.set('Escribe el usuario y la clave.');
      return;
    }

    this.error.set(null);
    this.enviando.set(true);

    this.auth.login(this.usuario.trim(), this.clave).subscribe({
      next: (r) => {
        // Con clave temporal no se puede ir a ninguna otra pantalla.
        if (r.debeCambiarClave) {
          this.router.navigateByUrl('/cambiar-clave');
          return;
        }
        const volverA = this.ruta.snapshot.queryParamMap.get('volverA');
        this.router.navigateByUrl(volverA && volverA.startsWith('/') ? volverA : '/inventario');
      },
      error: (e: unknown) => {
        const mensaje = describirError(e);
        this.error.set(mensaje.detalle || mensaje.titulo);
        this.enviando.set(false);
      },
    });
  }
}
