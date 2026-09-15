# ADR 0009 — La historia es una dimensión del portafolio

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

Un Modernization Score aislado dice poco. La pregunta que un comité de portafolio
hace no es "¿cuánto vale esta aplicación?", sino "¿está mejorando?".

Hasta la Fase 06 el sistema servía una única foto. Añadir historia después, como
una tabla lateral, habría dejado dos caminos de lectura distintos — y con ellos,
dos formas de que los números discrepen.

## Decisión

**La versión es parte del puerto de fuentes**, no un añadido posterior:

```csharp
Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(...);
Task<SnapshotSet> GetAllAsync(string? versionId, ...);
```

Un adaptador que no maneje historia devuelve una sola versión. El resto del
sistema no distingue los dos casos.

**Fase de archivos (ahora).** Una versión es un directorio con nombre ordenable
que contiene los CSV de esa carga y un `manifest.json`:

```
data/history/
  20260908-212400-a1b2/   eai_master.csv · product_strategy.csv · … · manifest.json
  20261115-093012-c3d4/   …
```

La carga más reciente es la vigente. Las anteriores siguen consultables.

**Fase de base de datos (después).** Un directorio pasa a ser una fila de una
tabla `snapshot_version`, y cada tabla de hechos gana su `version_id` como parte
de la clave. El puerto no cambia, así que tampoco cambia nada aguas arriba: el
motor, el what-if, el roadmap y las seis pantallas siguen igual.

## Consecuencias

- **El score se lee junto a su peso disponible en cada punto.** Un salto de score
  con el mismo peso es mérito real; con más peso puede ser solo más evidencia. La
  gráfica muestra ambos porque sin el segundo el primero engaña.
- **Cada carga es inmutable.** Publicar no sobrescribe: acumula. Eso hace posible
  reproducir cualquier score histórico y auditar cuándo cambió algo.
- **La escritura es atómica.** Los archivos se validan y se ensamblan en un
  directorio de staging; solo si el conjunto produce un portafolio con
  aplicaciones se mueve a su versión definitiva. Una carga rechazada deja el
  portafolio vigente intacto.
- **Los ids llevan sufijo aleatorio además de la marca de tiempo.** Con
  granularidad de segundos, dos cargas simultáneas producirían el mismo id y la
  segunda sobrescribiría a la primera sin aviso. Lo detectó un test antes de que
  llegara a producción.
- **Coste de la lectura histórica.** Dibujar la evolución de una aplicación lee
  todas las versiones. Con archivos y pocas cargas es despreciable; con muchas
  versiones pide un índice, que es exactamente lo que la fase de base de datos
  aporta.
