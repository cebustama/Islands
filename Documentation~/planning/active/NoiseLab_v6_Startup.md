# Arranque: NoiseLab v6 (fluidos: viento que rodea el relieve, corrientes marinas)

Fecha: 2026-11-06. Rama: `main`. Sigue a `planning/archive/NoiseLab_v5_Startup.md`.

## Material
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`: la escena (sample, IMGUI, unas 3.200 líneas,
  con los jobs de Burst al final del archivo). Autoridad: el código.
- `changelog-ssot.md`, entrada «NoiseLab v5»: qué hay, mediciones y límites conocidos.
- `planning/archive/NoiseLab_v5_Startup.md`, sección «Avance».
- `planning/active/Fluids_Module_Design.md`: objetivo (cuatro usos), decisión del relieve
  (opción b: el aire rodea lo que pasa de un umbral de altura y sube lo de debajo), candidatos
  A–E, protocolo de medición, decisiones abiertas. Propuesta, no autoridad.
- `research/2026-11-05-lague-fluidos.md` y `research/fluidos-lague/` (`tutorial.html`, sección
  15.2 para el determinismo; `notas-tecnicas.md`; `test-demos.js`): variante «proyección única»
  con rojo-negro + SOR, con código de referencia en JS. Investigación, no autoridad.
- `research/2026-11-05-mundo-finito-o-infinito.md`: cascada de escalas, margen hacia el viento.

## Verificado (con evidencia)
- NoiseLab v5 cerrada (changelog; suite 415/415 por el puente el 2026-11-06 a las 14:35).
  Claudio la probó en el editor y en el S22. Commits `0cf2af4`, `1d80144`, `8890317`, `caa852d`.
- El clima (costa, rango, viento) corre sobre la vista + un margen (`Margins`,
  `SampleMargins`, rejilla `extW × extH`); Edge test: 0,00 % de biomas cambian al arrastrar.
- Muestreo, nubes y clima en jobs de Burst con `FloatMode.Strict`; interruptor Burst y Burst
  check en Perf. S22: 0 de 327.680 valores de ruido difieren; muestreo al soltar 13–35 ms a
  256² con margen; nubes ~2 ms por fotograma; clima 18,5 ms (rejilla 684×512).
- Claudio acepta un nivel pequeño de diferencia numérica entre PC, móvil y web.

## Sin verificar
- Que un solve de presión (rojo-negro + SOR, iteraciones fijas) quepa en el presupuesto del S22
  a la resolución del clima. El JS de Lague no se ha medido en C#/Burst.
- Que el rojo-negro en paralelo sea determinista entre ejecuciones (debería: cada color solo lee
  el otro); comprobarlo con dos ejecuciones iguales y con el Burst check.
- Que la variante «proyección única» dé calmas y estelas creíbles detrás de islas altas.

## Candidatos (alcance aceptado por Claudio el 2026-11-06; orden por confirmar al arrancar)
1. **Viento que rodea el relieve** (uso 1 del diseño). Máscara de sólidos por umbral de altura
   (deslizador), viento de fondo, y el campo con la variante «proyección única» frente a C (ψ ×
   rampa + curl) y A (el barrido de hoy), en la vista dividida. Lo que cambia de fondo: con un
   viento que varía de celda a celda, la humedad ya no se puede barrer en una dirección; hay que
   transportarla (advección semi-lagrangiana con pasos fijos o barrido rápido; decidir).
2. **Corrientes marinas** (uso 2, pedido por Claudio). El mismo solver con toda la tierra como
   obstáculo y un viento de fondo por latitud (alisios y oestes, candidato B) que forma giros.
   La corriente transporta temperatura del mar; una corriente fría a lo largo de una costa
   enfría y seca la costa (desierto costero y niebla: Atacama y la corriente de Humboldt como
   regla, no como truco). Cómo lee el clima la temperatura del mar: decidir.
3. **Bandas por altura absoluta** como alternativa a los porcentajes (pequeño).
4. **Quickselect** para el umbral de agua por porcentaje (pequeño).

## Decisiones abiertas (antes del código que dependa de ellas)
- Resolución del fluido: la del clima, o más gruesa (p. ej. la mitad) interpolada a la vista.
- Presión frente a ψ: con varias islas, ψ necesita una constante por isla; la presión no.
- Transporte de la humedad y de la temperatura del mar (ver 1 y 2).
- Si se conserva el viento de la v3 (A) como opción del menú para comparar.

## Fuera de alcance (salvo decisión)
Fluidos con paso de tiempo (Stam, candidato E) y vórtices de Kármán; llevar algo de esto al
pipeline (`Stage_Biome2D`, `MapFieldId`, `Runtime/Fluids/`: cambio gobernado, mueve goldens);
mundo esférico; UI Toolkit; arreglar `Samples~` y Active Input Handling; rastrear el aviso
«Leak Detected» del editor (no es de NoiseLab).

## No rehacer
v1 a v5 (bandas, paletas, código, Perf, splines, biomas, viento de una dirección, curvas de
nivel, vista dividida, capas, nubes, tooltips, margen del clima, jobs de Burst, scroll), la
build Android y las mediciones.

## Ritual de arranque
1. `git log --oneline -8` y `git status` en `D:\Projects\Islands`.
2. Con Unity abierto y fuera de Play, `ClaudeTests/run.trigger` vacío en el anfitrión: 415/415.
3. Leer `Fluids_Module_Design.md` y, del tutorial de Lague, la proyección de presión y la
   sección 15.2 (determinismo).
4. Preguntar a Claudio el orden de los candidatos (recomendación: 1, luego 2; 3 y 4 al final si
   sobra) y resolver las decisiones abiertas que toque el primer paso.
5. Leer en `NoiseLab.cs` lo que toca el paso 1: `Margins`, `SampleMargins`, `ClimateBurst`,
   `WindJob`, `ViewClimateJob`, `ClimateManaged`, y la capa de viento del menú.
6. Al terminar cada paso, Claudio lo prueba en el editor y en el S22 (export de Perf, que el
   agente lee con adb) antes del siguiente.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs`.
