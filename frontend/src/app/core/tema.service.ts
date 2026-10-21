import { Injectable, effect, signal } from '@angular/core';

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

  readonly opciones: { valor: Tema; nombre: string }[] = [
    { valor: 'sistema', nombre: 'Sistema' },
    { valor: 'claro', nombre: 'Claro' },
    { valor: 'oscuro', nombre: 'Oscuro' },
  ];

  constructor() {
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
}

function leerTema(): Tema {
  try {
    const t = localStorage.getItem(CLAVE_TEMA);
    return t === 'claro' || t === 'oscuro' ? t : 'sistema';
  } catch {
    return 'sistema';
  }
}
