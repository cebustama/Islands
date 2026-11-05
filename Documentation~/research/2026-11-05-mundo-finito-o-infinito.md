# Mundo infinito, planeta y escalas (tangente de NoiseLab v4)

Fecha: 2026-11-04/05. Origen: pregunta de Claudio al ver que el viento de NoiseLab cambia al
arrastrar (candidato 5 de `planning/archive/NoiseLab_v4_Startup.md`). Investigación, no
autoridad: lo gobernado sigue en `PCG_Roadmap.md` (Phase K) y `Phase_W_Design.md`.

## El problema
En NoiseLab, el viento entra saturado por el borde de la vista que da al viento
(`WindField`) y la distancia a la costa no ve el agua fuera de la vista. Todo lo que depende de
lo que hay «más allá» cambia al mover la vista.

## Mundo infinito (cada punto = f(coordenadas, seed))
Tres recursos para lo que se extiende más allá de la vista:
1. **Volverlo ruido.** Continentes como ruido de baja frecuencia (la «continentalness» de
   Minecraft; aquí, los splines de la v3).
2. **Alcance acotado + margen.** Si un proceso olvida con el tiempo (el aire del viento pierde
   humedad de forma exponencial), lo que está a más de unas 3/pérdida celdas no cuenta: basta
   muestrear ese margen hacia el viento para que el resultado no dependa de la vista.
3. **Estructuras por celdas.** Placas como celdas de Voronoi de una rejilla infinita (con
   jitter): tipo y movimiento por hash de la celda; el tipo de borde (convergente, divergente,
   transformante) se decide mirando las 3×3 vecinas. Funciona sin límite.

No se puede así lo que tiene alcance ilimitado: cuencas de ríos de un continente entero,
océano frente a lago (conectividad). Eso exige un dominio finito.

## Planeta / mundo finito (Dwarf Fortress, RimWorld)
Se simula el mundo entero una vez, a baja resolución, y luego se hace zoom. Islands ya va por
aquí: Phase W (rejilla de mundo rectangular; cada casilla parametriza un mapa local,
`MapShapeInput`) y Phase K (placas de Voronoi a escala de mundo, solo exploratoria).

## Cascada de escalas (regla)
Cada proceso global se calcula en la escala más gruesa donde importa y donde el mundo cabe en
memoria; la escala siguiente lo lee como entrada y añade detalle de más frecuencia:

placas → continentes y océanos → cordilleras en bordes convergentes → viento, lluvia y ríos
principales en la rejilla del mundo → casilla del mundo → mapa local (interpola y añade ruido
fino).

## Si el mundo es una esfera
- Ruido 3D muestreado sobre la superficie: sin costuras ni polos deformados. Guardado en un
  cubo-esfera (6 caras de rejilla: respeta grid-first).
- Viento por latitud (células de circulación: alisios, vientos del oeste, del este polares) en
  vez de un ángulo único.
- Ejemplo chileno: Andes = borde convergente (Nazca bajo la Sudamericana); en Patagonia, los
  vientos del oeste mojan el lado chileno y dejan la estepa argentina en sombra de lluvia;
  Atacama = alta subtropical + corriente de Humboldt + sombra andina. (Afirmaciones de
  geografía general; citar fuentes si se usan en la web.)

## Recomendación (por decidir)
Para el videojuego (archipiélago con un «afuera» de donde llegan los afuerinos), mundo
**finito**, coherente con W y K. El arrastre infinito de NoiseLab es de laboratorio. Puente
barato: la rejilla de referencia de «% of the world» (128², 16×16 vistas) ya es un mundo finito
en miniatura donde podrían calcularse viento y costa (candidato 5).
