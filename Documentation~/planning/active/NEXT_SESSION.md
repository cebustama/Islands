# Próxima sesión (arranque)

Este archivo guarda siempre el arranque de la **próxima** sesión de Islands. Cuando Claudio dice
«continuemos», el agente lo lee y sigue su ritual. Al cerrar cada sesión se **sustituye** por el
arranque nuevo (no se acumula: el historial está en `changelog-ssot.md` y en `planning/archive/`).

Escrito: 2026-11-28, al cerrar NoiseLab v8.

---

Proyecto Islands.PCG — PL-16 puntos 1 y 4 (lluvia por latitud y aridez), tras NoiseLab v8.

**Material (leer primero):**
- `Documentation~/planning/active/PCG_Roadmap.md`: PL-16 (y PL-14, PL-19, PL-20).
- `Documentation~/changelog-ssot.md`: «NoiseLab v8» y «NoiseLab v7».
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (autoridad de código): el clima (`WindField` /
  `WindJob`, `ViewClimateJob`, `BigBiomes`) y *Sea → coast moisture*.

**Verificado (con evidencia):**
- v8 cerrada y subida (`10d4b6c`, `add5c3d`). S22, código C: Burst check 0 diferencias, regla de la
  isla 133 ms, comprobación −3,390 frente a −3,391. Planet apagado = los mismos resultados (costa
  1609, −7.55 °C, borde 0.09 %). Suite 415/415 por el puente (2026-11-28).

**Sin verificar:**
- El código D (`pl=1,1,1,40`) en el S22 (solo en el editor).
- En vistas casi todo mar, el viento de la prueba de borde cambia más con margen que sin él.

**Objetivo y DoD:**
- Punto 1: cinturones de lluvia por latitud (Hadley / Ferrel / polar: ecuador húmedo, seco a ~30°,
  húmedo a 50–60°, polos secos). Punto 4: aridez (lluvia − evaporación según la temperatura) en la
  lectura de biomas. Al entrar el punto 1, bajar el valor por defecto de *Sea → coast moisture* (hoy 3).
- DoD: Planet apagado = los mismos resultados con los términos nuevos a 0; Burst dos veces 0;
  *Run all tests* en el editor y el S22; el NL1 completo en cada prueba.

**Decisiones abiertas (confirmar con Claudio antes del código):**
- La forma del término de latitud (curva fija, perillas, o leída del viento por bandas).
- Cómo entra la aridez en la tabla de biomas (umbral de humedad que depende de la temperatura, o un
  eje nuevo).
- Si el valor por defecto de los términos nuevos es 0 (los mismos bits) o encendido.

**Fuera de alcance:** PL-19 punto 2 (relieve del fondo), PL-20 (la Tierra como entrada), el color
*Spin* (vorticidad), PL-14 (ríos), cambios en `Runtime/`.

**No rehacer:** v6, v7, v8 (regla de la isla, *Landmasses*, *Polar sea from*), la receta oficial.

**Códigos de prueba:** A (Planet apagado), B (Planet), C (`pl=1,1`) y D (`pl=1,1,1,40`) en
`planning/archive/NoiseLab_v8_Startup.md`, «Códigos de prueba» (D = C con `pl=1,1,1,40`).

**Ritual de arranque:**
1. `git log --oneline -4` en `D:\Projects\Islands` (último commit: `add5c3d` o posterior).
2. Leer PL-16 y el código del clima en `NoiseLab.cs`.
3. Suite por el puente (`ClaudeTests/run.trigger` en el anfitrión): 415/415. Si no la recoge,
   pedirle a Claudio que dé el foco a Unity.
4. Proponer el diseño en lenguaje sencillo y confirmar las decisiones con Claudio antes del código.
