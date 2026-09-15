import io
import os

ROOT = r"C:\Users\kmiro\Downloads\mod score"


def load(name):
    raw = io.open(os.path.join(ROOT, name), encoding="utf-8", newline="").read()
    return raw.replace("\r\n", "\n"), "\r\n" in raw


def save(name, s, crlf):
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(os.path.join(ROOT, name), "w", encoding="utf-8", newline="").write(s)
    print("ok", name)


s, crlf = load("PLAN.md")

# ------------------------------------------------- tabla de fases: 10, 11 y 12
old_row9 = "| **9 — Adaptador BD** | 1 sem | `DatabaseSourceReader`, EF Core 10, migraciones, feature flag | Contract tests verdes contra BD real |"
assert old_row9 in s, "fila 9"
s = s.replace(old_row9, old_row9 + """
| **10 — Experiencia de consulta** | 3 sem | Filtros globales persistentes, estadísticas clicables, roadmap por dimensión, linaje dentro de Cargas, nombres de archivo flexibles | Un filtro elegido en cualquier pantalla sobrevive a la navegación y se puede compartir por URL; hacer clic en "Mexico" filtra el portafolio |
| **11 — Sitio en inglés** | 2 sem | Todo el texto de interfaz en inglés; el backend deja de emitir texto para personas | Ninguna cadena visible nace en el backend; la interfaz pasa una revisión de idioma completa |
| **12 — Administración de reglas** | 3 sem | Editar ruleset, palancas de what-if y parámetros de roadmap desde el portal, solo administradores, con borrador, simulación de impacto y publicación auditada | Cambiar un peso desde el portal mueve el score del portafolio, queda auditado y no requiere recompilar |""", 1)

old_total = "Total: **~16 semanas**. Las fases 7 y 9 son deliberadamente cortas — el trabajo de aislamiento se hace en la fase 2, no cuando llegan los archivos reales."
assert old_total in s, "total"
s = s.replace(old_total, "Total: **~16 semanas** hasta la Fase 9, más **~8 semanas** de la ampliación (fases 10 a 12). Las fases 7 y 9 son deliberadamente cortas — el trabajo de aislamiento se hace en la fase 2, no cuando llegan los archivos reales.", 1)

# ------------------------------------------------------- seccion 8bis nueva
anchor = "## 9. Estructura del repositorio"
assert anchor in s, "seccion 9"

