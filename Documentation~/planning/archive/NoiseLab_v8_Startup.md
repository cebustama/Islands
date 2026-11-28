# Arranque: NoiseLab v8 (océano físico: la regla de la isla y el relieve del fondo)

**Cerrada el 2026-11-28** (la regla de la isla; el relieve queda en PL-19). Ver «Avance» al final y
`changelog-ssot.md`, «NoiseLab v8». Sigue PL-16, puntos 1 y 4.

Fecha: 2026-11-23. Rama: `main`. Sigue a `NoiseLab_v7_Startup.md` (v7 cerrada el
2026-11-23). Roadmap: PL-19 en `PCG_Roadmap.md`.

## Objetivo
Que el océano del planeta pueda llevar agua neta entre masas de tierra: una **corriente
circumpolar** cuando hay mar alrededor del polo (*Polar sea*), y paso de agua entre continentes. De
forma física y con Burst (pedido de Claudio, 2026-11-23). Después, si cabe, que el relieve del fondo
guíe y frene las corrientes.

## Verificado (con evidencia)
- v7 completa (`changelog-ssot.md`, «NoiseLab v7»; commits `f64eee0`, `99d8859`, `a888ddf`,
  `15847dc`). S22 (2026-11-23 22:40, *Run all tests*): Burst check 0 diferencias en ruido, clima,
  flujo, océano y temperatura del mar; región del planeta 46 ms, residual 0,0001; placas dos veces
  0 de 18.432 celdas distintas. Suite 415/415 por el puente (22:47).
- El límite: con *Polar sea* 1 (código C abajo) hay mar a 61–89°S y el océano converge, pero salen
  giros y no una corriente que dé la vuelta: `ComputeOceanRegion` pone ψ = 0 en toda la tierra y en
  los polos, así que la diferencia de ψ entre dos masas de tierra (el caudal entre ellas) es 0.
- Estructura de hoy (`NoiseLab.cs`): `ComputeOceanRegion` (máscara de mar con 2×2 muestras de
  altura por celda, viento zonal `OceanWindX`, β), `OceanSorJob` (rojo-negro por filas, `wrap` en el
  planeta), `OceanResidual`, `SeaHeat`/`SeaHeatJob`; región del planeta 256×128 (`PlanetOceanW`,
  `OceanN`); rozamiento por *West current* (`oceanWidth`).

## Sin verificar (hipótesis, comprobar al arrancar)
- La condición exacta de la regla de la isla para el modelo de Stommel con rozamiento lineal: la
  circulación del viento y la del rozamiento a lo largo de un contorno que rodea la isla se anulan
  (Godfrey 1989, *Geophys. Astrophys. Fluid Dyn.* 45; Pedlosky et al. 1997, «Circulation around
  islands and ridges», *J. Mar. Res.*). Leer al menos una fuente y escribir la versión discreta antes
  del código.
- Que la superposición (un cálculo con toda la tierra a 0 más uno por masa de tierra a 1 sin viento,
  y un sistema K×K para los pesos) cueste ~50–80 ms por masa con Burst en el planeta; medir en el S22.
- Que, sin relieve, la corriente circumpolar salga demasiado fuerte (el rozamiento lineal es el único
  freno); puede pedir una perilla de freno del canal hasta que entre el relieve.
- Que el relieve (f/H) quepa en el mismo SOR (coeficientes variables y un término más) sin perder la
  convergencia; los modelos de una capa exageran el efecto del fondo (suavizarlo).

## Alcance propuesto (a confirmar con Claudio)
1. **Masas de tierra:** componentes conexas de tierra en la rejilla del océano del planeta (con el
   este unido al oeste), más la pared de cada polo cuando hay un anillo de mar. Lista con tamaños en
   el resumen y un color *Landmasses* en ×32 para verlas.
2. **La regla de la isla:** un ψ propio para las masas grandes (umbral de tamaño), por superposición
   (K + 1 cálculos con `OceanSorJob`) y un sistema K×K pequeño (en double, fuera de Burst). Las
   islas pequeñas siguen a 0 o al valor del agua que las rodea (decidir).
3. **Medidas:** caudal circumpolar y entre continentes en el resumen; *Gyres* y *Sea temp* en ×32
   con y sin la regla (mismo código, un interruptor). Burst dos veces 0; *Run all tests* en el S22.
4. **(Opcional, si cabe) relieve del fondo (f/H)**, con el fondo de hoy (el ruido bajo el nivel del
   mar, suavizado). Con la fase K vendría de las placas (dorsales, fosas).

