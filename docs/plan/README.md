# Copia de las paginas publicadas

Las dos son el cuerpo de la pagina, sin la envoltura `<!doctype html><head>` que agrega el
publicador: se abren en un navegador, pero la fuente de verdad es el documento del
repositorio que cada una resume.

| Copia | Artefacto | Fuente de verdad |
|---|---|---|
| [modscore-plan.html](modscore-plan.html) | Version 19, del 2026-09-18, con el dashboard ejecutivo (D1 y D2): <https://claude.ai/artifact/5cFSpe2puMikBkB3u5HRKd> | [PLAN.md](../../PLAN.md) |
| [scoring-decisions.html](scoring-decisions.html) | Version 1, del 2026-09-16, las once reglas que el negocio tiene que confirmar: <https://claude.ai/artifact/Gp7c9gHv5kTSdGETPfeCi5> | [docs/scoring-decisions.md](../scoring-decisions.md) |

Para actualizar un artefacto desde otra sesion: leerlo por su URL, editar la version que
devuelve y publicar con la misma URL. Despues, reemplazar esta copia. El archivo que
devuelve la lectura usa CRLF: un parche escrito con LF no encuentra sus anclas de varias
lineas.
