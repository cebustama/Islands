# La Alpujarra: nubes en capas y relieve (captura)

Fecha: 2026-11-09. Estado: **investigación, no autoridad** (manda `SSoT_INDEX.md`). Tangente
abierta al arrancar NoiseLab v6; apuntada para después.

- **Origen:** seis fotos de Claudio desde Soportújar y Lanjarón (Alpujarra granadina), fin de
  semana del 7–8 de noviembre de 2026 (feria del embrujo de Soportújar). Sierras en capas cada
  vez más pálidas, bruma en los valles, un velo alto de nubes con el sol difuminado, cúmulos
  pequeños sobre las cumbres, bancales de olivos, cárcavas, el valle del Guadalfeo con un pueblo
  en el fondo (probablemente Órgiva) e invernaderos blancos a lo lejos.
- **Lo que dijo Claudio:** las nubes tienen «niveles»: unas bajas que chocan con las cumbres y
  otra capa mucho más arriba. ¿Cómo funciona? ¿Podemos generar una geografía así con Islands?

## Nubes en capas (explicación dada; fuentes por citar)
- El aire se enfría al subir (~6,5 °C/km); la base de la nube es donde se condensa.
- Bajas (< 2 km): aire húmedo del mar forzado a subir por las laderas (orográficas). Altas
  (6–12 km, cirros y cirroestratos de hielo): sistemas grandes que no notan el relieve y suelen
  ir en otra dirección (cizalladura).
- Inversión térmica: aire más caliente encima hace de tapa; la bruma queda abajo, y la
  perspectiva aérea pinta cada sierra más pálida.
- ⚠ Por comprobar: en la Alpujarra se llamaría «taró» a la niebla que sube del Mediterráneo.

## 💡 Para NoiseLab (después del paso 1 de la v6; decisión de Claudio: «para después»)
**Dos capas de nubes:** una baja que sigue el viento que rodea el relieve, nace en las laderas
de barlovento y se frena en las cumbres, y una alta que ignora el relieve y va en otra
dirección. La baja necesita el viento del paso 1.

## La geografía y lo que Islands tiene (comprobado en `CURRENT_STATE.md` y `Runtime/`)
Rasgos (de memoria, por verificar con fuentes): Sierra Nevada al norte (Mulhacén 3.479 m),
Lújar y Contraviesa al sur junto al mar, el valle del Guadalfeo entre medias (este–oeste),
barrancos paralelos norte–sur (Poqueira), cárcavas, bancales, acequias andalusíes.

| Rasgo | Estado en Islands |
|---|---|
| Cordilleras (ruido *ridged*) | Implementado (runtime de ruido) |
| Forma grande: cordillera, valle, cordillera | Parcial: máscara de forma (W.c) y curvas de NoiseLab dan islas, no esa forma |
| Ríos y cuencas | Implementado: `Stage_Hydrology2D` |
| Barrancos tallados por el agua | No: erosión simulada en NONE |
| Humedad del mar y sombra de lluvia | NoiseLab v3–v5; mejor con el viento de la v6 |
| Bruma en capas, perspectiva aérea | Render (vista en capas del palmar de la web) |
| Bancales | No; detalle visual posible |

**Prueba propuesta:** comparar con el modelo digital de elevación del IGN (MDT, gratuito) de la
zona: relieve real frente a generado.

## Conecta con
- `cuaderno-civilizaciones/notas/2026-11-06-brujas-lanjaron-alpujarra.md` (la feria del embrujo).
- `planning/active/Fluids_Module_Design.md` (uso 3, nubes).
- Web: palmar, vista A (panorama en capas).

**Estado:** semilla.
