# Diseño: módulo de fluidos (flujo, viento, corrientes)

Fecha: 2026-11-04. Estado: **propuesta de diseño, sin código**. Es planificación, no autoridad:
manda lo que digan `SSoT_INDEX.md` y `CURRENT_STATE.md`. Va después de NoiseLab v4, que deja
los fluidos fuera de su alcance.

Origen: capturas del 2026-11-03 y 2026-11-04 en `research/2026-11-capturas.md` (flujo potencial
y calle de Kármán; Pirámides de Sal y erosión del viento; mapa de corrientes de la NASA).

## Objetivo (Claudio, 2026-11-04)
Una sección o módulo de la librería para **algoritmos y simulaciones de fluidos**, con cuatro
usos:

1. **Viento para la humedad y la sombra de lluvia.** Hoy es el barrido de una sola dirección de
   NoiseLab v3 (`WindField`).
2. **Corrientes marinas.** Una corriente fría a lo largo de una costa produce desierto costero y
   niebla, y así Atacama sería una regla y no un truco.
3. **Nubes animadas** (NoiseLab, y luego adaptadores).
4. **Deriva** de canoas, naufragios o semillas para el juego.

## Decisión tomada
- **Relieve (Claudio, 2026-11-04, opción b):** el aire **rodea** lo que pasa de un umbral de
  altura y **sube** lo que queda por debajo, que es lo que produce la lluvia orográfica de la v3.
  El umbral es un parámetro con deslizador en el prototipo. En el mar, el obstáculo es toda la
  tierra.

## Lo que hay hoy (comprobado el 2026-11-03)
- `MapFieldId` solo tiene campos escalares (`ScalarField2D`); no hay campos de vectores. El enum
  crece añadiendo al final (`COUNT = 7`).
- `CoastDist` (Phase G) da la distancia a la costa.
- `Runtime/Surfaces/FlowJob.cs` (Catlike) tiene curl de ruido, pero mueve partículas para
  dibujar: no forma parte del pipeline.
- El viento de NoiseLab v3 va línea a línea en una sola dirección, en O(n²).

## Propuesta de representación
**Guardar la función de corriente ψ como campo escalar** (un `MapFieldId` nuevo al final del
enum). La velocidad se obtiene con diferencias finitas: u = ∂ψ/∂y, v = −∂ψ/∂x.
- *Por qué así:* con ψ no hay divergencia por construcción (el aire y el agua no se amontonan ni
  desaparecen), y cabe en la arquitectura actual de campos escalares.
- *Alternativa descartada por ahora:* dos campos U y V. Son dos campos en vez de uno, y el error
  numérico puede crear o perder masa.
- *Qué se rompe si se hace mal:* si la humedad se transporta con una velocidad que tiene
  divergencia, aparecen zonas que se mojan o se secan sin causa física.

## Candidatos para el campo
| | Técnica | Rodea | Coste | Para qué uso |
|---|---|---|---|---|
| A | Una dirección (lo de hoy) | No | O(n²) | Referencia para comparar |
| B | Bandas por latitud (alisios y oestes) | No | O(n²) | Corriente de fondo de C o D |
| C | ψ de fondo × rampa(distancia al obstáculo), más curl noise (Bridson et al. 2007) | Sí | O(n²), sin iterar | Viento y corrientes: el primero que hay que probar |
| D | Resolver Laplace ∇²ψ = 0 con el obstáculo como borde (Jacobi o multigrid, con número fijo de iteraciones) | Sí, y más fiel | Iterativo | Si C no da estelas ni calmas creíbles |
| E | Fluidos estables (Stam 1999), con paso de tiempo | Sí, con vórtices | Caro y depende del tiempo | Solo nubes animadas y estelas de Kármán, en el adaptador o en NoiseLab, nunca en el pipeline |

Las estelas de Kármán detrás de islas altas pueden imitarse con vórtices puestos a mano al ritmo
de Strouhal (f ≈ 0,2·U/D) sobre C, como en la animación de matheorem.s. Es un truco visual.

## Transporte (lo que cambia en la humedad)
Con un viento que cambia de celda a celda, el barrido de una dirección deja de valer. Hay dos
opciones: barrido rápido (*fast sweeping*, Zhao 2005; ver la entrada de Ultra Water) o
advección semi-lagrangiana con un número fijo de pasos. Las dos son deterministas.