section = """## 8bis. Ampliación solicitada — fases 10 a 12

Siete puntos pedidos el 12 de septiembre de 2026, una vez entregadas las fases 00
a 08. Ninguno cambia el modelo de scoring ni la arquitectura en dos capas; cuatro
son de experiencia de consulta, uno de idioma, uno de ingesta y uno abre una
capacidad nueva —editar las reglas desde el portal— que trae su propia superficie
de riesgo.

| # | Pedido | Fase | Cómo se resuelve |
|---|---|---|---|
| 1 | Filtros en todas las vistas, que perduren al cambiar de menú | 10 | La URL es la fuente de verdad del filtro y se conserva al navegar entre pantallas |
| 2 | Estadísticas clicables que filtren y muestren detalle | 10 | Cada barra, tramo y fila de agregado es un control que agrega su filtro y abre el detalle |
| 3 | Sitio completo en inglés | 11 | El backend deja de emitir texto para personas: devuelve códigos y datos, y la interfaz los traduce |
| 4 | Roadmap por aplicación, portafolio, país, CIO y LOB | 10 | Un parámetro de agrupación sobre el mismo planificador; la proyección sale del mismo motor |
| 5 | Linaje junto con Cargas | 10 | Una sola pantalla "Datos" con dos pestañas y un selector de versión compartido |
| 6 | Archivos con nombres distintos | 10 | El archivo se reconoce por su encabezado, no por su nombre; si hay ambigüedad, la interfaz pregunta |
| 7 | Ajustar fórmulas y escenarios desde el portal (solo administradores) | 12 | Ruleset editable con borrador, simulación de impacto y publicación auditada con reautenticación |

### 8bis.1 Fase 10 — Experiencia de consulta

**Filtros globales (puntos 1 y 2).** Hoy los filtros viven en el estado local de la
pantalla de portafolio y se pierden al navegar. Pasan a un contexto único con la
**URL como fuente de verdad**: `?country=Mexico&lob=Individual` viaja en cada
navegación, sobrevive a un refresco y se puede pegar en un chat. La sesión recuerda
el último filtro para volver a entrar donde uno lo dejó.

- Todas las pantallas de consulta aceptan el mismo conjunto: LOB, país, CIO,
  disposition, roadmap, tramo de score y criticidad.
- Cada agregado —los tramos de la distribución, las barras por LOB y por país, las
  celdas del roadmap— es un control accesible por teclado: al activarlo agrega ese
  filtro y muestra el detalle filtrado.
- Un filtro **nunca amplía** lo que se ve: el alcance del usuario acota primero y el
  filtro acota dentro (ADR 0012). No hay una vía de datos nueva.
- La capa Channel ya recibe estos filtros para el portafolio; se extienden al resto
  de las pantallas y a sus agregados.

**Roadmap por dimensión (punto 4).** `GET /api/roadmap?groupBy=…` con
`application`, `portfolio`, `country`, `cio`, `lob`, `disposition` o `roadmap`. Cada
grupo muestra cuántas aplicaciones tiene, su score actual y el proyectado por el
mismo motor de what-if, más el detalle al abrirlo. La agrupación se calcula sobre el
conjunto ya filtrado y con el alcance aplicado.

**Linaje dentro de Cargas (punto 5).** Una pantalla "Datos" con dos pestañas —
*Cargas* y *Linaje*— que comparten el selector de versión. El linaje pasa a ser por
versión: hoy solo describe la vigente. Sigue exigiendo alcance completo, así que un
usuario limitado ve la pestaña de cargas y no la de linaje.

**Nombres de archivo flexibles (punto 6).** El emparejamiento por nombre obliga a
renombrar los exports antes de subirlos. Pasa a hacerse **por la firma del
encabezado**: cada contrato de fuente ya declara sus columnas obligatorias y sus
alias, así que un archivo se reconoce por lo que trae dentro. Si dos contratos
encajan, o ninguno, la interfaz muestra el encabezado leído y deja asignarlo a mano
antes de publicar.

> El control de seguridad no se relaja: lo que se escribe en disco sigue siendo el
> nombre canónico del contrato, y el nombre que envía el cliente sigue sin usarse
> jamás como ruta (ADR 0010). Reconocer por contenido es, de hecho, más estricto que
> confiar en un nombre.

### 8bis.2 Fase 11 — Sitio en inglés

Hoy hay texto para personas en los dos lados: la interfaz y el backend, que emite
motivos de exclusión, nombres MAPS, resúmenes de carga y detalles de error.

**Decisión: el backend deja de emitir texto para personas.** Devuelve códigos
estables y los datos que los acompañan (`excluded.reasonCode = "sca.no-critical"`
con su métrica y su peso), y la interfaz arma la frase. Es más trabajo que traducir
cadenas, y a cambio:

- el idioma deja de estar repartido entre dos capas;
- las pruebas dejan de depender de frases;
- agregar un segundo idioma después es un catálogo más, no otra migración.

Los logs y los comentarios del código siguen como están: son para quien opera y
mantiene, no para quien usa el portal. Los `ProblemDetails` conservan un `type`
estable y su `detail` pasa a inglés, porque lo leen ambos.

**Criterio de aceptación:** una búsqueda de texto en español sobre las respuestas de
la capa Channel no devuelve nada, y la interfaz pasa una revisión completa.

### 8bis.3 Fase 12 — Administración de reglas

El requisito 7 convierte lo que hoy es un contrato versionado en el repositorio en
algo editable desde el portal: pesos, bandas, casos y compuestos del ruleset;
palancas de what-if; parámetros del roadmap.

**Flujo:** borrador → validación → **simulación sobre todo el portafolio** →
publicación. La simulación es lo que hace que esto no sea peligroso: antes de
publicar se ve cuántas aplicaciones suben, cuántas bajan y cuánto se mueve el
promedio, con el detalle de las más afectadas.

**Cómo encaja con lo que ya existe:**

- El motor ya carga las reglas como datos y **las valida al cargarlas** (pesos que
  no suman su pilar, métricas que apuntan a un pilar inexistente, inputs
  desconocidos). Un borrador inválido se rechaza antes de guardarse.
- Los rulesets pasan de recurso embebido a almacén versionado, con el mismo patrón
  de puerto que las fuentes: archivo primero, base de datos después.
- Cada score ya registra con qué versión de ruleset se calculó, así que un cambio no
  reescribe la historia: las cargas anteriores conservan la suya.

**Seguridad de la nueva capacidad** —quien edita las reglas mueve el score de todo
el portafolio, así que el listón es el más alto del sistema:

| Control | Detalle |
|---|---|
| Rol propio | `ruleset.admin`, separado de `ingestion.write`, y con alcance completo |
| Reautenticación | Publicar exige step-up ante el IdP. Activa el requisito ASVS 3.7.1, hoy N/A por no existir la funcionalidad |
| Auditoría | Evento nuevo por publicación: quién, qué versión, qué cambió y el impacto simulado |
| Reversión | Publicar es crear una versión nueva, nunca editar una publicada; volver atrás es publicar la anterior |
| Límite de frecuencia | Política propia, como la carga |

**Criterio de aceptación:** cambiar un peso desde el portal mueve el score del
portafolio, queda auditado con su impacto, y no requiere recompilar ni desplegar.

---

"""

s = s.replace(anchor, section + anchor, 1)
save("PLAN.md", s, crlf)
