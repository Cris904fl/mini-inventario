import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TemaService } from './core/tema.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {
  // Se inyecta al arrancar para que el tema guardado se aplique en cualquier pantalla.
  private readonly tema = inject(TemaService);
}
