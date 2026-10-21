import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { MensajeDeError, describirError } from '../../core/problema';

@Component({
  selector: 'app-cambiar-clave',
  imports: [FormsModule, RouterLink],
  templateUrl: './cambiar-clave.html',
  styleUrl: './cambiar-clave.css',
})
export class CambiarClavePage {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly claveActual = signal('');
  protected readonly claveNueva = signal('');
  protected readonly confirmacion = signal('');
  protected readonly enviando = signal(false);
  protected readonly error = signal<MensajeDeError | null>(null);
  protected readonly listo = signal(false);

  /** Mensaje de validacion local; null cuando el formulario esta bien. */
  protected readonly aviso = computed(() => {
    const nueva = this.claveNueva();
    if (!nueva) return null;
    if (nueva.length < 8) return 'La clave nueva debe tener al menos 8 caracteres.';
    if (nueva === this.claveActual()) return 'La clave nueva debe ser distinta de la actual.';
    if (this.confirmacion() && this.confirmacion() !== nueva) return 'La confirmación no coincide.';
    return null;
  });

  protected readonly puedeEnviar = computed(() =>
    !!this.claveActual() && !!this.claveNueva() && this.confirmacion() === this.claveNueva() && !this.aviso() && !this.enviando());

  cambiar() {
    if (!this.puedeEnviar()) return;
    this.error.set(null);
    this.enviando.set(true);

    this.auth.cambiarClave(this.claveActual(), this.claveNueva()).subscribe({
      next: () => {
        this.listo.set(true);
        this.enviando.set(false);
        setTimeout(() => this.router.navigateByUrl('/inventario'), 900);
      },
      error: (e: unknown) => {
        this.error.set(describirError(e));
        this.enviando.set(false);
      },
    });
  }
}