## Invariantes que se mantienen
Determinista (sin `UnityEngine.Random`, iteraciones fijas, orden de fila estable), sobre la
rejilla y sin motor gráfico; los adaptadores al final. E queda fuera del núcleo.

## Decisiones abiertas (antes de escribir código)
1. **Dónde vive el módulo:** `Runtime/Fluids/` con su asmdef, o operadores dentro de `Runtime/PCG`
   (como `HeightFieldHydrologyOps2D`). Depende de si E (con tiempo) entra en la librería.
2. **Si ψ se gobierna como `MapFieldId`** o queda primero solo en NoiseLab.
3. **Orden de los usos:** propuesta, primero viento (1), luego corrientes (2), nubes (3) y deriva
   (4).
4. **Qué pasa con los goldens:** llevar el viento a `Stage_Biome2D` los mueve. Es un cambio
   gobernado.

## Protocolo de medición (prototipo en NoiseLab, después de la v4)
- A, C y D lado a lado con la vista 2×2 de la v4.
- Divergencia máxima y media del campo de velocidad (tiene que ser ~0 en C y D).
- Milisegundos en el S22 a 64², 128² y 256².
- Diferencia de humedad frente a A: dónde cambia la sombra de lluvia.
- Una costa con corriente fría: ¿sale desierto costero?
- Revisión visual de Claudio en el editor y en el S22.

## Añadido el 2026-11-05 (desde los videos de Lague)
Variante **«proyección única»** para probar junto a C y D: viento de fondo (+ curl) sobre la
máscara de sólidos y un solve de presión con rojo-negro + SOR (ω ≈ 1,7, iteraciones fijas), sin
pasos de tiempo. Ventaja frente a ψ con varias islas: no hace falta la constante de ψ en cada
una. Detalle y código: `research/2026-11-05-lague-fluidos.md` y `research/fluidos-lague/`.

## Añadido el 2026-11-06 (cierre de NoiseLab v5)
NoiseLab ya muestrea y calcula el clima con jobs de Burst (`GridNoiseJob<N>`, `WindJob`…,
`FloatMode.Strict`) y el clima ve más allá de la vista (margen hacia el viento). El prototipo
de fluidos de la v6 parte de ahí: rejilla ampliada, jobs en paralelo y Burst check para
comparar. Claudio pide incluir las **corrientes marinas** (uso 2) en la v6.

## Añadido el 2026-11-09 (paso 1a de NoiseLab v6)
- **Medido:** con rejilla grande, SOR con ω 1,7 y 60 pasadas no converge (la divergencia subió
  0,044 → 0,053). Con ω óptimo, 2/(1+sin(π/N)), hacen falta cientos de pasadas y el coste
  crece con el cubo del lado (mitad de la rejilla del clima, 400 pasadas: ~170 ms en el editor).
  Arreglo de la v6: rejilla a 1/4 del clima, 200 pasadas (31,5 ms; divergencia 0,107 → 0,017),
  un cuarto de las pasadas al arrastrar y bloques alineados al mundo.
- **Decisión de Claudio: multirejilla en la versión siguiente (v7).** Mismo resultado mejor
  convergido, coste proporcional a las celdas; permitiría volver a mitad o resolución completa.
  Lo difícil: qué es pared en los niveles gruesos (una prueba rápida, ingenua, convergió mal).
  No arregla el ~4 % que cambia el viento al arrastrar (la presión ve todo el recuadro).
- **Candidato para un mundo con continentes (razonamiento, sin fuentes revisadas):** viento de
  fondo por latitud (alisios, del oeste, polares; Coriolis; estaciones) en vez de un ángulo
  único; el rodeo solo para las grandes cordilleras; los continentes como paredes de las
  corrientes; cálculo en la generación del mundo, no por fotograma.

