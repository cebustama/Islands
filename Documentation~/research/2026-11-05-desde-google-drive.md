# Desde el Google Drive (2026-11-05)

Del análisis del Google Drive de Claudio (`D:\Projects\google-drive`, informe y lecturas). El código entre paréntesis es la `u` de `catalogo.csv`. Nota de referencia: no cambia el roadmap.

## 1. *Generating cultures* (2021): Islands con cultura encima

Propuesta de investigación de Claudio (d6379, marzo de 2021). Su prototipo, «Island Culture Generator», es **una isla con un asentamiento, familias y lugares de interés**.

Sobre ella se generan mitos que nacen de incidentes deformados al contarse. Los mitos dan símbolos, colores, rituales y virtudes, y las islas se mezclan al entrar en contacto. Propone también **música según los materiales de la isla**.

Es la dirección a largo plazo de Islands como base del mundo del juego: el terreno de Islands, más una capa de historia y cultura. La capa de historia ya estaba en el modelo de clases de Band Manager (`World`, `History`, `WorldEvents`; ver `frisson-ddg`).

## 2. Las técnicas del FONDEF (LBS, 2021–2024)

Level Building Sidekick, del FONDEF ID21I10363 (U. de Talca, ISI Lab; Frisson era empresa asociada y Claudio estaba en el comité directivo, no en el equipo técnico). Fuentes: el paper de software (d9246) y la propuesta V3 (d8388). Es una extensión de Unity de iniciativa mixta con estos módulos:

| Módulo | Técnica | En Islands |
| --- | --- | --- |
| Interiores | grafo de habitaciones → plano por **hill climbing** (de ascenso más empinado; proponen cambiarlo por simulated annealing o evolutivos) | Dungeon Generation Pipeline |
| Exteriores | **Wave Function Collapse** con pesos por tile | catálogo de técnicas de Islands Lab |
| Distribución de elementos | **MAP-Elites** «ligero» sobre un algoritmo genético | sin equivalente |
| Misiones | **gramática libre de contexto** (inspirada en Questgram), solo interfaz | sin equivalente |

**Prueba con usuarios** (Think-Aloud, 9 participantes con Unity avanzado):
- logro medio de 83,3 % en una mazmorra 3D y de 60 % en un exterior;
- todos dicen que ahorra tiempo;
- **9 de 9 no entendieron MAP-Elites**.

Lección para NoiseLab y para los explainers de la web: una técnica de calidad-diversidad necesita enseñarse con su mapa visible, no solo exponerse como un botón.

Hay además unos 110 logs CSV de experimentos de hill climbing (por ejemplo, d8894) por si hace falta comparar parámetros.

## 3. Curso de PCG en el IP Arcos (2020)

80 unidades de material del curso «PCG 2020» (Arcos) y del electivo de PCG de 2021 están en Drive (proyecto `pcg-islands` del catálogo). Es material de clase de Claudio que podría alimentar la serie de Islands Lab.
