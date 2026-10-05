# ADR 0029 — El sitio compilado viaja con el código, y el CI exige que sea el build de ese commit

**Estado:** aceptada · **Fecha:** 2026-10-05 · **Relacionado:** [ADR 0001](0001-arquitectura-api-led-en-dos-capas.md)

## Contexto

Para levantar la aplicación sin Vite ni Node (camino C de la guía), el sitio compilado se
descargaba como artefacto `sitio-compilado` del CI. El 2026-10-05, en el equipo de la empresa
donde se usa, no alcanzó ninguna de las vías:

- **`npm ci` no se puede completar.** Primero falló el certificado: la red inspecciona HTTPS y
  firma con una raíz que Node no conoce (se resuelve con `--use-system-ca`). Después, el proxy
  respondió **403** a `brace-expansion@1.1.21`, que el registro público sí entrega: la política
  de la red bloquea versiones recientes. Las únicas versiones sin vulnerabilidades conocidas
  son justo las recientes, 1.1.21 y 5.0.12 (todas las anteriores tienen CVE altas, la última
  publicada el 2026-09-29), así que bajar de versión no es una opción.
- **El artefacto no se pudo descargar** desde ese equipo. GitHub lo entrega desde un dominio
  distinto al del código, y no hay forma de diagnosticarlo sin acceso a esa red.
- **No se pueden pasar archivos** de otro equipo al de la empresa.

Lo único que sí llega es el **ZIP del código** que entrega GitHub (**Code → Download ZIP**).

`brace-expansion` solo entra por `eslint` y `typescript-eslint`, que se usan para revisar el
código (lint), no para construir el sitio. Pero la política del proxy bloquea por antigüedad,
no por paquete: el próximo arreglo de seguridad de cualquier dependencia volvería a quedar
bloqueado.

## Decisión

1. **El sitio compilado se versiona en `sitio-compilado/`**, en la raíz del repositorio, para
   que lo traiga el ZIP. Se genera desde `src/web` con `npm run build:sitio-compilado` (el mismo
   `tsc -b && vite build`, con otra carpeta de salida). El camino C usa
   `--webroot C:\modscore1\sitio-compilado`; no hay nada que descargar aparte.
2. **El CI exige que sea el build de ese commit.** El job SPA compila `src/web` desde el lock
   y compara byte a byte con `git diff --no-index --exit-code dist ../../sitio-compilado`. Si
   difiere (desfasado o tocado a mano), el job falla, también en los PR. Se verificó antes de
   decidir que el build es reproducible: Windows con Node 24.14 y Linux (Alpine) con Node 24.21
   dan los mismos bytes.
3. **Se versiona tal cual:** `.gitattributes` marca la carpeta `-text` (sin conversión de
   finales de línea, para que la comparación sea exacta) y `linguist-generated` (plegada en los
   diffs de GitHub).
4. **Lo demás no cambia de criterio:** las imágenes siguen compilando la SPA desde el código
   (`.dockerignore` excluye la carpeta), CodeQL analiza el código fuente y no el compilado
   (`paths-ignore`), y gitleaks sí recorre la carpeta (sin hallazgos el 2026-10-05).
5. **Se retira el artefacto `sitio-compilado` del CI:** con la carpeta en el repositorio sobra.

## Alternativas consideradas

- **Cambiar las dependencias de lint** para no depender de `brace-expansion`: la política del
  proxy bloquea por antigüedad, no por paquete; el próximo arreglo de seguridad de cualquier
  dependencia volvería a dejar el equipo sin poder instalar.
- **Bajar a versiones que el proxy deje pasar:** todas las anteriores tienen vulnerabilidades
  altas. Va contra la regla de no usar librerías vulnerables.
- **Instalar solo lo necesario para compilar** (sin lint): `npm ci` instala el lock entero, y
  separarlo no evita que una versión nueva de Vite o de React quede bloqueada igual.
- **Un release de GitHub con el `.zip` adjunto:** los adjuntos se descargan desde un dominio
  distinto al del ZIP del código, con el mismo riesgo que el artefacto, y habría que
  publicarlos a mano en cada cambio.
- **Pedir a soporte que habilite las versiones:** sigue siendo lo correcto para el camino A, y
  la guía lo indica, pero no depende del proyecto ni tiene plazo.

## Consecuencias

- El ZIP de GitHub basta para usar la aplicación: solo hace falta el .NET SDK 10. Si el proxy
  de NuGet llegara a bloquear una versión nueva de un paquete .NET, sería el mismo problema del
  lado de `dotnet build`; hasta ahora ese equipo compila sin problema.
- Quien cambie `src/web` tiene que correr `npm run build:sitio-compilado` y versionar la
  carpeta en el mismo commit. Si lo olvida, el CI lo detiene; no puede llegar a `main` un sitio
  que no corresponde al código.
- El repositorio suma unos 940 KB, y cada cambio de la SPA reemplaza archivos minificados
  (los nombres llevan el hash del contenido). Es una excepción acotada a la regla de no
  versionar resultados de build.
- Si el build dejara de ser reproducible (por ejemplo, un plugin que incruste la fecha), el
  paso del CI fallaría en cada corrida: habría que corregir el build, no quitar la comprobación.