## Fuentes (por comprobar)
- Bridson y Müller-Fischer, *Fluid Simulation*, notas del curso SIGGRAPH 2007.
- Müller, Ten Minute Physics #17, «How to write an Eulerian fluid simulator».
- Bridson, Hourihan y Nordenstam, «Curl-noise for procedural fluid flow», SIGGRAPH 2007.
- Stam, «Stable Fluids», SIGGRAPH 1999.
- Zhao, «A fast sweeping method for Eikonal equations», *Math. Comp.* 74 (2005).
- Milne-Thomson, *Theoretical Hydrodynamics* (óvalo de Rankine, calle de Kármán).
- NASA SVS, *Perpetual Ocean* (2012, ECCO2).

## Añadido el 2026-11-10 (paso 2 de NoiseLab v6: corrientes marinas)

Decisión de Claudio: **modelo B (Stommel 1948) con rejilla gruesa propia**, lo más correcto y
escalable posible. Propuesta, no autoridad; manda el código.

- **Ecuación:** ε∇²ψ + β(y)·∂ψ/∂x = rot τ(y); u = −∂ψ/∂y, v = ∂ψ/∂x; ψ = 0 en tierra.
  Viento zonal τx = −τ0·sin(6·lat): alisios (0–30°, calma en el ecuador), oestes (30–60°),
  polares (60–90°). β = cos(lat), por el giro del planeta. ε fija el ancho de la corriente
  del oeste (deslizador, en anchos de vista).
- **Escala (cascada de escalas, `research/2026-11-05-mundo-finito-o-infinito.md`):** regiones
  fijas al mundo de 16 × 16 anchos de vista (de polo a polo en y), 8 celdas por ancho de vista
  (128²), una cada 8 vistas en x, solapadas a la mitad y mezcladas con pesos en triángulo
  (sus paredes caen donde pesan 0). Nada depende de la vista; caché de 4 regiones.
- **Solucionador:** Gauss–Seidel por líneas, dos colores de filas en paralelo, cada fila de
  este a oeste (la dirección en que β lleva la información), término β a contracorriente
  (estable con cualquier ancho). Prueba offline (`stommel.py`, cuenca rectangular 128²):
  ω 1,85 llega a la solución exacta en 100 pasadas; giro subtropical horario en el norte,
  antihorario en el sur, corriente del oeste ~7× el interior, corriente lenta hacia el
  ecuador en la costa este.
- **Límite conocido:** ψ = 0 en toda tierra: entre dos masas de tierra no pasa agua neta.
  La regla de la isla (Godfrey 1989) lo resolvería; medir antes de arreglar.
- **Subpasos:** 2a corrientes y capa *Currents*; 2b temperatura del mar (latitud + transporte
  por la corriente con el esquema de la 1b.2 + afloramiento costero por el viento hacia el
  ecuador de los alisios); 2c el clima lee la temperatura del mar (costa fría: más fresca y
  seca; evaporación según la temperatura).
- **Web:** candidato a página de Islands Lab (ψ, giros, Atacama y Humboldt).

## Añadido el 2026-11-17 (módulo de hidrología)

- La lluvia y la temperatura que deja este módulo (2c) alimentan la hidrología: H1 pondera el caudal con `lluvia − evaporación(temperatura)`. Por eso la hidrología va después de la v6. Diseño en [`Hydrology_Module_Design.md`](Hydrology_Module_Design.md) (PL-14).

## Añadido el 2026-11-17 (2b hecha: temperatura del mar)

Manda el código (`SeaBaseTemp`, `SeaHeat`, `SeaHeatJob` en `NoiseLab.cs`); resumen en
`../archive/NoiseLab_v6_Startup.md` («2b cerrada»).

- **Cambio frente al diseño:** el afloramiento no lee el viento a lo largo de la costa. El
  viento del modelo B es solo zonal (τx), así que en una costa norte–sur nunca va paralelo a
  ella. La regla lee su huella: tierra al este y corriente hacia el ecuador → tira hacia el
  agua profunda (10 °C). Si algún día el viento tiene componente meridiana (alisios que se
  curvan hacia el ecuador al este de cada océano), la regla de Ekman podría sustituirla.
- **El transporte no es el de la 1b.2 tal cual:** pierde por tiempo, no por distancia (relajación
  `h / memoria`), y los flujos de cara salen de ψ en las esquinas, sin divergencia.
- **Para la 2c:** la temperatura vive en la rejilla de las regiones (156 km por celda); la
  costa que aflora mide una celda. El clima la muestreará con `SampleRegionField`, igual que
  la capa *Sea temp*.
