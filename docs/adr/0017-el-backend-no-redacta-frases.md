# ADR 0017 — El backend no redacta frases para personas

**Estado:** aceptada · **Fecha:** 2026-09-12 · **Fase:** 11

## Contexto

El punto 3 de la ampliación pide el sitio completo en inglés. Al inventariarlo había
texto para personas en las dos capas: 347 literales en español dentro del código del
backend —motivos de exclusión, evidencia de alertas, resúmenes de carga, errores de
CSV, pasos de la fórmula, `ProblemDetails`— y todo el texto de la interfaz.

Traducir cadenas en su sitio habría dejado el idioma repartido entre dos capas, con
pruebas que comparan frases y un segundo idioma que exigiría repetir la migración.

## Decisión

**No todo el texto es igual, y cada tipo tiene su lugar.**

| Tipo | Ejemplo | Dónde vive |
|---|---|---|
| **Frase generada** desde datos | "3 open Critical flaws", "Required columns are missing: EAI_CODE" | El backend devuelve `{ code, args }`; la interfaz la redacta con `formatNotice` |
| **Contenido redactado** en un contrato | Definición de Refactor, hitos, reglas de coherencia, notas del ruleset, nombres de palanca | Sigue siendo dato del contrato, traducido en el propio contrato |
| **Error de API** | Filtro inválido, EAI inexistente | `ProblemDetails` con `type` estable (`urn:modscore:problem:invalid-filter`) y `title`/`detail` en inglés |
| **Texto para quien opera** | Logs, errores de arranque, resúmenes OpenAPI, comentarios | Sin cambios |

**Las frases generadas pasan a `Notice`.** Un código estable en minúsculas con puntos
(`csv.missingColumns`) y sus datos como texto invariante (`79.67`, no `79,67`). Los
códigos viven en `NoticeCodes`, y el catálogo de frases de la SPA
(`src/web/src/i18n/notices.ts`) tiene una por código.

**El contenido redactado no se mueve a la interfaz.** Las definiciones MAPS, los hitos
y las reglas de coherencia son contenido del modelo: la Fase 12 los hace editables
desde el portal, y si vivieran en el bundle de la SPA nadie podría editarlos sin
desplegar. Se tradujeron en `roadmap-v1.json`, `ruleset-v1.3.json` y
`source-schemas.json`. Los alias de columna no se tocan: "Suma de Kenna_Score" es como
exporta Power BI, no texto de la interfaz.

**Los tramos de score pasan a códigos** (`critical`, `at-risk`, `acceptable`,
`solid`). Los nombres de la Fase 10 se siguen aceptando como filtro, y las rutas en
español redirigen a las nuevas, para que un enlace compartido entonces no se rompa.

## Consecuencias

- **Un código nuevo sin frase no llega a producción.** Una prueba recorre
  `NoticeCodes` por reflexión y falla si alguno no está en `notices.ts`; otra hace lo
  mismo con los tramos.
- **El criterio de aceptación es una prueba, no una revisión.** Un barrido recorre la
  sesión, el portafolio, las siete agrupaciones del roadmap, el detalle, el cálculo,
  el what-if, los tres tipos de score objetivo, el linaje, las cargas aceptadas y
  rechazadas y los errores de validación, y falla si encuentra texto en español. La
  etiqueta que una persona escribe al cargar queda fuera: es dato suyo.
- **Las pruebas dejaron de comparar frases.** Cerca de cuarenta aserciones pasaron de
  `Contains("otra carga", ...)` a `Code == NoticeCodes.UploadBusy`.
- **Los rechazos guardados en la base llevan el aviso serializado** en la misma
  columna, sin migración. Las filas anteriores a la Fase 11 se leen como "fila no
  convertible" en lugar de romper el linaje.
- **Un error de ruleset ya no se muestra al usuario con su detalle interno.** El 422
  devuelve una frase fija; el mensaje con la métrica y el valor sigue en la excepción,
  para quien opera.
- **Un segundo idioma es un catálogo más** con las mismas claves, más la traducción
  de los contratos. Cuando exista, los contratos pasarán a campos por idioma.
