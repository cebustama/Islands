# Arranque: NoiseLab v4 (ver varias capas a la vez, nubes)

Fecha: 2026-11-02. Rama: `main`. Sigue a `planning/archive/NoiseLab_v3_Startup.md`.

## Material
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`: la escena (sample, IMGUI, unas 2.000 líneas).
  Autoridad: el código.
- `changelog-ssot.md`, entrada «NoiseLab v3»: qué hay, mediciones y límites conocidos.
- `planning/archive/NoiseLab_v3_Startup.md`, sección «Avance»: decisiones y correcciones de la v3.
- `research/playlist-pcg/backlog.md` y `planning/active/NoiseLab_v3_Inputs.md`: candidatos
  que siguen abiertos (investigación, no autoridad).

## Verificado (con evidencia)
- NoiseLab v3 cerrada (changelog; suite 415/415 por el puente el 2026-11-02 a las 15:36).
  Claudio la probó en el editor y en el S22.
- Flujo de `Update`: Sample → Measure → Calibrate → Flood → Climate → Paint. Caché por campo:
  cambiar curvas o clima no vuelve a muestrear.
- S22 (export de Perf, 2026-11-02): 7 ms por muestreo a 64² al arrastrar (60 fps); 20 ms a
  128²; unos 85 ms a 256² con 5 campos. Editor (Mono): unos 1,6 s al soltar.
- Las octavas de temperatura y humedad pasan a 4 por defecto: con 2, los bordes de bioma
  salían como bandas largas casi rectas (prueba de Claudio con Noise = 1 y Coast = 0).
- El código `NL1` escribe todos los campos que usa; los ajustes de la v3 se reinician antes
  de aplicar un código.

## Sin verificar
- Coste de pintar 4 paneles: la estimación es que la pintura es barata frente al muestreo, que
  se comparte. Medir con Perf en el S22.
- Si las bandas rectas desaparecen del todo con 4 octavas en la seed y el código de la captura
  original. Claudio vio que «se nota un poco más el cambio», no lo comparó con esa captura.

## Candidatos (aceptados por Claudio como dirección, en este orden)
1. **Vista dividida 2×2:** cada panel con su capa (mapa, altura, temperatura, humedad,
   ruidos, viento). Arrastrar mueve todos los paneles a la vez. En vertical, 2×2 encima de
   los controles; en horizontal, a la izquierda.
2. **Capas superpuestas:** una lista estilo inspector con +/− y una casilla por capa, y
   opacidad, sobre el mapa normal.
3. **Nubes animadas:** `Simplex3D` con z = tiempo, desplazadas con el viento y disueltas sobre
   las cumbres (el aire que sube y pierde agua). Los vórtices de von Kármán detrás de islas
   altas necesitarían simular fluidos (Stam, 1999): fuera de la v4 salvo decisión.
4. Bandas por altura absoluta como alternativa a los porcentajes (las montañas «más altas» se
   verían en el color, no solo en el sombreado).

## Decisiones (aceptadas por Claudio el 2026-11-02; no volver a preguntarlas)
1. **Alcance:** primero la vista dividida y después las capas superpuestas (las dos parten de
   «elegir capa», que ya existe en Map → Layer). Las nubes, solo si alcanza.
2. **Paneles:** selector de vista 1 / 1×2 / 2×2. El ruido se muestrea una vez y se comparte;
   cada panel solo repinta su capa. Arrastrar mueve todos los paneles a la vez.
3. **Código:** clave nueva `v=` con la vista y la capa de cada panel (por ejemplo
   `v=4:map,height,moisture,wind`) y `ov=` para las capas superpuestas (capa y opacidad). Un
   código sin estas claves abre un panel, como hoy.
4. **Capas superpuestas:** lista bajo Layer, con una casilla y una opacidad por capa, y +/−
   para añadir y quitar. Se mezclan en orden sobre el mapa base. Las curvas de nivel pasan a
   ser una capa más de la lista, con sus ajustes actuales.
5. **Nubes (si alcanza):** una textura de nubes aparte, de baja resolución, encima del mapa,
   sin tocar las cachés del terreno (animarlas obliga a repintar en cada fotograma).

## Fuera de alcance
Burst, UI Toolkit, pasar el viento o la distancia euclídea a `Stage_Biome2D` (cambio gobernado:
se mueven goldens), arreglar `Samples~`, Active Input Handling, fluidos.

## No rehacer
v1 a v3 (bandas, paletas, código, Perf, splines, biomas, viento, distancia euclídea, curvas de
nivel), la build Android y las mediciones.

## Ritual de arranque
1. `git log --oneline -8` y `git status` en `D:\Projects\Islands`.
2. Con Unity abierto y fuera de Play, `ClaudeTests/run.trigger` vacío en el anfitrión: 415/415.
3. Leer `NoiseLab.cs` por secciones: `Update`, `Paint`, la UI de la pestaña Map y `BuildCode`.
4. Confirmar en 5 líneas el plan del primer paso (vista dividida) y empezar; las decisiones ya
   están tomadas. Al terminar cada paso, Claudio lo prueba en el editor antes del siguiente.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (lo único que se toca).
- `Documentation~/changelog-ssot.md`, `CURRENT_STATE.md` (al cerrar).
- Explainers de la web (otra sesión):
  `web/claudiobustamante-site/Documentation~/planning/active/Splines_Biomes_Explainers_Startup.md`.
