# Arranque: NoiseLab en el móvil (modo clase)

Fecha: 2026-10-31. Rama: `main`.

## Verificado (con evidencia)
- La escena didáctica está hecha: `Samples/0.1.0-preview/NoiseLab/` (`NoiseLab.cs`,
  `NoiseLab.unity`), commits locales `9afd4a1` y `1cafc65` (sin push al cerrar la sesión
  del 2026-10-31). Detalle en `changelog-ssot.md` («NoiseLab»).
- Claudio la probó en el editor en vertical (IG Reel 1080×1920) y en horizontal (Full HD):
  todos los controles responden y nada se corta.
- Suite EditMode 415/415 tras el cambio (puente `ClaudeTests/`).
- El editor 6000.3.10f1 tiene instalados los módulos `AndroidPlayer` y `WebGLSupport`
  (`D:\Unity\Editor\6000.3.10f1\Editor\Data\PlaybackEngines`).
- La lista de escenas de build del anfitrión
  (`ProjectSettings/EditorBuildSettings.asset`) solo tiene `SnakeCave.unity`.

## Sin verificar
- La plataforma activa del anfitrión parece «Web» (barra de título en una captura de
  Claudio), pero no se ha comprobado en Build Profiles.
- Que una escena dentro de `Packages/` (paquete por `file:`) entre en la build sin
  copiarla a `Assets/`.
- Rendimiento en el móvil: el muestreo es C# en el hilo principal, sin Burst
  (65 536 muestras × octavas en cada cambio). En WebGL puede notarse.
- Si IMGUI responde bien al tacto en el dispositivo real.

## Decisiones abiertas (las toma Claudio, al principio)
1. **Android (APK por USB) o WebGL (en el navegador del móvil).** Android: más rápido y
   nativo, hace falta el modo desarrollador y un cable. WebGL: la plataforma ya parece
   activa y se abre con un enlace, pero hay que servirla (en local o en la web) y es más
   lenta.
2. **Si es WebGL, dónde se sirve:** solo en local para la prueba, o en `/privado/` de la
   web. Esto último abre un frente en otro repo: solo con el OK de Claudio.

## Tarea
Hacer una build de NoiseLab y probarla en el móvil de Claudio, en modo clase. Si el
rendimiento no da, la primera medida es bajar la resolución por defecto en el móvil, antes
de pasar el muestreo a un job con Burst.

DoD: la build abre en el móvil; se toca y responde; el histograma muestra lo mismo que en el
editor con la misma semilla; Claudio lo confirma. Sin cambios de runtime.

## No rehacer
La escena, su lección, el arreglo del rango del ruido, las mediciones.

## Fuera de alcance
La app de portafolio; UI Toolkit; arreglar `Samples~` (deuda anotada en `CURRENT_STATE.md`,
«Open observations»); documentación de W.c.

## Ritual de arranque
1. `git log --oneline -5` y `git status` en `D:\Projects\Islands`. Comprobar si
   `9afd4a1`/`1cafc65` ya están subidos.
2. Con Unity abierto, `ClaudeTests/run.trigger` vacío en el anfitrión: 415/415.
3. Preguntar a Claudio qué plataforma está activa en Build Profiles y presentar las
   decisiones 1 y 2 con una recomendación. No tocar ajustes de build antes de su respuesta.

## Archivos
- `Samples/0.1.0-preview/NoiseLab/NoiseLab.cs` (la escena).
- `ProjectSettings/EditorBuildSettings.asset` del anfitrión (sin git: anotar a mano lo que
  cambie).
- `Documentation~/CURRENT_STATE.md`, `changelog-ssot.md`.