## Decisiones abiertas (antes del código)
- **D-V8-1, orden:** el 2026-11-22 Claudio puso PL-16 (puntos 1 y 4: lluvia por latitud y aridez)
  justo después de la v7. Recomendación de cierre de la v7: el océano físico primero (lo pidió el
  2026-11-23, es independiente y arregla un límite visible), luego PL-16, luego la fase K. Confirmar.
- **D-V8-2:** qué masas tienen ψ propio (umbral de tamaño; los polos) y qué se hace con las islas
  pequeñas.
- **D-V8-3:** ¿relieve (f/H) en la v8 o después de la fase K, con dorsales de verdad?
- **D-V8-4:** ¿solo con *Planet*? Recomendación: sí; con *Planet* apagado, los mismos bits que la v7.

## Decisiones tomadas (Claudio, 2026-11-25: «ok» a las cinco recomendaciones)
- **D-V8-1:** el océano físico ahora; PL-16 después.
- **D-V8-2:** ψ propio para las masas grandes (unas 8, más los muros de los polos; el umbral se fija
  con el conteo real). Las islas pequeñas cuentan como **mar** en el cálculo del océano (la vista las
  sigue mostrando como tierra). Dejarlas a 0 las ataría al muro norte y rompería la prueba exacta.
- **D-V8-3:** el relieve (f/H) después, cuando la regla esté medida.
- **D-V8-4:** solo con *Planet*; con *Planet* apagado, los mismos bits.
- **D-V8-5:** un sitio de prueba D con un paso de mar hacia 55°S (corriente hacia el este), al final.

## La regla de la isla (fuentes leídas el 2026-11-25)
- Pedlosky, Pratt, Spall y Helfrich 1997 (*J. Mar. Res.* 55, §2a, ec. 2.2): la componente
  tangencial del momento, integrada sobre la **costa** de la isla (velocidad normal 0: Coriolis se
  anula; la presión tiene un solo valor), da ∂ₜ∮u·t ds = ∮τ·t ds + ∮Diss(u)·t ds. Pratt y Pedlosky
  1998 (*JPO* 28, ec. 1.1–1.2, §6): la fórmula de Godfrey (1.2) cambia el camino para no calcular el
  rozamiento y sobrestima el caudal hasta un 25 %. Godfrey 1989 no se abrió en el original.
- Con Stommel (Diss = −r·u), estacionario: **r ∮u·t ds = ∮τ·t ds**, exacta. Nuestra ecuación
  (`eps∇²ψ + β∂ₓψ = rot τ`) sale de ese momento con r = `eps`.
- **Discreta:** cada masa S es un nodo con un valor ψ_S, y su ecuación es la suma de la de
  `OceanSorJob` sobre sus celdas (Stokes):
  `Σ_caras costa k(ψ_mar − ψ_S) + Σ_extremos este de fila (β_y/h)(ψ_este − ψ_S) − Σ_{c∈S} force = 0`,
  con k = eps/h². El término β es el resto O(h) del upwind (0 en el continuo). Muro de cada polo: un
  nodo con fila fantasma; el norte fijo a 0 (referencia); el viento en el polo es sin(3π) = 0.
  Solución: superposición (K+1 resoluciones de `OceanSorJob`, sin cambiarlo) y un sistema K×K en
  double.
- **Prueba exacta (deducida, no leída):** si toda la tierra al sur de una latitud de puro mar tiene
  nodo, sumar las ecuaciones da **ū(y) = τₓ(y)/eps** (Hidaka / Munk y Palmén, sin verificar en
  fuente). Con West current 0,25: |ū| ≈ 3,4 a 61–89°S.
- **Hallazgo:** *Polar sea* abre mar en los vientos del este polares (60–90°), así que allí la
  corriente irá hacia el **oeste** (análogo: la corriente costera antártica). La corriente hacia el
  este necesita un paso en los vientos del oeste (30–60°): D-V8-5.

## Fuera de alcance
Corrientes con paso de tiempo, varias capas o remolinos; mezcla entre giros; la fase K (placas que
dan forma al terreno); PL-16; PL-14; cambios en `Runtime/`.

## No rehacer
La v6 y la v7 (planeta, océano periódico, ×32, avisos, *Polar sea*, placas, *Run all tests*,
*Snapshot*, ir al punto tocado), la receta oficial.

## Ritual de arranque
1. `git log --oneline -6` en `D:\Projects\Islands`: el último commit de código es `15847dc`.
2. Leer este archivo, PL-19 en `PCG_Roadmap.md` y, en `NoiseLab.cs`, `ComputeOceanRegion`,
   `OceanSorJob`, `OceanResidual` y `MakeBigPicture`.
3. Suite por el puente (`ClaudeTests/run.trigger` en el anfitrión): 415/415.
4. Buscar y leer la regla de la isla (Godfrey 1989 o Pedlosky et al. 1997, o un apunte de curso que
   la derive para Stommel); decir qué se pudo abrir. Escribir la condición discreta.
