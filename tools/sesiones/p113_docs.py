from pathlib import Path
from patchlib import patch, ROOT

ROOT_PATH = ROOT


def sub(relative, old, new, count=None):
    p = ROOT_PATH / relative
    t = p.read_text(encoding="utf-8", newline="")
    n = t.count(old)
    assert n >= 1 and (count is None or n == count), (relative, n, old[:60])
    p.write_text(t.replace(old, new), encoding="utf-8", newline="")
    print("ok", relative, n)


# Conteo de parametros: 14 -> 15 (se agrego code.topActions).
sub("HANDOFF.md", "14 parámetros", "15 parámetros")
sub("PLAN.md", "14 parámetros editables", "15 parámetros editables")
sub(r"docs\adr\0026-el-analisis-de-codigo-llega-de-github-enterprise.md", "reautenticación, auditoría y versiones: 14 parámetros —ventana de *ending soon*, mayores",
    "reautenticación, auditoría y versiones: 15 parámetros —ventana de *ending soon*, mayores")
sub(r"docs\adr\0026-el-analisis-de-codigo-llega-de-github-enterprise.md", "los puntos de cada hallazgo, los tres niveles y el largo de la lista— y",
    "los puntos de cada hallazgo, los tres niveles y el largo de las dos listas— y")
sub(r"docs\PENDING-ITEMS.md", "Its 14 parameters are a new `code` area", "Its 15 parameters are a new `code` area")
sub(r"docs\PENDING-ITEMS.md", "All 14 values are editable", "All 15 values are editable")
sub(r"docs\plan\modscore-plan.html", "14 parámetros editables desde el portal", "15 parámetros editables desde el portal")

# ADR 0026: la ampliacion del 2026-09-27.
patch(r"docs\adr\0026-el-analisis-de-codigo-llega-de-github-enterprise.md", [
("""## Alternativas consideradas""",
"""### 5. Ampliación del 2026-09-27: en el tiempo y qué cambiar

- **Si nada cambia, hoy y en uno, dos y tres años.** Cada componente se evalúa contra su fecha
  de fin de vida en la fecha del horizonte; las alertas y las versiones quedan como están y
  no hay commits nuevos, así que un repositorio cruza el umbral de abandono con el tiempo. Por
  horizonte: componentes vencidos, aplicaciones afectadas, **vigencia del código** (la parte
  de los componentes evaluables que sigue con soporte, de 0 a 100) e índice de riesgo. No se
  inventan alertas ni fechas futuras: un ciclo sin fecha publicada cuenta como vigente.
- **Qué cambiar y cuándo.** Por componente, el destino es el ciclo publicado con soporte que
  más dura, LTS cuando el producto las tiene, y que no vence dentro de la ventana de
  *ending soon*; si el producto no tiene ninguno, el que lo reemplaza (AngularJS → Angular), y
  si ni eso, *no newer supported release yet*. Urgencia: ya (vencido, biblioteca obsoleta o
  con alertas, repositorio abandonado), dentro de uno, dos o tres años por la fecha de fin de
  vida, o planificar (biblioteca solo atrasada). Cada sugerencia dice cuántos puntos baja hoy
  el índice y cuántos evita cuando el componente venza. El plan del portafolio agrupa la
  misma acción en todos los repositorios donde aplica.
- **Score, Currency Score y riesgo por carga.** La tendencia del portafolio y la historia de
  cada aplicación llevan, además del score, el Currency Score de CMT y el Risk Score
  promedio, calculados con las reglas vigentes.
- **Datos de ejemplo en el tiempo.** Las versiones anteriores del modo Dummy dejaron de ser
  un portafolio distinto por trimestre: son las mismas aplicaciones con trayectorias
  deterministas (`DummyTimeline`), y `data/timeline` las trae como cuatro cargas listas para
  leer. La proyección no depende de ellas: sale de las fechas de fin de vida.

## Alternativas consideradas"""),
])

