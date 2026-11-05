# Arranque: NoiseLab v5 (alcance por decidir)

Fecha: 2026-11-05. Rama: `main`. Sigue a `planning/archive/NoiseLab_v4_Startup.md`.

## Material
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`: la escena (sample, IMGUI, unas 2.500 líneas).
  Autoridad: el código.
- `changelog-ssot.md`, entrada «NoiseLab v4»: qué hay, mediciones y límites conocidos.
- `planning/archive/NoiseLab_v4_Startup.md`, secciones «Candidatos» y «Avance».
- `research/2026-11-05-mundo-finito-o-infinito.md`: mundo infinito frente a planeta, cascada
  de escalas (investigación, no autoridad).
- `planning/active/Fluids_Module_Design.md`: propuesta del módulo de fluidos (no autoridad).

## Verificado (con evidencia)
- NoiseLab v4 cerrada: vista dividida (`v=`), capas superpuestas (`ov=`), nubes (`cu=`),
  tooltips, Perf export v2. Commits `c6e07fb`, `bc6141b`, `0c75bbe`. Suite 415/415 (puente,
  2026-11-05 17:29). Claudio la probó en el editor y en el S22.
- S22 (export 2026-11-05 17:25): pintar 4 paneles 0,4 ms; nubes 7,5 ms por fotograma
  (96², 5 octavas); muestreo al soltar 62 ms (256², 3 campos).

## Sin verificar
- Que bajar las nubes a 4 octavas o a 64² las deje en unos 3–4 ms sin que se vean borrosas.

## Candidatos (por decidir con Claudio al arrancar)
1. **Viento y costa a escala de mundo** (recomendado primero): hoy el viento entra saturado por
   el borde de la vista y la distancia a la costa no ve el agua de fuera, así que la sombra de
   lluvia cambia al arrastrar. Opciones: un margen muestreado hacia el viento, o calcular viento
   y costa en la rejilla de referencia del mundo (128²) e interpolar. Es la base de los fluidos.
2. **Bandas por altura absoluta** como alternativa a los porcentajes.
3. **Fluidos** (viento que rodea el relieve, corrientes, nubes y deriva más físicas):
   `Fluids_Module_Design.md`. El más grande.
4. **Quickselect** para el umbral de agua por porcentaje (desde el lab de cuantiles de la web).
5. **Coste de las nubes** (ver «Sin verificar»).

## Fuera de alcance (salvo decisión)
Burst, UI Toolkit, pasar viento o distancia euclídea a `Stage_Biome2D` (cambio gobernado: se
mueven goldens), arreglar `Samples~`, Active Input Handling.

## No rehacer
v1 a v4 (bandas, paletas, código, Perf, splines, biomas, viento, curvas de nivel, vista
dividida, capas, nubes, tooltips), la build Android y las mediciones.

## Ritual de arranque
1. `git log --oneline -8` y `git status` en `D:\Projects\Islands`.
2. Con Unity abierto y fuera de Play, `ClaudeTests/run.trigger` vacío en el anfitrión: 415/415.
3. Preguntar a Claudio qué candidatos entran y en qué orden (recomendación: 1, luego 5).
4. Leer en `NoiseLab.cs` lo que toque el primer paso (para el 1: `WindField`, la distancia a
   la costa y la referencia del mundo, `Calibrate`).
5. Al terminar cada paso, Claudio lo prueba en el editor antes del siguiente.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`.
- `Documentation~/changelog-ssot.md`, `CURRENT_STATE.md` (al cerrar).