- `Documentation~/planning/active/Fluids_Module_Design.md`.
- `Documentation~/research/fluidos-lague/tutorial.html`, `notas-tecnicas.md`.
- `Documentation~/changelog-ssot.md`, `CURRENT_STATE.md` (al cerrar).

## Decisiones (Claudio, 2026-11-09, al arrancar)
- Orden: 1 (viento que rodea el relieve), 2 (corrientes marinas), luego 3–4 si sobra.
- Resolución del fluido: la mitad de la rejilla del clima, interpolada a la vista; medir
  también a resolución completa en el S22.
- Método del paso 1: «proyección única» (presión, rojo-negro + SOR) y el barrido de la v3 (A)
  como opción del menú para comparar. C (ψ × rampa) queda para después si hace falta.
- Transporte de la humedad: barrido rápido (fast sweeping, Zhao 2005), en un hilo como `WindJob`.
- Arranque: suite 415/415 por el puente (2026-11-09 09:49), git limpio en `6c49239`.
- Tangente apuntada (Claudio: «para después»): dos capas de nubes (baja que sigue el viento
  que rodea, alta que lo ignora) y la Alpujarra como prueba de relieve, en
  `research/2026-11-09-alpujarra-nubes-y-relieve.md`.
- Cambio (Claudio, 2026-11-09, tras medir la 1a): la rejilla del fluido pasa a **1/4** del
  clima, con menos pasadas al arrastrar. La multirejilla, para la v7.

## Progreso
- **1a hecha** (`4de55fc`, 1a + 1a.2): proyección de presión, capa *Wind flow*, Burst check y
  Edge test. Probada en el editor por Claudio.
- **1a.3** (rejilla a 1/4, 200 pasadas, un cuarto al arrastrar): editor 31,5 ms, divergencia
  0,107 → 0,017, Edge test viento Δ media 0,041, máx 0,64; Burst dos veces idéntico. S22: flujo
  8,9 ms, divergencia 0,101 → 0,009, Edge test viento Δ media 0,135, máx 1,06.
- **Paso de interfaz** (`30c06a0`): panel a pantalla completa, códigos guardados, latitud y
  medias de la vista. Probado en el editor y el S22.
- **1b hecha** (la humedad sigue al flujo por barrido rápido, 3 rondas; el flujo corre dentro
  del clima). Probada por Claudio en el editor y el S22. S22: aire 62–102 ms (el flujo, 9–11 ms;
  clima total ~125 ms), cambio máximo de la última ronda 0,22–0,29 (no converge del todo); Edge
  test con margen: humedad Δ media 0,009, máx 0,18, biomas cambiados 3,5 % (sin margen 18,6 %);
  Burst check: humedad 1,8e-7, aire 3e-7, caras del flujo 0 distintas.
- **1b.2 hecha** (`aa7896d`): el aire en la rejilla del flujo con volúmenes finitos sobre las
  caras, hasta que se asienta (1e-4, tope 64 rondas). S22 (export 2026-11-10 14:26): aire 29
  rondas, 23,3 ms (antes 62–102), 0 celdas sin asentar; clima 53,5 ms (antes ~125); Edge test
  con margen: biomas 0,30 % (antes 3,5 %); Burst check: 0 diferencias en flujo y aire.
  Editor: C# frente a Burst difieren en el aire hasta 2,8e-4 (las caras ya difieren en el
  editor). **Paso 1 cerrado.**
- **Paso de interfaz 2:** Code → *Set as default* / *Clear default* (PlayerPrefs, por
  dispositivo), *Copy results* en Perf y *Copy* en el resumen del viento; el export de Perf
  lleva la línea del flujo. Probado en el editor y el S22.
- **Idea de Claudio (2026-11-10), fuera de la v6:** una escena índice (menú principal) para
  elegir entre labs, cada uno en su escena y su código; primer lab nuevo: placas tectónicas
  con Voronoi y su movimiento, que guíen cordilleras y quebradas, para integrarlo después
  como fase del mundo (enlaza con la fase K de `PCG_Roadmap.md` y `Stage_Tectonics2D` del
  backlog de la playlist).
