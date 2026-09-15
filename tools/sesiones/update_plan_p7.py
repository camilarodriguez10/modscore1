import io, os

root = r"C:\Users\kmiro\Downloads\mod score"

# ---------------------------------------------------------------- PLAN.md
p = os.path.join(root, "PLAN.md")
s = io.open(p, encoding='utf-8').read()

s = s.replace('| **7 — Adaptador de archivos** | 1 sem |', '| **7 — Adaptador de archivos** ✅ | ENTREGADA |')

# Nueva sección de data histórica, antes del plan de entrega
anchor = "## 8. Plan de entrega por fases"
history_section = """## 7bis. Manejo de data histórica

Un Modernization Score aislado dice poco. La pregunta que hace un comité de
portafolio no es *"¿cuánto vale esta aplicación?"* sino *"¿está mejorando?"*.

La historia no es una tabla lateral: **la versión es parte del puerto de fuentes**,
así que los tres adaptadores la manejan igual y el resto del sistema no distingue
entre una foto y una serie.

```csharp
Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(...);
Task<SnapshotSet> GetAllAsync(string? versionId, ...);
```

### 7bis.1 Fase de archivos (entregada)

Una versión es un directorio con nombre ordenable que contiene los CSV de esa
carga más un `manifest.json` con sus metadatos.

```
data/history/
  20260908-212400-a1b2/     eai_master.csv · product_strategy.csv · … · manifest.json
  20261115-093012-c3d4/     …
```

| Aspecto | Cómo se resuelve |
|---|---|
| Qué versión es la vigente | La de captura más reciente |
| Cómo se publica | Staging → validación → ensamblado → `Directory.Move` atómico |
| Colisión de ids | Marca de tiempo **más sufijo aleatorio**: dos cargas en el mismo segundo no pueden pisarse |
| Consulta histórica | `GetAllAsync(versionId)` relee ese directorio |
| Retención | Manual por ahora; el directorio es la unidad de borrado |

### 7bis.2 Fase de base de datos (pendiente, Fase 09)

Un directorio pasa a ser una fila de `snapshot_version`, y cada tabla de hechos
gana `version_id` como parte de su clave.

```sql
snapshot_version(id, captured_at, label, uploaded_by, application_count)
eai_master(version_id, eai_code, …)          PK (version_id, eai_code)
sca_flaw(version_id, flaw_idx_num, …)        PK (version_id, flaw_idx_num)
…
```

| Aspecto | Cómo cambia |
|---|---|
| Puerto `ISourceReader` | **No cambia.** Es el punto de todo el diseño |
| Consulta histórica | `WHERE version_id = @v`, con índice — deja de leer todas las versiones |
| Retención | Política declarativa: conservar N versiones o M meses |
| Concurrencia | Transacción por carga, en lugar de mover un directorio |
| Score precalculado | Tabla `score_snapshot(version_id, eai_code, score, numerator, denominator)` para no recalcular la serie en cada consulta |

**Lo que hace barata esta migración** es que la Fase 07 ya obligó a modelar la
historia. Cambiar el almacenamiento no cambia el contrato, y la suite de contract
tests corre igual contra el tercer adaptador.

### 7bis.3 Lo que la historia hace visible

- **Score junto a su peso disponible en cada punto.** Un salto con el mismo peso
  es mérito real; con más peso puede ser solo más evidencia. La gráfica muestra
  ambos porque sin el segundo el primero engaña.
- **Cada carga es inmutable.** Publicar acumula, no sobrescribe: cualquier score
  histórico es reproducible y auditable.
- **La evolución por aplicación y la del portafolio** comparten la misma serie,
  calculada con el mismo motor.

---

""" + anchor
assert anchor in s
s = s.replace(anchor, history_section, 1)

io.open(p, 'w', encoding='utf-8').write(s)
print("ok PLAN.md")

# ---------------------------------------------------------------- README.md
p = os.path.join(root, "README.md")
s = io.open(p, encoding='utf-8').read()
s = s.replace('| 07 Adaptador de archivos | pendiente |', '| 07 Adaptador de archivos | entregada |')
s = s.replace('274 tests.', '343 tests.')
s = s.replace("""## Regenerar los datos dummy""",
"""## Correr sobre archivos en vez de datos generados

```bash
DataSource__Provider=File DataSource__Directory=./data/history dotnet run --project src/system/Modernization.SystemApi
```

Con el adaptador de archivos, la pantalla **Cargas** permite subir los CSV y
recalcular. Cada carga se publica como una versión consultable, y la evolución del
score aparece en esa misma pantalla.

Para arrancar necesitas al menos una carga. La más simple: genera los dummy,
cópialos a `data/history/<id>/` y escribe su `manifest.json` — o levanta el sistema
con el adaptador Dummy, exporta, y sube los archivos por la interfaz.

## Regenerar los datos dummy""")
io.open(p, 'w', encoding='utf-8').write(s)
print("ok README.md")