# PLAN 8.11: la ampliacion.
patch("PLAN.md", [
("""**C2 — la integración con GitHub Enterprise** está en [8ter](#en-paralelo--integración-con-github-enterprise-pantalla-code)""",
"""**C1 ampliada el 2026-09-27.** Tres pedidos más sobre la misma pantalla:

| Pedido | Cómo quedó |
|---|---|
| El estado actual contra la obsolescencia estimada en uno, dos y tres años si nada cambia | *Where is obsolescence heading if nothing changes?*: por horizonte, componentes vencidos, aplicaciones afectadas, vigencia del código (parte de los componentes con soporte) e índice de riesgo. Lo mismo por aplicación en su detalle, y una columna «In 3 years» en la tabla |
| La sugerencia de mejora que debe aplicarse | *What should change first?*: el plan del portafolio, cada acción una vez con sus repositorios y aplicaciones, por urgencia y por puntos que baja o evita; y la lista propia de cada aplicación, con sus repositorios abandonados. Destino: el ciclo con soporte que más dura, LTS cuando existe |
| Al menos tres bases dummy nuevas para comparar score, Currency y riesgo en el tiempo | `data/timeline`: el mismo portafolio en Q4 2025, Q1, Q2 y Q3 2026 (cuatro cargas con su manifiesto). El modo Dummy trae los mismos cuatro trimestres. *How did score, currency and risk move across uploads?* compara las tres medidas carga por carga, y cada aplicación muestra la suya |

El mismo día se retiró el paquete portable: la aplicación se levanta desde el código con
comandos para cmd y PowerShell, y un equipo que bloquea `.exe` propios usa el sitio compilado
servido por la Channel.

**C2 — la integración con GitHub Enterprise** está en [8ter](#en-paralelo--integración-con-github-enterprise-pantalla-code)"""),
])

# HANDOFF 6quater: piezas nuevas.
patch("HANDOFF.md", [
("""- **SPA:** `routes/CodePage.tsx`, `i18n/code.ts`, `codeExplanation` en `i18n/analytics.ts`,
  la sección *Code* de `DefinitionsPage.tsx` y la pestaña *Code* en la navegación.""",
"""- **SPA:** `routes/CodePage.tsx`, `i18n/code.ts`, `codeExplanation` en `i18n/analytics.ts`,
  la sección *Code* de `DefinitionsPage.tsx` y la pestaña *Code* en la navegación.
- **En el tiempo (2026-09-27):** `CodeOutlook` en `Modernization.Insights` —horizontes a 0,
  1, 2 y 3 años, sugerencias por repositorio, destino de cada actualización (`BestCycle`) y
  el plan del portafolio—; `DashboardTrendPoint` y `ScoreHistoryPoint` llevan además Currency
  y Risk Score por carga (`DashboardAnalyzer.RiskScoreOf`); `DummyTimeline` en
  `Modernization.Sources/Dummy` rebobina el portafolio trimestre a trimestre, y
  `tools/Modernization.DummyData` escribe `data/timeline` con cuatro cargas."""),
("""- **El índice es una suma sin tope**: una aplicación con muchos repositorios acumula más.
  Si el negocio lo quiere normalizado, es código (GH-07).""",
"""- **El índice es una suma sin tope**: una aplicación con muchos repositorios acumula más.
  Si el negocio lo quiere normalizado, es código (GH-07).
- **«Si nada cambia» es literal:** mismas versiones, mismas alertas, ningún commit. La
  proyección no inventa vulnerabilidades ni fechas; un ciclo sin fecha publicada cuenta como
  vigente. Por eso la vigencia del código cae rápido el primer año con los datos de ejemplo:
  .NET 8 y 9, Python 3.10, Spring y Ubuntu 22.04 vencen entre fines de 2026 y mediados de 2027.
- **El destino de una actualización sale del catálogo** (LTS que más dura y fuera de la
  ventana). Si el catálogo se regenera, los destinos cambian solos; las pruebas de
  `EachComponentGoesToTheRightTarget` fijan algunos y hay que revisarlas.
- **La historia del modo Dummy cambió de naturaleza el 2026-09-27:** antes cada trimestre era
  otro portafolio (otra semilla, otros EAI); ahora son las mismas aplicaciones. Las pruebas no
  dependían de lo anterior."""),
])

# PENDING: resumen de lo hecho.
patch(r"docs\PENDING-ITEMS.md", [
("""- **Fixed on 2026-09-18:** the Insights risk index counted Kenna **below 50** as weak vulnerability management.""",
"""- **Code screen over time** (2026-09-27): the outlook today and in one, two and three years if nothing changes (components past end of life, share of code still supported, code risk), an improvement plan with a target release and an urgency per action, and score, Currency Score and Risk Score per upload. `data/timeline` holds the same dummy portfolio in four quarterly uploads, and the Dummy mode now rewinds the same applications instead of generating a different portfolio per quarter.
- **Fixed on 2026-09-18:** the Insights risk index counted Kenna **below 50** as weak vulnerability management."""),
])
