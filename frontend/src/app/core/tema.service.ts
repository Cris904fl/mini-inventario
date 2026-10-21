import { Injectable, computed, effect, signal } from '@angular/core';

export type Tema = 'sistema' | 'claro' | 'oscuro';

const CLAVE_TEMA = 'kardex.tema';

/**
 * Tema visual. Por defecto sigue al sistema operativo (prefers-color-scheme);
 * el usuario puede fijar claro u oscuro y la eleccion queda en el navegador.
 * Se aplica con el atributo data-theme en <html>, que leen las variables de
 * styles.css.
 */
@Injectable({ providedIn: 'root' })
export class TemaService {
  readonly tema = signal<Tema>(leerTema());

  /** Lo que prefiere el sistema operativo ahora mismo. */
  private readonly sistemaOscuro = signal(window.matchMedia('(prefers-color-scheme: dark)').matches);

  /** Tema que se esta viendo en realidad: resuelve "sistema". */
  readonly efectivo = computed<'claro' | 'oscuro'>(() => {
    const t = this.tema();
    if (t === 'sistema') return this.sistemaOscuro() ? 'oscuro' : 'claro';
    return t;
  });

  readonly opciones: { valor: Tema; nombre: string }[] = [
    { valor: 'sistema', nombre: 'Sistema' },
    { valor: 'claro', nombre: 'Claro' },
    { valor: 'oscuro', nombre: 'Oscuro' },
  ];

  constructor() {
    window.matchMedia('(prefers-color-scheme: dark)')
      .addEventListener('change', (e) => this.sistemaOscuro.set(e.matches));

    effect(() => {
      const t = this.tema();
      const html = document.documentElement;
      if (t === 'sistema') html.removeAttribute('data-theme');
      else html.setAttribute('data-theme', t === 'claro' ? 'light' : 'dark');
      try {
        localStorage.setItem(CLAVE_TEMA, t);
      } catch {
        /* sin almacenamiento: el tema dura la sesion */
      }
    });
  }

  elegir(t: Tema) {
    this.tema.set(t);
  }

  /** Alterna entre claro y oscuro a partir del que se ve (para el boton sol/luna). */
  alternar() {
    this.tema.set(this.efectivo() === 'oscuro' ? 'claro' : 'oscuro');
  }
}

function leerTema(): Tema {
  try {
    const t = localStorage.getItem(CLAVE_TEMA);
    return t === 'claro' || t === 'oscuro' ? t : 'sistema';
  } catch {
    return 'sistema';
  }
}