5. Confirmar con Claudio D-V8-1…4 antes de escribir código. En cada prueba, el NL1 completo.

## Códigos de prueba (sitios fijos; *Run all tests* en *Perf*)
- **A, Planet apagado (receta v6):**
  `NL1;t=simplex/simplex;s=88775;f=8;o=1;l=2;g=0.5;w=%71;x=0.343;y=2.185;pal=mapa;b=50,30,20/4,30,30,16,12,8;q=w;h=s;n1=simplex/perlin,0.15,4,2,0.5;n2=simplex/perlin,3,3,2,0.5;n3=simplex/perlin,8,4,2,0.5;c=-1:-1,0:-1,0.1:-0.3,0.2:0.25,0.5:0.4,1:0.7/-1:1,-0.2:0.6,0.3:0.15,1:0.05/-1:-0.3,-0.3:-0.05,0.2:0.1,0.6:0.45,1:0.6;k=z;n4=simplex/perlin,2,4,2,0.5;n5=simplex/perlin,3,4,2,0.5;cl=0.8,0.5,0.5,0.1,0.05,0.7,0.17,0.3,0.5,10,3;wd=300,0.6,0.6,0.01,0.05;wf=0.5,200;oc=0.25,1.56,0.5;sh=0.8;ct=0.05;ov=contours:0.45;v=2:map,moisture`
- **B, Planet:**
  `NL1;t=simplex/simplex;s=88775;f=8;o=1;l=2;g=0.5;w=%71;x=-0.515;y=0.593;pal=mapa;b=50,30,20/4,30,30,16,12,8;q=w;h=s;n1=simplex/perlin,0.15,4,2,0.5;n2=simplex/perlin,3,3,2,0.5;n3=simplex/perlin,8,4,2,0.5;c=-1:-1,0:-1,0.1:-0.3,0.2:0.25,0.5:0.4,1:0.7/-1:1,-0.2:0.6,0.3:0.15,1:0.05/-1:-0.3,-0.3:-0.05,0.2:0.1,0.6:0.45,1:0.6;k=z;n4=simplex/perlin,2,4,2,0.5;n5=simplex/perlin,3,4,2,0.5;cl=0.8,0.5,0.5,0.1,0.05,0.7,0.17,0.3,0.5,10,3;wd=300,0.6,0.6,0.01,0.05;wf=0.5,200;pl=1;oc=0.25,1.56,0.5;sh=0.8;ct=0.05;ov=contours:0.45;v=2:map,moisture`
- **C, Planet con *Polar sea* 1 (el caso circumpolar):** B con `pl=1,1` en lugar de `pl=1`.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (código; autoridad).
- `Documentation~/planning/active/PCG_Roadmap.md` (PL-19, PL-17, PL-18, PL-16, Phase K).
- `Documentation~/changelog-ssot.md` («NoiseLab v7», «NoiseLab v6»).
- `Documentation~/planning/active/NoiseLab_Migration_Matrix.md` (O1, O5; fila nueva al cerrar).
- `Documentation~/planning/archive/NoiseLab_v7_Startup.md` (avance de la v7).

## Avance (2026-11-25/28)
- **Ritual** (2026-11-24): suite 415/415; fuentes leídas (Pedlosky et al. 1997 y Pratt y Pedlosky
  1998, con `pdftotext`; Godfrey 1989 no). Decisiones D-V8-1…5: «ok» de Claudio a las cinco.
- **Paso 1** (`10d4b6c`): masas de tierra y color *Landmasses*. Código C: 11 masas (6291, 746, 492,
  luego ≤ 8). Umbral por tamaño (≥ 64 celdas, una vista de tierra) en vez de «las 8 mayores».
- **Paso 2** (`10d4b6c`): la regla de la isla. Código C, S22: comprobación a 75°S −3,390 frente a
  −3,391; vuelta al planeta −7,26 (al oeste); 5 cálculos, 133 ms; Burst check 0. Planet apagado: los
  mismos resultados (editor y S22).
- **Sitio D** (cierre): *Polar sea from*; `pl=1,1,1,40` (editor): a 45°S +3,399 frente a +3,395
  (al este); neto −4,54. No corrido en el S22.
- *Gyres*: opción a (el color es el nivel de ψ; ayuda cambiada).
- **Tangentes guardadas:** color *Spin* (la vorticidad ζ = ∇²ψ: dónde gira el agua sobre sí misma;
  ~30 líneas); una «Antártida» que tape los vientos polares del este para que el neto vaya al este
  (mejor con PL-20, la Tierra como entrada); el relieve (PL-19, punto 2).
