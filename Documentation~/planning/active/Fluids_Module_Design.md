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

## Fuentes (por comprobar)
- Bridson, Hourihan y Nordenstam, «Curl-noise for procedural fluid flow», SIGGRAPH 2007.
- Stam, «Stable Fluids», SIGGRAPH 1999.
- Zhao, «A fast sweeping method for Eikonal equations», *Math. Comp.* 74 (2005).
- Milne-Thomson, *Theoretical Hydrodynamics* (óvalo de Rankine, calle de Kármán).
- NASA SVS, *Perpetual Ocean* (2012, ECCO2).
