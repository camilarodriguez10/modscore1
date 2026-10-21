# Modernization Score Platform — Plan de Diseño e Implementación

> Reconstrucción del cálculo de **Modernization Score v1.3** con fuentes dummy, backend **.NET 10** en microservicios y SPA **React 19**.
> Base: 34 capturas del BRD "Modernization Scoring Tool", el dashboard Power BI v1.3 y los archivos fuente (`0809 *.csv/xlsx`).

---

## 0. Resumen ejecutivo

| Tema | Decisión |
|---|---|
| Fórmula | `Score = 20 + (Numerador ÷ Denominador) × 80` — **verificada contra dos dashboards reales** |
| Backend | .NET 10 (LTS), API-led en **2 capas**: `channel-api` (BFF/orquestador) + `system-api` (reglas de negocio + fuentes) |
| Frontend | React 19 + Vite + TypeScript, patrón **BFF** (sin tokens en el browser) |
| Datos | Puerto `IDataSource` con 3 adaptadores intercambiables: **Dummy → Archivos → Base de datos** (req. 8 y 9) |
| Motor de score | Reglas versionadas en JSON (`ruleset-v1.3.json`), sin recompilar para cambiar pesos |
| What-if | Motor de simulación sobre el mismo engine determinista, con ranking de palancas por puntos ganados |
| Seguridad | Zero-trust: OIDC + BFF cookie, mTLS entre servicios, SBOM, escaneo SCA/SAST bloqueante en CI |

---

## 1. Modelo de scoring reconstruido (desde las imágenes)

### 1.1 Fórmula normalizada

```
Score = 20 + (Numerador ÷ Denominador) × 80

Numerador   = Σ (peso_i × valor_i / 100)   para toda métrica CON dato
Denominador = Σ (peso_i)                   para toda métrica CON dato
```

Reglas de "ALL Sections" del BRD (captura `image_67142913`):
- *"Null or zero values are excluded from scoring"*. El nulo se excluye siempre. El **cero** se excluye donde la tabla de la métrica no lo nombra como dato propio (Kenna, OSS, RAW Tech, Operational); donde sí lo nombra, manda la tabla (Tech Debt `0 → 100`, SCA `None → 100`, MFA `No (with no users) → 100`). Cada métrica lo declara con `zeroIsNoData` en el ruleset — ver [ADR 0019](docs/adr/0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md) y RFC-13.
- Las métricas con *"No Data Available"* se excluyen **del numerador y del denominador**.
- Todo resultado se normaliza al rango **20–100** (nunca 0).
- La **misma fórmula** se aplica a cada pilar y al score global.
- Ante una contradicción entre documentos, **mandan las capturas del BRD y del dashboard** (índice en [docs/brd/README.md](docs/brd/README.md)).

### 1.2 Verificación de la fórmula (crítica — esto valida todo el modelo)

**Caso A — EAI 10969** (dashboard v1.3):

| Pilar | Numerador | Denominador | Score calculado | Dashboard |
|---|---|---|---|---|
| Modernization Risk Vectors | 1.60+7.00+1.60+7.00+6.00 = **23.20** | 8+7+8+7+10 = **40** | 20+(23.20/40)×80 = **66.40** | 66,40 ✅ |
| Architecture & Maturity | 5.00+5.00+7.57+8.84 = **26.41** | 5+5+10+10 = **30** | 20+(26.41/30)×80 = **90.42** | 90,42 ✅ |
| Stability | 5.00+5.00 = **10.00** | 5+5 = **10** | 20+(10/10)×80 = **100.00** | 100,00 ✅ |
| **Global** | **59.61** | **80** | 20+(59.61/80)×80 = **79.61** | **79,61** ✅ |

**Caso B — EAI 14381** (ejemplo del BRD): Numerador 17.00+21.75+6.00 = 44.75, Denominador 25+25+10 = 60 → `20 + (44.75/60)×80 = 79.67` ✅ (coincide con el dashboard del BRD: Risk 74.40, Arch 89.60, Stability 68.00, Global 79.67).

> El score global se calcula sobre **métricas planas**, no como promedio ponderado de los pilares. Los pilares son vistas agregadas de los mismos términos.

### 1.3 Catálogo de pesos (ruleset v1.3)

**Modernization Risk Vectors — 50%**

| Métrica | Peso | Fuente |
|---|---|---|
| Kenna Score | 8 | CMT (App Sec Kenna Application Report) |
| DR In-Scope | 7 | EAI (Application Security and IT Risk) |
| SCA Flaws (Critical) | 10 | SCA Scan (Veracode) |
| SCA Flaws (High) | 8 | SCA Scan (Veracode) |
| SCA Flaws (Medium) | 0 | *removida del score* |
| SCA Flaws (Low) | 0 | *removida del score* |
| MFA | 7 | EAI (Application Security and IT Risk) |
| Open-Source Score | 10 | RPT (Risk Profile Tool) |

**Architecture & Maturity — 40%**

| Métrica | Peso v1.3 | Sub-pesos BRD | Fuente |
|---|---|---|---|
| RAW Tech Score | 5 | 5 | CMT (Currency Model Detail Report) |
| Native REST APIs / APIM | 5 | 5 | API & DataPower |
| Architectural Value (compuesto) | 10 | Modern 5, Obsolete 1, Heritage 2, Not Approved 3 | CMT |
| Product Strategy (compuesto) | 10 | Invest 4, Tolerate 1, Eliminate 2, Migrate 3 | MTS |
| Tech Debt Score | 10 | 5 | EAI |
| Automation DevSecOps | 0 | 0 | *removida del cálculo* |
| Containerized | 0 | 4 (tachado en BRD) | OCP — *removida* |
| Coding Language / Application Disposition | — | NULL (solo se muestran) | EAI |

> Con Containerized, la tabla del BRD suma 40: RAW 5 + API 5 + Containerized 4 + Architectural Value 11 + Product Strategy 10 + Tech Debt 5. El texto de Containerized agrega *"This score is added to product strategy (2 pts) and architecture value (2 pts)"*. El dashboard v1.3 usa otra distribución (compuestos 10 y 10, Tech Debt 10), y el **ejemplo trabajado del propio BRD para EAI 14381** la confirma: su denominador de Architecture es 25 = RAW 5 + Architectural Value 10 + Product Strategy 10. Por eso el ruleset sigue al dashboard (RFC-01, RFC-03).

**Stability — 10%**

| Métrica | Peso | Fuente |
|---|---|---|
| Monitoring | 5 | Elastic / APM / Synthetics |
| Operational Score | 5 | CMT (ServiceNow, ventana 12 meses) |

Total: 50 + 40 + 10 = **100**.

### 1.4 Tablas de mapeo valor → score (0–100)

| Métrica | Regla |
|---|---|
| **Kenna Score** | 670–1000 (Critical/High) → **20** · 340–669 (Medium) → **60** · 1–339 (Low) → **100** · 0 → sin dato |
| **DR In-Scope** | Yes → **100** · No → **20** |
| **SCA Flaws** | High/Critical → **20** · Medium → **20** · Low → **60** · None → **100** |
| **MFA** | Yes (con usuarios) → **100** · Yes (sin usuarios) → **100** · **No (con usuarios) → 20** · No (sin usuarios) → **100** |
| **Open-Source Score** | 75–100 → **100** · 60–74 → **60** · 0–59 → **20** · 0 exacto → sin dato |
| **RAW Tech Score** | 75–100 → **100** · 60–74 → **60** · 0–59 → **20** · 0 exacto → sin dato |
| **Native REST APIs / APIM** | APIM → **100** · APIC → **100** · SOAP → **20** · DataPower/SOAP Libs → **20** |
| **Architectural Value** (por producto) | Modern → **100** · Heritage → **60** · Obsolete → **20** · Not Approved for Use → **20** |
| **Product Strategy** (por producto) | Invest → **100** · Tolerate → **60** · Eliminate → **20** · Migrate → **20** |
| **Tech Debt Score** | 2.5–5 → **20** · 0.1–2.4 → **60** · 0 → **100** |
| **Operational Score** | 80–100 → **100** · 60–79.99 → **60** · 0–59.9 → **20** · 0 exacto → sin dato |
| **Monitoring** | Elastic Agent **o** Synthetic **o** APM habilitado → **100** · ninguno → **20** |
| **Application Disposition / Roadmap** | Maintain (Evolve) → **100** · Maintain (Patch) → **60** · Rehost → **60** · Refactor → **20** · Reimagine → **20** · Retire → **20** — *actualmente removida del cálculo, se muestra*. `Consolidate → 20` no está en la tabla del BRD: viene del modelo MAPS y es supuesto (RFC-15) |
| **Automation DevSecOps** | 5 → **100** · 4 → **60** · 0/NULL–3 → **20** — *removida*. El BRD puntúa el NULL con 20; hoy el nulo se excluye (RFC-14) |
| **Containerized** | Yes → **100** · No → NULL (sin score) — *removida* |

**Compuestos** (Architectural Value y Product Strategy) — promedio ponderado por conteo:

```
ArchValue = (100·Modern + 60·Heritage + 20·Obsolete + 20·NotApproved) / (Modern+Heritage+Obsolete+NotApproved)
ProdStrat = (100·Invest + 60·Tolerate + 20·Eliminate + 20·Migrate) / (Invest+Tolerate+Eliminate+Migrate)
```

> ⚠️ **Supuesto a validar (RFC-01).** El BRD asigna un peso a cada conteo por separado (Modern 5, Obsolete 1, …), mientras el dashboard v1.3 muestra un único compuesto de peso 10. El promedio ponderado por conteo reproduce el orden de magnitud observado (ej. 15 Modern + 1 Heritage → 97.50, coincidiendo con EAI 14381). Se implementa como **estrategia intercambiable** (`CountWeightedAverage` vs `PerCountBanded`) configurable en el ruleset.

### 1.5 Otros supuestos abiertos

| # | Tema | Estado |
|---|---|---|
| RFC-01 | Compuestos ArchValue / ProductStrategy | Dos estrategias implementadas, default = promedio ponderado |
| RFC-02 | **Currency Score** (94,66 en dashboard) | No definido en el BRD. Se modela como KPI independiente del CMT (% de componentes vigentes), **fuera** del Modernization Score |
| RFC-03 | Tech Debt peso 5 (BRD) vs 10 (dashboard) | Default = **10**, el peso publicado en el dashboard. Ningún caso dorado tiene Tech Debt, así que la elección **no está verificada numéricamente** |
| RFC-04 | "Override rule" (dashboard muestra dos valores) | Se modela como `ScoreOverride` con auditoría (quién, cuándo, por qué) |
| RFC-05 | SCA: ¿cuenta flaws abiertos o presencia? | Default = presencia de ≥1 flaw abierto de esa severidad. Resuelto en [ADR 0003](docs/adr/0003-ausencia-de-dato-frente-a-valor-cero.md): app presente en el archivo = escaneada; ausente = sin dato |
| RFC-08 | Huecos entre bandas del BRD | Operational Score no define (59,9 · 60) ni (79,99 · 80); Tech Debt no define (0 · 0,1) ni (2,4 · 2,5). El motor falla con error accionable — ver [ADR 0004](docs/adr/0004-huecos-entre-bandas-del-brd.md) |
| RFC-10 | Claim de alcance del IdP | La autorización por LOB/CIO lee `portfolio_scope` con valores `*`, `lob:…`, `country:…`, `cio:…`; el nombre es configurable. Sin claim no se ve nada — ver [ADR 0012](docs/adr/0012-el-alcance-se-aplica-sobre-los-datos.md) |
| RFC-11 | Cache compartido de sesiones | Sesiones en el servidor con cache en memoria: una réplica de channel-api hasta registrar Redis — ver [ADR 0011](docs/adr/0011-la-sesion-vive-en-el-servidor.md) |
| RFC-12 | Identidad de carga de trabajo | Token de servicio estático con rotación sin corte y mTLS; tokens de vida corta según la plataforma del cluster — ver H-16 en [hallazgos](docs/security/hallazgos.md) |
| RFC-13 | "Null or zero values are excluded" frente a tablas que puntúan el 0 | La tabla específica gana; el 0 se excluye en Kenna, OSS, RAW Tech y Operational (`zeroIsNoData`) — ver [ADR 0019](docs/adr/0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md) |
| RFC-14 | DevSecOps `0/NULL–3 → 20` puntúa el nulo | Sin efecto hoy (peso 0). Si recupera peso, decidir si el nulo puntúa 20 o se excluye |
| RFC-15 | `Consolidate` en la disposición | No figura en la tabla de puntaje del BRD; se mapea a 20 por venir de "Eliminate" en MAPS. Peso 0, solo afecta al roadmap |
| RFC-16 | Containerized "added to product strategy (2 pts) and architecture value (2 pts)" | El dashboard y el ejemplo 14381 usan compuestos de peso 10; se documenta la discrepancia con la tabla del BRD (AV 11, PS 10, Containerized 4, Tech Debt 5) |
| RFC-17 | Filas con `EAI Code = 0` en `OSS_Score_by_EAI` | No corresponden a aplicaciones; hoy no cruzan con ninguna. Confirmar si deben reportarse como fila huérfana en la carga |

---

## 2. Fuentes de datos (dummy → archivos → base de datos)

### 2.1 Esquemas identificados en las capturas

| Fuente lógica | Archivo real observado | Campos clave |
|---|---|---|
| **EAI Master** | *EAI TABLE* | `EAI_Code`, `Application_Name`, `Application_Status`, `Application_Disposition`, `CIO`, `CRE`, `LOB`, `SUB_LOB`, `Region Name`, `Country Name`, `Critical Application Indicator`, `Cloud_Indicator`, `Cloud_Type`, `Cloud_Vendor`, `Coding_Language`, `DR_In_Scope`, `MFA`, `Does this Application Utilize MFA?`, `Internet_Facing`, `Kenna_Score`, `Raw_Tech_Score`, `Operational_Score`, `L1_Business_Capability_Count`, `Primary_Business_Capability`, `Recovery_Point_Objective_Range`, `Recovery_Time_Objective_Range`, `User Count`, `Year/Quarter/Month/Day` |
| **Architectural Value** | *(CMT)* | `EAI_CODE`, `Modern_Count`, `Heritage_Count`, `Obsolete_Count`, `Not_Approved_for_Use_Count` |
| **Product Strategy** | `0809 PRODUCT STRATEGY` | `EAI_CODE`, `Eliminate_Count`, `Invest_Count`, `Migrate_Count`, `Tolerate_Count` |
| **Tech Debt** | `0809 tech_debt` | `APP_ID`, `COMBINED_DENIAL_SCORE`, `COMBINED_ACCEPTED_SCORE`, `COMBINED_APP_SCORE`, `LAST_UPDATED_DATE`. Desde el 2026-10-08 solo se cargan `APP_ID` y `COMBINED_APP_SCORE` |
| **SCA Flaws** | `0809 veracode_sca_open_flaws_6` | `FLAW_IDX_NUM`, `APP_NM`, `EAI_CD`, `APP_ID`, `SVR_DSC` (1-Critical/2-High/3-Medium), `FND_STT`, `FRST_FND_DT`, `LST_FND_DT`, `RSLV_DT`, `FND_AGE`, `SCAN_TY`, `ROPEN_F`, `CVE_ID`, `CVSSV3_SCR`, `VUL_NM`, `LIB_NM`, `LIB_VND`, `PUB_DT`, `ACCSBLT` (Internal/External) |
| **OSS Score** | `0809 OSS_Score_by_EAI_3` | `EAI Code`, `OSS Score`, `OPEN_SRC_SFTWR_HLTH_SCOR_UPDT_TS` |
| **Monitoring** | `0809 MONITOREO` | `EAI`, `Agent`, `APM`, `Monitoring`, `Synthetics` (booleanos) |
| **APIM Onboarding** | `0809 APIM_Onboarded_EAIs_LATAM` | `EAI`, `Country`, `Application Short Name`, `Application Name` |
| **Kenna** | *CMT App Sec Kenna Application Report* | `EAI_CODE`, `Kenna_Score` |

### 2.2 Estrategia de evolución (requisitos 8 y 9)

Contrato único, tres implementaciones. **El motor de scoring nunca cambia.**

```
        ┌──────────────────── Canonical Model (inmutable) ────────────────────┐
        │  ApplicationSnapshot · MetricObservation · SourceProvenance         │
        └───────────────────────────────▲────────────────────────────────────┘
                                        │  Anti-Corruption Layer
        ┌───────────────────────────────┴────────────────────────────────────┐
        │                   ISourceReader<TRecord>  (puerto)                 │
        └──┬──────────────────────┬───────────────────────┬──────────────────┘
     Fase 1│                 Fase 2│                  Fase 3│
   ┌───────▼────────┐   ┌──────────▼─────────┐   ┌─────────▼──────────┐
   │ SeededDummy    │   │ FileSourceReader   │   │ DatabaseSource     │
   │ Reader         │   │ (CSV/XLSX,         │   │ Reader             │
   │ Bogus + seed   │   │  CsvHelper +       │   │ EF Core 10 /       │
   │ determinista   │   │  ClosedXML)        │   │ Dapper, read-only  │
   └────────────────┘   └────────────────────┘   └────────────────────┘
```

Puntos clave del diseño:

1. **Un solo modelo canónico.** Los tres adaptadores producen los mismos DTOs. Cambiar de fase es cambiar `"DataSource:Provider": "Dummy" | "File" | "Database"` en configuración.
2. **Contratos de esquema versionados.** Cada fuente declara su schema en `contracts/*.schema.json` con nombres de columna, tipos, nullability y aliases. El `FileSourceReader` valida contra el contrato y falla con error accionable (columna faltante, tipo inválido) en lugar de producir scores silenciosamente incorrectos.
3. **Provenance obligatorio.** Cada `MetricObservation` lleva `SourceSystem`, `SourceFile`, `SourceField`, `ExtractedAt`, `RowHash`. Esto alimenta directamente la pantalla de "cómo se calculó" (requisito 4) y hace la migración a BD auditable.
4. **Contract tests compartidos.** Una única suite (`SourceReaderContractTests`) corre contra los 3 adaptadores. Migrar a archivos o a BD no puede romper el score sin que un test lo detecte.
5. **Golden dataset.** Los EAI **10969** y **14381** de las capturas quedan como fixtures con sus scores esperados (79.61 y 79.67). Test de regresión permanente.

### 2.3 Datos dummy

- Generación determinista con `Bogus` + seed fijo → mismos datos en cada corrida, diffs limpios.
- **~250 aplicaciones** sintéticas repartidas en LOBs/países LATAM (Chile, México, Brasil), replicando la distribución observada.
- Cobertura intencional de casos borde: apps sin datos de SCA, sin Tech Debt, sin monitoreo, con conteos en cero, con override activo.
- Se emiten **también como CSV** en `data/dummy/` con exactamente los nombres de columna reales → la Fase 2 se prueba desde el día 1 sin esperar archivos productivos.

---

## 3. Arquitectura de microservicios

### 3.1 Arquitectura por capas — API-led

Dos capas de API con responsabilidades estrictamente separadas. Una regla de negocio jamás vive en la capa Channel; una preocupación de canal jamás vive en la capa System.

```
┌──────────────────────────────────────────────────────────────────────┐
│  React 19 SPA                                       (otros canales)  │
└─────────────────────────────┬────────────────────────────────────────┘
                              │ HTTPS · cookie httpOnly · CSRF
┌─────────────────────────────▼────────────────────────────────────────┐
│  CAPA CHANNEL  ·  channel-api                     BFF / ORQUESTADOR  │
│                                                                      │
│  · Termina OIDC; los tokens nunca llegan al browser                  │
│  · Compone la respuesta de cada pantalla en una sola llamada         │
│  · Traduce el modelo canónico a view-models por pantalla             │
│  · Rate limiting, CSRF, cabeceras de seguridad, CSP estricta         │
│  · Fan-out/fan-in y agregación con Polly                             │
│                                                                      │
│  ✗ CERO reglas de negocio · ✗ CERO acceso a fuentes de datos         │
└─────────────────────────────┬────────────────────────────────────────┘
                              │ mTLS · token de servicio · JSON canónico
┌─────────────────────────────▼────────────────────────────────────────┐
│  CAPA SYSTEM  ·  system-api          REGLAS DE NEGOCIO + FUENTES     │
│                                                                      │
│   ┌────────────────────────────────────────────────────────────┐     │
│   │  NÚCLEO DE DOMINIO         puro · determinista · sin I/O   │     │
│   │  Scoring.Engine  ·  WhatIf  ·  Roadmap  ·  Rulesets        │     │
│   └───────────────────────────▲────────────────────────────────┘     │
│                    puertos    │    ISourceReader<T>                  │
│   ┌───────────────────────────┴────────────────────────────────┐     │
│   │  ADAPTADORES     Dummy  │  File (CSV/XLSX)  │  Database    │     │
│   └────────────────────────────────────────────────────────────┘     │
│                                                                      │
│  ✗ CERO conocimiento de pantallas, sesiones o canales                │
└──────────────────────────────────────────────────────────────────────┘
```

| Capa | Servicio | Responsabilidad | Nunca hace |
|---|---|---|---|
| **Channel** | `channel-api` | Orquestación y composición por pantalla, autenticación, sesión, seguridad de borde. Un servicio por canal (hoy web; mañana móvil o Power BI reutilizan la misma capa System). | Calcular un score. Leer un archivo o una tabla. |
| **System** | `system-api` | Motor de scoring, what-if, roadmap, rulesets versionados y conexión a todas las fuentes vía puertos. Expone recursos canónicos completos. | Saber qué pantalla lo llamó. Componer view-models. |

**Por qué dos capas y no seis servicios.** La capa Channel absorbe todo lo que cambia con el canal (una pantalla nueva, un cliente móvil) sin tocar reglas de negocio. La capa System absorbe todo lo que cambia con el negocio o con las fuentes (un peso, un ruleset, la migración a base de datos) sin romper contratos de canal. Los requisitos 8 y 9 caen enteros del lado System, detrás de un puerto — por eso migrar de dummy a archivos y luego a base de datos no toca ni una línea del channel-api ni del frontend.

**Evolución documentada.** El `system-api` está modularizado internamente en `Scoring`, `WhatIf`, `Roadmap` y `Sources`, cada uno con su propio contrato interno. Cuando el volumen lo justifique, cualquiera de esos módulos se extrae como System API independiente (`scoring-system-api`, `sources-system-api`) sin cambiar el contrato que consume el channel-api. Si aparece orquestación compleja entre varios System APIs, se inserta una capa Process entre ambas — el patrón lo admite sin rediseño.

**El motor como librería, no como llamada de red.** `Modernization.Scoring.Engine` es una librería referenciada por el módulo de scoring y por el de what-if dentro del mismo proceso. Esto garantiza que el score baseline y el score simulado se calculan con el mismo código, bit a bit. Un what-if que invocara al scoring por HTTP podría divergir; este diseño lo hace imposible.

### 3.2 Stack .NET 10

| Componente | Elección | Justificación |
|---|---|---|
| Framework | .NET 10 LTS, Minimal APIs | Menor superficie que MVC |
| Channel API | **YARP 2.x** para el proxy inverso | De Microsoft, sin dependencias transitivas de terceros |
| Validación | **FluentValidation** | Whitelist explícita de entradas |
| Persistencia | **EF Core 10** (fase 3) + **Dapper** (lecturas calientes) | |
| CSV / XLSX | **CsvHelper**, **ClosedXML** | Maduros, sin CVEs activos; ClosedXML evita el COM de Excel |
| Datos dummy | **Bogus** | Determinista con seed |
| Mensajería | **MassTransit + RabbitMQ** | Eventos de ingestión |
| Observabilidad | **OpenTelemetry** → OTLP | Trazas distribuidas end-to-end |
| Resiliencia | **Polly v8** (`Microsoft.Extensions.Http.Resilience`) | Circuit breaker, timeouts |
| Tests | xUnit v3, FluentAssertions, Testcontainers, **Verify** (snapshot de breakdowns) | |
| Contratos API | OpenAPI nativo de .NET 10 | Genera el cliente TS del front |

> **Deliberadamente NO se usa:** AutoMapper (mapeo explícito), Newtonsoft.Json (`System.Text.Json` con `JsonSerializerOptions` estrictos y source generators), ni ningún paquete sin release en los últimos 12 meses.

### 3.3 Diseño del motor de scoring

```csharp
// Modernization.Scoring.Engine — determinista, puro, sin I/O
public sealed record MetricEvaluation(
    string   MetricKey,          // "mfa", "kenna", "sca.critical"
    string   PillarKey,          // "risk" | "architecture" | "stability"
    object?  RawValue,           // "No", 670, 28.95
    string   RawValueDisplay,    // "No (with users)"
    decimal? MappedScore,        // 20  · null = No Data Available
    decimal  Weight,             // 7
    decimal? WeightedScore,      // 1.40
    bool     IsIncluded,
    string   ExclusionReason,    // "No data available in EAI source"
    string   RuleApplied,        // "No (with users) => 20"
    SourceProvenance Provenance);

public sealed record ScoreBreakdown(
    string EaiCode,
    string RulesetVersion,       // "v1.3"
    decimal Numerator,
    decimal Denominator,
    decimal Score,               // 20 + (Num/Den)*80
    IReadOnlyList<PillarBreakdown> Pillars,
    IReadOnlyList<MetricEvaluation> Metrics,
    ScoreOverride? Override,
    DateTimeOffset CalculatedAt);
```

Principios:
- **Puro y determinista.** Sin `DateTime.Now`, sin acceso a red ni disco. Entrada → salida reproducible. Todo se testea con tabla de casos.
- **Reglas como datos.** `ruleset-v1.3.json` contiene pesos, bandas y estrategias. Cambiar un peso = publicar un ruleset nuevo, no un deploy. Los rulesets son **inmutables y versionados**; cada score guarda con qué versión se calculó.
- **`decimal`, nunca `double`.** Evita drift de punto flotante en las comparaciones contra el dashboard.
- **Explicabilidad de primera clase.** El breakdown no es logging: es el contrato de la API que alimenta la pantalla del requisito 4.

---

## 4. What-if: motor de escenarios (requisito 5)

### 4.1 Mecánica

Cambiar una métrica altera numerador **y** denominador cuando la métrica pasa de "sin dato" a "con dato":

```
Δ = [20 + (N + w·v'/100 − w·v/100) / D' × 80] − [20 + N/D × 80]
```

**Ejemplo real — habilitar MFA en EAI 10969** (valor 20 → 100, peso 7, D = 80):

```
Numerador: 59.61 → 59.61 + 7×(100−20)/100 = 65.21
Score:     79.61 → 20 + (65.21/80)×80 = 85.21
Ganancia:  +5.60 puntos
```

Regla general cuando la métrica **ya tiene dato**: `Δ = 0.8 × w × (v' − v) / D`. Con `D = 80` la constante es exactamente `w × (v'−v) / 100` — de ahí que MFA valga **+5.6 pts** y remediar los SCA Critical valga **+8.0 pts**.

### 4.2 Catálogo de palancas

| Palanca | Métrica | Salto típico | Ganancia (D=80) |
|---|---|---|---|
| Habilitar MFA | `mfa` | 20 → 100 | **+5.60** |
| Remediar SCA Critical | `sca.critical` | 20 → 100 | **+8.00** |
| Remediar SCA High | `sca.high` | 20 → 100 | **+6.40** |
| Incluir en plan DR | `dr.inScope` | 20 → 100 | **+5.60** |
| Subir OSS Score a ≥75 | `oss` | 20 → 100 | **+8.00** |
| Bajar Kenna a rango Low | `kenna` | 20 → 100 | **+6.40** |
| Onboarding a APIM/APIC | `api.native` | 20 → 100 | **+4.00** |
| Habilitar monitoreo (Elastic/APM/Synthetic) | `monitoring` | 20 → 100 | **+4.00** |
| Eliminar tech debt | `techDebt` | 20 → 100 | **+8.00** |
| Modernizar N productos obsoletos | `archValue` | recalcula compuesto | variable |
| Reclasificar productos a Invest | `productStrategy` | recalcula compuesto | variable |
| Subir Operational Score a ≥80 | `operational` | 20 → 100 | **+4.00** |

### 4.3 Capacidades del servicio

1. **Escenario simple** — una palanca, delta inmediato.
2. **Escenario compuesto** — N palancas combinadas, con el efecto de interacción correcto (el denominador cambia si alguna métrica pasa de sin-dato a con-dato; sumar deltas individuales sería incorrecto y el motor lo evita recalculando de cero).
3. **Ranking de eficiencia** — palancas ordenadas por puntos ganados, cruzadas con esfuerzo estimado → "quick wins" primero.
4. **Objetivo inverso** — "quiero llegar a 90": el motor devuelve el subconjunto mínimo de palancas que lo logra (búsqueda greedy + verificación exacta).
5. **Techo alcanzable** — score máximo con todas las palancas aplicadas; identifica lo que queda estructuralmente limitado.
6. **Escenarios guardados y comparables** — persistidos, versionados contra el ruleset, compartibles por link.

> Los escenarios **nunca** mutan datos reales. Se ejecutan sobre una copia del snapshot y los resultados se marcan explícitamente como proyección.

---

## 5. Roadmap y Disposition (requisito 6)

### 5.1 Modelo MAPS (del BRD)

| Disposition | Roadmap | Descripción | Score |
|---|---|---|---|
| **Tolerate** — optimizar preservando; sin cambios requeridos o modernización imposible en el año calendario | Maintain (Patch) | *Retain.* Inversión para mantenerlo vigente y seguro | 60 |
| | Rehost | Migración a otro hosting/compute (AKS, ASE) sin alterar arquitectura ni funcionalidad; sin recompilación | 60 |
| **Invest** — apps con encaje funcional al negocio, necesarias para objetivos esenciales | Maintain (Evolve) | Nueva funcionalidad/capacidades sobre la app actual | **100** |
| | Refactor | Cambios de código o arquitectura preservando funcionalidad; patrones modernos; incluye rearchitect y re-platform | 20 |
| | Reimagine | *Replace.* Nueva versión desde cero con frameworks modernos, o sustitución por COTS/SaaS | 20 |
| **Eliminate** — la app sale del portafolio, reduciendo el conteo | Retire | Baja de apps obsoletas, redundantes o sin valor de negocio | 20 |
| | Consolidate | La funcionalidad se consolida en otra aplicación | 20 |

### 5.2 Vista "Plan Futuro" por aplicación

- **Estado actual**: disposition, roadmap, score actual, desglose por pilar, frescura de cada fuente.
- **Estado objetivo**: score proyectado tras ejecutar el roadmap, calculado con el motor what-if (no un número escrito a mano).
- **Plan de ejecución**: hitos derivados del roadmap (ej. Rehost → evaluación AKS → contenedorización → cutover), fechas objetivo, owner (CIO / CRE / AD Director), dependencias entre aplicaciones.
- **Coherencia**: alertas cuando disposition y datos se contradicen — ej. app marcada **Retire** con conteo alto de productos Invest, o **Tolerate** con SCA Critical abiertos.
- **Vista portafolio**: matriz Disposition × Roadmap con conteo de apps y score promedio, drill-down a la lista.

---

## 6. Frontend — SPA React 19

### 6.1 Stack

| Componente | Elección |
|---|---|
| Componente | Planeado | Implementado (revisión del 2026-09-14) |
|---|---|---|
| Build | Vite 6 + TypeScript 5.7 (`strict`) | **Vite 7 + TypeScript 5.9**, `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` |
| Router | React Router 7 (data APIs) | React Router 7 con rutas declarativas; la URL lleva los filtros ([ADR 0016](docs/adr/0016-el-filtro-vive-en-la-url-y-solo-acota.md)) |
| Datos servidor | TanStack Query v5 | TanStack Query v5 |
| Estado UI | Zustand | **Sin librería**: estado local y URL. El borrador de reglas vive en `sessionStorage` |
| Formularios | React Hook Form + Zod | **Sin librería**: campos controlados; el servidor valida (una sola fuente de verdad) |
| Gráficos | Recharts + SVG propio | **SVG propio** ([ADR 0006](docs/adr/0006-spa-sin-libreria-de-graficos.md)) |
| Estilos | Tailwind CSS 4 + Radix UI | Tailwind CSS 4, componentes propios accesibles |
| Cliente API | Generado desde OpenAPI | **Tipos a mano con prueba de contrato**: `ViewModelContractTests` falla si un modelo de vista C# y su interfaz TypeScript difieren en cualquier sentido |
| Tests | Vitest + Testing Library + Playwright | **Vitest** (lógica pura, sin DOM) + **Playwright** (40 escenarios: 19 de flujo y 21 de accesibilidad con axe; Edge en local, Chromium en el CI) |
| Carga | — | Pantallas de administración cargadas bajo demanda (`lazy` + `Suspense`) |

> *Corrección del plan.* Se prometía usar **Actions + `useActionState`**, **`use()`** con Suspense y el **compilador** de React 19. No se usan: las mutaciones de TanStack Query cubren los estados pendientes y no hubo tablas con costo de render medible. Queda como decisión abierta TD-02 en [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md).

### 6.2 Mapa de pantallas

| # | Pantalla | Contenido | Req. |
|---|---|---|---|
| 1 | **Portfolio** | Grid de apps con filtros idénticos al dashboard (CIO, CRE/AD Director, Region, Country, EAI Code, Application Status, L1 Business Capability, Critical App, LOB/Sub-LOB). Distribución de scores, heatmap por LOB. | — |
| 2 | **Score Overview** | Gauge del score global + tres tarjetas de pilar replicando el layout v1.3 (valor crudo → ×peso → ponderado), badges de peso disponible vs total. | 4 |
| 3 | **Cómo se calculó** | **La pantalla clave.** Waterfall de contribución por métrica; tabla completa `raw → regla aplicada → score mapeado → peso → ponderado`; fórmula renderizada con los números reales sustituidos; métricas excluidas con su motivo; panel de procedencia (archivo, columna, fila, timestamp) por cada métrica. | 4 |
| 4 | **What-If Lab** | Panel de palancas con sliders/toggles; recálculo en vivo con delta por métrica; comparación lado a lado baseline vs escenario; ranking de quick wins; modo "objetivo": ingresar score meta y obtener el set mínimo de acciones; guardar y compartir escenarios. | 5 |
| 5 | **Roadmap & Plan Futuro** | Ficha MAPS de la app, timeline de hitos, score proyectado post-roadmap, dependencias, alertas de incoherencia. | 6 |
| 6 | **Portfolio Roadmap** | Matriz Disposition × Roadmap, capacidad por trimestre, agregados por LOB. | 6 |
| 7 | **Data Lineage** | Estado de cada fuente: última carga, filas, filas rechazadas con motivo, cobertura por métrica (% de apps con dato). Desde el 2026-09-28, con la pestaña **Mapping** (archivo y columna que cargan cada campo, decidido antes de cargar) y los motivos de rechazo agrupados en el resultado de cada carga ([ADR 0027](docs/adr/0027-el-mapping-de-columnas-se-decide-antes-de-cargar.md)). | 8, 9 |
| 8 | **Admin / Rulesets** | Comparación de versiones de ruleset, simulación de impacto en el portafolio antes de publicar, historial de overrides con justificación. | — |
| 9 | **Insights** | Las seis preguntas de gestión sobre el portafolio filtrado: salud, qué modernizar primero (impacto contra esfuerzo), dónde reducir complejidad, dónde se concentra el riesgo, qué causa la obsolescencia y dónde faltan defensas de continuidad. Cada número con su aritmética a la vista ([ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md)). | 4, 6 |
| 10 | **Code** | Obsolescencia y riesgo según lo que declaran los repositorios de cada aplicación en GitHub Enterprise: componentes contra su fecha de fin de vida (endoflife.date), bibliotecas obsoletas, desactualizadas o con alertas, discrepancias con el Currency Score de CMT e índice de riesgo de código por aplicación, con su detalle. Datos de demostración hasta la integración ([ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)). | — |

### 6.3 Seguridad del frontend

- **Patrón BFF**: cero tokens en el browser. El `channel-api` mantiene la sesión y emite cookie `httpOnly` + `Secure` + `SameSite=Strict`. Elimina de raíz el robo de tokens por XSS.
- **CSP estricta, sin nonce**: `default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'`. Sin `unsafe-inline`, sin `unsafe-eval` y sin orígenes de terceros. El build de producción no tiene un solo script ni estilo en línea, así que `'self'` es más estricto que un nonce — verificado en la Fase 08 con cero violaciones en navegador. Las fuentes se sirven desde el mismo origen.
- Sin `dangerouslySetInnerHTML` en ningún punto; regla de ESLint que lo prohíbe.
- `Permissions-Policy` restrictiva. Subresource Integrity no aplica: todos los recursos son del mismo origen.
- Doble-submit CSRF token en todas las mutaciones.

---

## 7. Seguridad (requisito 7)

### 7.1 Superficie de librerías — cero vulnerabilidades conocidas

| Control | Implementación |
|---|---|
| Inventario | **Central Package Management** (`Directory.Packages.props`) — una sola versión por paquete en toda la solución |
| Bloqueo | `packages.lock.json` commiteado + `--locked-mode` en CI: el build falla si alguien altera el árbol de dependencias |
| Escaneo .NET | `dotnet list package --vulnerable --include-transitive` → **falla el build ante cualquier severidad ≥ Moderate** |
| Escaneo npm | `npm audit --audit-level=moderate` + **OSV-Scanner** + `npm ci --ignore-scripts` (bloquea postinstall maliciosos) |
| SBOM | **CycloneDX** generado por build y archivado como artefacto |
| Actualizaciones | **Renovate** con auto-merge solo de parches y suite verde |
| Cadena de suministro | Firma de imágenes con **cosign**, verificación de atestación SLSA en admisión al clúster |
| Criterio de adopción | Ninguna dependencia sin release en 12 meses, sin mantenedor activo o sin política de seguridad publicada |

> Nota de coherencia: la plataforma **mide** postura de seguridad (SCA, OSS Score, Kenna). Su propio pipeline aplica los mismos umbrales — la herramienta debe aprobar su propio scoring.

### 7.2 Defensa en profundidad

**Identidad y acceso**
- OIDC contra el IdP corporativo (Entra ID / Keycloak); Authorization Code + PKCE; sin flujo implícito.
- Autorización basada en políticas por LOB/CIO — un CRE solo ve su portafolio. Filtrado a nivel de query, no en la UI.
- Escritura de overrides y publicación de rulesets exigen re-autenticación (step-up) y quedan en log de auditoría inmutable.

**Servicio a servicio**
- **mTLS obligatorio** vía service mesh (Linkerd) — sin tráfico en claro dentro del clúster.
- Tokens de servicio de vida corta; ningún servicio confía en headers del cliente.
- **NetworkPolicies default-deny**: cada servicio declara explícitamente con quién habla.

**Aplicación**
- Validación de entrada en whitelist (FluentValidation) en el borde y en cada servicio.
- Rate limiting por usuario y por IP en el `channel-api`.
- Salida sin datos innecesarios: DTOs explícitos, nunca entidades de dominio.
- Sin SQL dinámico; parámetros siempre. EF Core y Dapper parametrizados.
- Cabeceras: HSTS con preload, `X-Content-Type-Options`, `Referrer-Policy: no-referrer`, `Cross-Origin-Opener-Policy`.

**Runtime**
- Imágenes **distroless / chiseled**, non-root, filesystem read-only, todas las capabilities dropeadas.
- Secretos en Azure Key Vault / HashiCorp Vault vía CSI driver — nunca en variables de entorno ni en configuración.
- Escaneo de imágenes (Trivy) bloqueante en el pipeline.

**Datos**
- Cifrado en tránsito (TLS 1.3) y en reposo.
- El inventario de aplicaciones es información sensible: acceso mínimo necesario, logs de acceso, retención definida.
- Sin PII en logs; redacción estructurada.

**Pipeline**
- SAST (CodeQL) + escaneo de secretos (Gitleaks) + IaC scanning (Checkov) + DAST (OWASP ZAP en staging).
- Rama protegida, revisión obligatoria, commits firmados.
- Objetivo de conformidad: **OWASP ASVS Nivel 2**, con verificación explícita del Top 10.

---

## 7bis. Manejo de data histórica

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
| Primer arranque | Sin ninguna carga las pantallas responden **409 con la salida** —ir a Cargas— y la carga sigue habilitada. El adaptador activo lo declara la capa System (`/system/ingestion/capabilities`); deducirlo de la primera versión bloqueaba la carga justo cuando no hay ninguna |

### 7bis.2 Fase de base de datos (entregada en la Fase 09)

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
| Score precalculado | **Descartado.** La Fase 08 dejó la historia en 0,006 s cacheando por versión; una copia precalculada añadiría coherencia que mantener a cambio de milisegundos |

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

## 8. Plan de entrega por fases

| Fase | Duración | Alcance | Criterio de aceptación |
|---|---|---|---|
| **0 — Fundaciones** ✅ | ENTREGADA | Monorepo, Directory.Packages.props, CI con gates de seguridad, contenedores base, OTel | Pipeline verde con escaneos bloqueantes activos |
| **1 — Motor de scoring** ✅ | ENTREGADA | `Scoring.Engine`, ruleset v1.3, breakdown explicable, tabla completa de mapeos | **Golden tests: EAI 10969 = 79.61 y EAI 14381 = 79.67, exactos** |
| **2 — Datos dummy** ✅ | ENTREGADA | Generador Bogus, ~250 apps, export CSV con nombres de columna reales, `ingestion-service` con adaptador Dummy | Contract tests verdes; casos borde cubiertos |
| **3 — Capas Channel y System** ✅ | ENTREGADA | `system-api` + `channel-api`, OIDC, OpenAPI | E2E autenticado devuelve breakdown completo |
| **4 — SPA base** ✅ | ENTREGADA | React 19, Portfolio, Score Overview, **Cómo se calculó** | Pantalla 3 reproduce el layout del dashboard v1.3 |
| **5 — What-If** ✅ | ENTREGADA | `whatif-service`, catálogo de palancas, escenarios compuestos, objetivo inverso, What-If Lab | Δ MFA = +5.60 pts verificado contra cálculo manual |
| **6 — Roadmap** ✅ | ENTREGADA | `roadmap-service`, modelo MAPS, plan futuro, score proyectado, matriz de portafolio | Las 7 combinaciones disposition/roadmap modeladas |
| **7 — Adaptador de archivos** ✅ | ENTREGADA | `FileSourceReader` CSV/XLSX, contratos de esquema, rechazos con motivo, Data Lineage | Mismo score leyendo dummy vs sus CSV exportados |
| **8 — Endurecimiento** ✅ | ENTREGADA | Pentest, ASVS L2, carga, DR, runbooks | **Cumplido:** 23 hallazgos, las 3 Altas cerradas, ninguna High/Critical abierta |
| **9 — Adaptador BD** ✅ | ENTREGADA | `DatabaseSnapshotProvider`, EF Core 10, migraciones, publicación transaccional, feature flag | **Cumplido:** contract tests verdes contra base real y el golden 79,67 leído desde ella |
| **10 — Experiencia de consulta** ✅ | ENTREGADA | Filtros globales en la URL, estadísticas clicables, roadmap por dimensión, pantalla Datos con linaje por carga, archivos reconocidos por su encabezado | **Cumplido:** clic en «Mexico» filtra, el filtro sobrevive a la navegación y el enlace se comparte; verificado en la interfaz real |
| **11 — Sitio en inglés** ✅ | ENTREGADA | Todo el texto de interfaz en inglés; el backend devuelve códigos y datos y la interfaz redacta la frase | **Cumplido:** ninguna respuesta de la capa Channel trae texto en español —lo comprueba una prueba— y la interfaz pasó la revisión en vivo |
| **12 — Administración de reglas** ✅ | ENTREGADA | Ruleset, palancas de what-if y parámetros de roadmap editables desde el portal, solo administradores, con borrador, simulación de impacto y publicación auditada | **Cumplido:** cambiar un peso desde el portal mueve el portafolio, queda auditado con su impacto y lo toman las réplicas sin desplegar |
| **13 — Revisión y remediación** ✅ | ENTREGADA | Reglas contra las capturas del BRD, deuda técnica, rendimiento, robustez, comparación de reglas, exportaciones, pruebas de la SPA, git y paquete portable (ver 8.6) | **Cumplido:** 640 pruebas .NET, 35 unitarias y 6 de punta a punta en verde; la historia en frío pasa de 1,7 s a 8–15 ms con calentamiento y la simulación de 117 a 47 ms |
| **14 — Verificación de seguridad (ASVS L2)** 🟡 | EN CURSO | Lo que no necesita infraestructura, hecho el 2026-09-15 (ver 8.7): pipeline verde, auditoría de validaciones rechazadas, retención de cargas, decisión sobre validación de esquema, controles compensatorios de aislamiento, y accesibilidad y e2e en el CI. Falta el entorno de prueba para los once parciales restantes y la revisión por otra persona | **Parcial:** ASVS pasa de 103 a 106 cumple y de 14 a 11 parciales; los que quedan dependen de cluster, IdP, ingress y gestor de secretos |
| **15 — Autenticación con APIM** 🟡 | EN CURSO | Opción C con A elegida el 2026-09-16 ([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)): credencial conectable, validación del JWT en la capa System, modo `Both` para cambiar sin corte, reautenticación de APIM y alcance desde grupos (ver 8.8) | **Parcial:** el código está probado con dobles; cerrar H-16 espera a que APIM emita los tokens y el secreto salga del gestor |
| **Pantalla 09 — las seis preguntas** ✅ | ENTREGADA | Pedida el 2026-09-16, fuera de la numeración de fases: salud, prioridades, complejidad, riesgo, obsolescencia y resiliencia en una vista con los mismos filtros (ver 8.9) | **Cumplido:** las seis se responden con las fuentes actuales; 14 pruebas del analizador, 14 de API, 4 de Vitest y 4 de punta a punta, más accesibilidad en claro y oscuro |
| **Dashboard ejecutivo — D1 y D2** ✅ | ENTREGADA | Pedido el 2026-09-18 a partir de la propuesta EA Data & Insights: *Portfolio Health & Risk Intelligence*, hoy la página de inicio (`/`), con cinco indicadores, seis paneles, un detalle común y cinco filtros nuevos (ver 8.10) | **Cumplido:** 28 pruebas del analizador, 13 más en la suite de API, 8 de Vitest y 6 de punta a punta; axe sin violaciones serias en claro y oscuro. Las definiciones abiertas quedan como defaults provisionales (ADR 0024, DD-01 a DD-07) |
| **Dashboard ejecutivo — D3** ✅ | ENTREGADA | Diccionario de indicadores (*Definitions*), explicación de cada cálculo en el dashboard y en Insights, y las 37 definiciones editables desde el portal con las reglas versionadas ([ADR 0025](docs/adr/0025-las-definiciones-viajan-con-las-reglas.md)) | **Cumplido:** ningún indicador sin sus seis campos; una prueba de API publica un umbral desde el portal y el dashboard se mueve sin desplegar |
| Dashboard ejecutivo — D4 | PLANIFICADA | Fuentes nuevas: TLM, resiliencia, deudas técnicas, application class, dependencias | Cada fuente encendida convierte un bloque «sin dato» en una medición |
| **Pantalla Code — C1** ✅ | ENTREGADA | Pedida el 2026-09-26: obsolescencia y riesgo por librerías, software y componentes de cada repositorio, con endoflife.date como fuente del fin de vida y datos de demostración hasta integrar GitHub Enterprise (ver 8.11) | **Cumplido:** la pantalla responde con los mismos filtros y alcance, cada número se explica con las definiciones vigentes (área `code`, 15 parámetros editables) y el proveedor de demostración no arranca fuera de Development |
| Pantalla Code — C2 · GitHub Enterprise | PLANIFICADA | El recolector `code-collector` con una GitHub App de solo lectura, el mapeo repositorio → EAI y la publicación del inventario a la capa System (ver 8ter, GH-01 a GH-09) | La pantalla muestra repositorios reales de una organización piloto, sin ningún dato de demostración y sin que la System ni la Channel tengan credenciales de GitHub |
| **16 — Escala y operación** | 3 sem | Destino y motor de base (RFC-07), Redis, réplicas, respaldos, retención, SIEM, notificaciones de reglas | Dos réplicas por capa bajo carga; restauración probada; alertas activas |
| **17 — Hallazgos de infraestructura** | 2 sem | **Al final por decisión del 2026-09-14:** H-16, H-17, H-20, H-21 | ASVS 2.10.1, 3.3.4, 7.3.3, 12.4.2 y 14.1.5 cumplen |

Total: **~16 semanas** hasta la Fase 9, más **~8 semanas** de la ampliación (fases 10 a 12) y **~1 semana** de la revisión (Fase 13). Las fases 14 a 17 suman **~11 semanas** estimadas y dependen de decisiones del equipo listadas en [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md). Las fases 7 y 9 son deliberadamente cortas — el trabajo de aislamiento se hace en la fase 2, no cuando llegan los archivos reales.

### 8.1 Resultado de la Fase 08 — Endurecimiento

**Criterio cumplido: ninguna High ni Critical abierta.** La revisión encontró 23
hallazgos; las tres Altas se cerraron con pruebas que las reproducen. Detalle en
[docs/security/hallazgos.md](docs/security/hallazgos.md).

| Severidad | Encontrados | Cerrados | Abiertos |
|---|---:|---:|---|
| Alta | 3 | 3 | — |
| Media | 10 | 8 | H-16 parcial (token de servicio estático) · H-17 (firma de imágenes y logs inmutables: infraestructura) |
| Baja | 8 | 6 | H-20 (ver y cerrar sesiones activas) · H-21 (antivirus en cargas) |
| Informativa | 2 | 2 | — |

Las tres Altas:

1. **Cualquier sesión podía publicar una carga.** Ahora exige el rol `ingestion.write` y alcance completo, en las dos capas.
2. **La autorización por LOB/CIO de la sección 7.2 no existía.** Todos veían todo, incluidas las vulnerabilidades por aplicación. Ahora el alcance se aplica en la capa System, y ninguna lectura del portafolio compila sin él.
3. **El rate limiter corría antes de autenticar.** Todos compartían el balde de la IP del ingress: uno solo agotaba el servicio.

| Entregable | Dónde |
|---|---|
| Modelo de amenazas STRIDE por frontera | [docs/security/modelo-de-amenazas.md](docs/security/modelo-de-amenazas.md) |
| ASVS L2 al cierre de la Fase 08: 100 cumple · 15 parciales · 3 pendientes. **Vigente desde la Fase 14: 106 cumple · 11 parciales · 3 pendientes · 10 IdP o N/A** | [docs/security/asvs-l2.md](docs/security/asvs-l2.md) |
| Runbooks: secretos, carga defectuosa, DR, incidente | [docs/runbooks/](docs/runbooks) |
| Pipeline con gates de seguridad | `.github/workflows/ci.yml` (build, pruebas, CodeQL, Trivy, Checkov, SBOM) y `seguridad.yml` (Gitleaks y OSV en cada push) — acciones fijadas por SHA |
| Contenedores y cluster | `deploy/docker` (distroless de Azure Linux, no root) · `deploy/k8s` (PSA restricted, NetworkPolicy default-deny, mTLS) |
| Prueba de carga | `tools/Modernization.LoadTest` |
| Decisiones | ADR 0011 (sesión en el servidor) · 0012 (alcance sobre los datos) · 0013 (autenticado no significa inofensivo) |

**Medido antes y después**, con 30 versiones en la historia:

| | Antes | Después |
|---|---:|---:|
| Historia del portafolio | 1,0 s | 0,006 s |
| Historia de una aplicación | 0,8 s | 0,005 s |
| Pantalla Cargas, 16 usuarios concurrentes | — | 4 241 req/s · p95 5,5 ms |
| JSON de 2 MB contra cualquier endpoint | aceptado | 413 |
| Carga de 40 MB | cortada a 30 MB | aceptada en 3,3 s |

**Desviación del plan, a propósito:** la CSP no usa nonce (sección 6.3).

**No se ejecutó en local:** DAST con OWASP ZAP, Trivy y la construcción de las
imágenes, porque requieren descargar imágenes de contenedor. Quedan en CI.

---

### 8.2 Resultado de la Fase 09 — Adaptador de base de datos

**Criterio cumplido:** la misma suite de contrato que verificó Dummy y File corre
contra una base real, y el EAI de referencia sigue dando **79,67** leído desde ella.

| Evidencia | Resultado |
|---|---|
| Suite de contrato del tercer adaptador | 127 pruebas en el proyecto de contratos (eran 98) |
| Total | **466 pruebas en verde**, cero paquetes vulnerables con EF Core y SQLite dentro |
| Extremo a extremo por HTTP | Subir los ocho CSV por la pantalla publica una versión en la base, y el portafolio responde con ella |
| Golden desde la base | `20 + (44.75 / 60) x 80 = 79.67`, idéntico a archivos |

**Qué se construyó**, más allá de leer de una base:

- **Un puerto de publicación** (`ISnapshotPublisher`), gemelo del de lectura. La
  capa System ya no conoce ni al adaptador de archivos ni al de base: publicar es
  mover un directorio o abrir una transacción, y desde arriba no se distingue.
- **El esquema tiene la forma de las fuentes**, no la del dominio, y la carga se ata
  con una propiedad sombra ([ADR 0014](docs/adr/0014-la-base-guarda-la-forma-de-las-fuentes.md)).
- **Los rechazos se guardan** en su tabla: con archivos se recalculan al leer, pero
  aquí el archivo ya no existe, y sin ellos el linaje se apagaría en silencio.
- **Retención declarativa** (`Database:RetainVersions`): conserva las N cargas más
  recientes. Con archivos había que borrar directorios a mano.
- **Migraciones de EF Core**, aplicadas al arrancar en un solo nodo y por un job de
  despliegue cuando haya varias réplicas.

**Dos desviaciones del plan, ambas deliberadas:**

1. **Sin tabla `score_snapshot`.** El plan la contemplaba para abaratar la historia.
   La Fase 08 midió el costo real y el cálculo cacheado por versión dejó la consulta
   en 0,006 s: precalcular añadiría una copia que mantener coherente con el ruleset
   vigente a cambio de milisegundos.
2. **El motor concreto sigue siendo RFC-07.** El modelo es agnóstico y se verifica
   contra SQLite, que es una base real —archivo, transacciones, índices,
   migraciones—. Añadir SQL Server o PostgreSQL cuando se confirme el destino es un
   paquete y un juego de migraciones; arrancar contra un motor no verificado falla
   explícitamente en lugar de suponer.

**EF Core no llega a la capa Channel:** el adaptador vive en su propio proyecto y la
referencia desde la capa System es privada. El generador de datos dummy y el lector
de archivos tampoco lo cargan.

### 8.3 Resultado de la Fase 10 — Experiencia de consulta

**Criterio cumplido, verificado en la interfaz real:** hacer clic en «Mexico» deja
`/?country=Mexico` con sus 80 aplicaciones; el filtro sigue puesto al pasar por
Roadmap y Datos y al volver al portafolio; y `/roadmap?country=Mexico&groupBy=cio`
abierto en una pestaña nueva —sin memoria de sesión— reproduce la misma vista.

| Evidencia | Resultado |
|---|---|
| Pruebas | **520 en verde** (eran 466): 159 motor · 214 API · 147 contratos. Cero paquetes vulnerables en 14 proyectos y `npm audit` limpio |
| Cada estadística es un filtro | Una prueba recorre todas las claves de cada agregado del portafolio y de cada agrupación del roadmap, y exige que como filtro devuelvan exactamente su conteo |
| En vivo, capa System (250 aplicaciones) | 22 de 22 verificaciones: los 22 valores de las cinco dimensiones filtran su conteo exacto, el roadmap agrupado responde en 40–160 ms, y 8 exports con nombres cualquiera se publican en 0,2 s con los nombres canónicos en disco |
| Alcance | Un usuario limitado a un LOB que filtra por otro ve cero aplicaciones y cero grupos |

**Qué se construyó:**

- **Filtros globales con la URL como fuente de verdad** ([ADR 0016](docs/adr/0016-el-filtro-vive-en-la-url-y-solo-acota.md)):
  siete criterios compartidos —LOB, país, CIO, disposition, roadmap, tramo y
  criticidad—. La barra vive en el armazón; donde el filtro no actúa (detalle de
  una aplicación, Datos) se muestra el filtro activo con el enlace de vuelta.
- **Estadísticas clicables:** tramos, barras por LOB, país, CIO, disposition y
  roadmap, y las celdas de la tabla. Cada una es un botón con nombre legible para
  lectores de pantalla; activarla de nuevo quita el filtro.
- **Roadmap por dimensión:** `groupBy=portfolio|application|country|cio|lob|disposition|roadmap`,
  con la mezcla de roadmaps de cada grupo. Agrupar no recalcula: una prueba compara
  el grupo por aplicación con el plan individual.
- **Pantalla Datos:** Cargas y Linaje con un selector de carga compartido. El linaje
  pasa a ser por carga y su total sale de la carga, no de la fuente mejor cubierta.
- **Archivos reconocidos por su encabezado** ([ADR 0015](docs/adr/0015-el-archivo-se-reconoce-por-su-encabezado.md)):
  si un encabezado encaja en dos fuentes, la pantalla muestra lo leído y deja
  asignar la fuente a mano. El archivo aterriza con un nombre generado.
- **Los tramos pasan al dominio**, porque filtrar por tramo exige puntuar.

**Cuatro defectos encontrados en el camino, todos cerrados:**

1. Las barras de roadmap emitían «Maintain (Patch)» y el filtro esperaba
   «MaintainPatch»: el clic devolvía cero. Lo encontró la prueba que recorre las claves.
2. Pasar por Datos y volver dejaba el portafolio sin filtro: el enlace no lo llevaba
   y la memoria solo actuaba al cargar la página. Lo encontró la verificación en
   vivo; ahora un enlace que llega sin filtro lo recupera, quitarlo desde la barra
   borra la memoria y el botón atrás lo quita.
3. Una carga inexistente en el linaje habría respondido 500 en lugar de 404.
4. Al volverse botones, las filas de agregado se leían «12048,0%» en un lector de
   pantalla; ahora cada una tiene su nombre accesible.

### 8.4 Resultado de la Fase 11 — Sitio en inglés

**Criterio cumplido.** Ninguna respuesta de la capa Channel trae texto en español, y
eso no depende de una revisión: lo comprueba una prueba. La interfaz pasó además la
revisión en vivo, pantalla por pantalla.

| Evidencia | Resultado |
|---|---|
| Pruebas | **524 en verde** (eran 520): 159 motor · 218 API · 147 contratos. Cero paquetes vulnerables en 14 proyectos y `npm audit` limpio |
| Barrido de idioma | Recorre sesión, portafolio, las siete agrupaciones del roadmap, 40 aplicaciones en sus cinco pantallas, escenarios, los tres casos de score objetivo, los errores de validación, una petición sin CSRF y cargas aceptadas y rechazadas. Cero textos en español; la etiqueta que escribe quien carga queda fuera, porque es dato suyo |
| Catálogo | Cada código de `NoticeCodes` y cada tramo tienen frase en `notices.ts`; un código sin frase hace fallar una prueba |
| En vivo | Portafolio, roadmap por dimensión, detalle, cálculo, what-if, plan, cargas y linaje en inglés, con números y fechas en formato inglés («Dec 18, 2026»). Un 404 responde `urn:modscore:problem:application-not-found` |
| Enlaces anteriores | `/aplicaciones/14381/calculo` abre `/applications/14381/calculation`, y `/datos?tab=linaje` abre `/data?tab=lineage` |

**Qué se construyó:**

- **El backend dejó de redactar frases** ([ADR 0017](docs/adr/0017-el-backend-no-redacta-frases.md)):
  el valor crudo de una métrica, el motivo de exclusión, la evidencia de una alerta, el
  pronóstico del plan, los pasos de la fórmula, el veredicto de una carga y el rechazo
  de una fila viajan como `{ code, args }`, y la SPA arma la frase.
- **Errores de API con `type` estable** (`urn:modscore:problem:<código>`) y `title` y
  `detail` en inglés. El detalle interno de un error de ruleset ya no llega al usuario.
- **El contenido de los contratos se tradujo en el propio contrato**: definiciones MAPS,
  hitos, reglas de coherencia y notas del ruleset. Los alias de columna no se tocaron,
  porque son los encabezados que exporta Power BI.
- **Tramos como códigos** (`critical`, `at-risk`, `acceptable`, `solid`). Los nombres
  anteriores se siguen aceptando como filtro.
- **Rutas en inglés**, con redirección desde las anteriores.
- **Los rechazos guardados en la base llevan el aviso serializado**, sin migración. Los
  anteriores a esta fase se leen como «fila no convertible».

**Dos defectos encontrados en la revisión en vivo, ambos cerrados:**

1. Un enlace de la Fase 10 con `?band=Critico` filtraba bien, pero la barra mostraba el
   chip «Critico». Ahora la URL pasa al código y el chip dice «Critical».
2. Conteos sin concordancia, como «1 applications with a plan». Los conteos de las
   pantallas y las frases del catálogo concuerdan ahora en singular y plural.

**Sigue en español, a propósito:** logs, comentarios del código, resúmenes OpenAPI y
errores de arranque y de validación del ruleset, porque los lee quien opera. También la
documentación del proyecto.

### 8.5 Resultado de la Fase 12 — Administración de reglas

**Criterio cumplido, verificado en vivo y por una prueba de punta a punta.**

En vivo, sobre las 250 aplicaciones de la historia local:

- **Cambio.** Desde la pantalla Rules se pasaron 3 puntos de peso de Kenna a MFA.
- **Simulación.** Mostró el efecto antes de publicar: el promedio pasaba de 70,52 a 70,68,
  con 101 aplicaciones que suben y 65 que bajan.
- **Publicación.** v1.4 quedó vigente en el mismo proceso y el portafolio pasó a 70,68,
  sin recompilar ni reiniciar.
- **Auditoría.** Registró
  `Reglas publicadas. usuario=dev-user version=v1.4 base=v1.3 cambios=2 suben=101 bajan=65 deltaPromedio=0.16`.
- **Restauración.** Cargar v1.3 desde la pestaña de versiones y publicarla creó v1.5 con
  el contenido de fábrica, y el portafolio volvió a 70,52.

| Evidencia | Resultado |
|---|---|
| Pruebas | **598 en verde** (eran 524): 190 motor · 235 API · 173 contratos. Cero paquetes vulnerables en 15 proyectos y `npm audit` limpio |
| Criterio de aceptación | `PublishingAWeightMovesThePortfolioIsAuditedAndNeedsNoRedeploy` publica desde la capa Channel. Comprueba que el promedio del portafolio cambia, que la 14381 puntúa con v1.4 y con el mismo motor que simuló, y que queda el evento 4211 |
| Sin redesplegar, con réplicas | `AnotherReplicaTakesThePublicationWithoutRestarting`: dos hosts sobre el mismo almacén, y lo que publica uno lo lee el otro |
| Cálculo a mano | EAI 10969 con Kenna en 5 y MFA en 10: numerador 62,009 sobre 80, score **82,01** |
| Almacenes | La misma suite de contrato corre sobre memoria, archivos y SQLite |
| Seguridad | Rol propio, reautenticación, permisos que la System vuelve a exigir, CSRF, límites y JSON estricto y acotado, con 17 pruebas de API. ASVS 3.7.1 y 4.3.3 pasan a cumple |

**Qué se construyó:**

- **Reglas como versiones publicadas** ([ADR 0018](docs/adr/0018-las-reglas-se-publican-como-versiones.md)).
  Ruleset, palancas y roadmap se publican juntos. Las de fábrica son v1.3, cada
  publicación crea una versión nueva y volver atrás es publicar la anterior.
- **Se editan valores, no estructura:**
  - pesos: el total tiene que dar 100 y el de cada pilar es la suma de sus métricas;
  - bandas, y puntajes de casos y de categorías;
  - qué palancas se ofrecen y su cantidad por defecto;
  - los umbrales del roadmap, las palancas de cada roadmap y las semanas de cada hito.
- **Borrador, simulación y publicación.** El impacto lo calcula el servidor sobre todo el
  portafolio: cuántas suben y cuántas bajan, el promedio, los tramos, las diez que más se
  mueven, la proyección de los planes y las alertas.
- **Almacén con el mismo patrón que las fuentes:** memoria, archivos en `.rules` y la
  tabla `rules_version` con su migración.
- **Pantalla Rules en la SPA**, solo para `ruleset.admin` con alcance completo. La
  reautenticación está integrada: el borrador sobrevive a la ida y vuelta al IdP.
- **Los objetivos de las palancas salen de las bandas.** Eran constantes que podían
  quedar desalineadas con el ruleset.

**Dos decisiones que corrigen o precisan el plan:**

1. **La historia se puntúa con las reglas vigentes.** La sección 8bis.3 decía que cada
   carga conservaría su versión de reglas, pero eso mezclaría en una misma tendencia un
   cambio de datos con uno de reglas. Lo que no se reescribe es el registro de cada versión.
2. **La normalización 20–100 no se edita.** Los tramos, el score objetivo y los umbrales
   usan esa escala: cambiarla es una decisión de arquitectura, no un ajuste.

**Qué no se pudo verificar en local:** la reautenticación contra un IdP real
(`max_age=0` y `auth_time`). En local corre el esquema de desarrollo, que la simula; la
lectura del token está aislada en `StepUp`.

### 8.6 Resultado de la Fase 13 — Revisión y remediación

Trece puntos pedidos el 14 de septiembre de 2026, tras revisar toda la sesión.

**Criterio cumplido.** Todo lo pedido quedó implementado, probado y documentado. Los
pendientes que requieren al equipo están en [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md),
en inglés y con el máximo detalle.

| Evidencia | Resultado |
|---|---|
| Pruebas .NET | **640 en verde** (eran 598): 200 motor · 175 contratos · 265 API |
| Pruebas de la SPA | **35 unitarias** (Vitest) y **6 de punta a punta** (Playwright con Microsoft Edge, sobre las tres piezas reales) |
| Contrato C# ↔ TypeScript | `ViewModelContractTests`: cada modelo de vista y su interfaz coinciden en los dos sentidos |
| Seguridad de dependencias | Cero paquetes vulnerables y `npm audit` limpio, con Vitest y Playwright dentro |
| Historia del portafolio en frío, 30 cargas | 1.655 ms → 584 ms en paralelo → **8–15 ms** con calentamiento en segundo plano |
| Simulación de reglas con 8 clientes, medida en reposo | p50 117 ms → **47 ms** (p95 66 ms); de 66 a 168 peticiones por segundo |
| Bundle principal de la SPA | 429 kB → **406 kB**: las pantallas de administración se cargan bajo demanda |

**Reglas de cálculo, primero** (las capturas del BRD mandan):

- *"Null or zero values are excluded from scoring"* ([ADR 0019](docs/adr/0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md)).
  El cero se excluye en Kenna, OSS, RAW Tech y Operational. En Tech Debt y SCA la tabla del
  BRD lo puntúa, así que el cero sigue puntuando. Es la marca `zeroIsNoData` del ruleset,
  editable sin desplegar.
- Notas del ruleset corregidas contra las capturas: RFC-03 no está verificado
  numéricamente, y se agregan RFC-13 a RFC-17.
- Se corrigen el ADR 0003 (el cero), el ADR 0017 (qué edita el portal) y el ADR 0018 (la
  historia no guarda versión por punto).
- El artefacto del plan decía 17 métricas; son **18**.

**Deuda técnica, rendimiento y robustez:**

- **Listar versiones de reglas** ya no reconstruye 50 motores: una versión publicada no
  cambia, así que se valida una vez por proceso.
- **La historia en frío** se calcula por carga en paralelo, y **un servicio en segundo
  plano** la deja caliente al arrancar, tras cada carga o publicación y periódicamente.
- **La simulación** calcula el lado vigente una vez por carga y versión, y el borrador en
  paralelo.
- **Almacén de reglas caído:**
  - se sigue puntuando con las vigentes (evento 5303);
  - leer una versión anterior responde 503;
  - publicar se rechaza con `rules.storeUnavailable`.
- **Staging huérfano** del almacén de reglas por archivos: se limpia en la siguiente
  publicación.
- **Reautenticación independiente del proveedor** (`IReauthenticationFlow`). Con OIDC cierra
  la sesión anterior antes de ir al IdP; antes quedaba huérfana hasta vencer. Camino a OAuth
  con APIM en el [ADR 0020](docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md).
- **Prueba de carga** con escenarios de reglas y latencia de la primera petición.
- **Lógica pura del editor de reglas** extraída a `src/web/src/rules/draft.ts`, con pruebas.

**Sugerencias de producto:**

- **Cambios de reglas en la tendencia (4).** La tarjeta de la tendencia marca con una línea
  punteada cuándo se publicó cada versión. `GET /api/rules/timeline` lo ve todo el que
  consulta, sin decir quién publicó.
- **Comparar reglas (5).** Pantalla nueva `/admin/rules/compare`. Compara dos versiones, o el
  borrador de la pestaña contra la vigente, sobre todo el portafolio y sobre una aplicación
  métrica por métrica.
- **Contenido descargable según los filtros (7):**
  - CSV del portafolio filtrado, del roadmap agrupado y de la comparación de dos versiones;
  - protección contra inyección de fórmulas, límite propio de 10 por minuto, tope de filas y
    evento de auditoría 4213;
  - aviso dentro del sitio cuando se publican reglas nuevas.

**Respaldo y distribución:**

- **Repositorio git** privado en GitHub (`main`, primer push el 2026-09-15). Guarda todo lo
  creado —el estado local de `data/`, los scripts de las sesiones y la copia del plan— salvo
  las capturas del BRD y las imágenes.
- **Scripts de respaldo** (`tools/git/`): corren las verificaciones y se niegan a subir
  capturas, imágenes, llaves, `.env` o archivos de más de 100 MB. Runbook en
  [docs/runbooks/respaldo-en-git.md](docs/runbooks/respaldo-en-git.md).
- **Paquete portable** (`tools/portable/`): ejecutables de un solo archivo con la SPA
  dentro, solo en `127.0.0.1`. **Retirado el 2026-09-27** por decisión: la aplicación se
  levanta desde el código con comandos para cmd y PowerShell ([guía](docs/guia-localhost.md)).

**Qué no se pudo verificar en local:**

- **ZAP contra staging** (SEC-20). El resto del pipeline quedó en verde el 2026-09-15 en la
  corrida #5, que pasó los 7 jobs en unos 10 minutos. Antes hubo que corregir el lint de la
  SPA, CodeQL en un repositorio privado (`upload: never` y `actions: read`), la versión de
  Trivy (v0.74.0) y el UID de los pods (10001).
- **Reautenticación contra un IdP real.**
- **Rendimiento en hardware de destino:** la misma simulación dio p50 204 ms con la máquina
  ocupada y 47 ms en reposo. Fijada a dos núcleos (2026-09-16), ocho simulaciones a la vez
  dan p95 329 ms, bajo el criterio de 500 ms, sin tocar código (TD-06). Falta repetirlo en el
  pod real.

**Tres decisiones que corrigen o precisan el plan:**

1. **El cero no siempre es un valor.** La regla general del BRD se aplica donde las tablas no
   la contradicen.
2. **Los tipos de la SPA no se generan desde OpenAPI.** Una prueba de contrato da la misma
   garantía sin sumar una herramienta a la cadena de suministro.
3. **Los hallazgos H-16, H-17, H-20 y H-21 pasan al final del plan**, por decisión del
   2026-09-14. Los parciales de ASVS se resuelven antes, en la Fase 14.

### 8.7 Resultado de la Fase 14 — Verificación de seguridad (ASVS L2)

En curso desde el 2026-09-15. Se hizo todo lo que no depende de un despliegue real.

**Cerrado en código, con pruebas:**

- **Validaciones rechazadas auditadas** (ASVS 7.1.3, SEC-06). Un middleware de la capa
  Channel registra el evento **4214** en toda respuesta de `/api` que rechaza la entrada
  (400, 413, 415 o 422), con la ruta y el código y **sin** los valores enviados. Va en el
  pipeline y no en cada endpoint para que ninguno se olvide.
- **Retención de cargas en archivos** (ASVS 8.3.4, SEC-07). `Uploads:RetainVersions`
  conserva las N cargas más recientes, igual que `Database:RetainVersions`; 0 conserva
  todas. Solo borra directorios con manifiesto, así que el almacén de reglas y el staging
  no se tocan. Cuántas conservar lo decide el negocio (OP-03).
- **Validación de esquema decidida** (ASVS 13.2.2, SEC-12). Se queda el contrato tipado
  estricto —`BoundedJson` más las invariantes de `Ruleset.Validate`— en lugar de un
  validador de esquema JSON, que sumaría una dependencia y una segunda fuente de verdad
  ([ADR 0021](docs/adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md)).
- **Aislamiento de terceros** (ASVS 14.2.6, SEC-14). Aceptado como límite de diseño con
  controles compensatorios documentados: árbol mínimo y bloqueado, escaneos en cada push,
  imágenes chiseled sin shell y con solo lectura, NetworkPolicy y CSP estricta.

**Accesibilidad y pruebas (TD-04, TD-05):**

- axe recorre las pantallas de uso diario y una de administración, en claro y en oscuro
  dentro del CI, y falla con violaciones
  serias o críticas. Encontró dos problemas reales, ya corregidos:
  - el texto atenuado quedaba en 4,42:1 sobre blanco y 3,89:1 sobre el fondo de página;
  - los colores de estado, pensados como marcas vivas, se usaban también como letra, con
    contrastes de hasta 1,65:1. Ahora hay variantes de texto que cumplen AA sin cambiar las
    marcas, y tres barras decorativas declaran `role="img"` en lugar de llevar `aria-label`
    en un `div` sin rol.
- El job **SPA de punta a punta** corre la suite completa en el CI con Chromium.

**Lo que queda y por qué:** los once parciales restantes necesitan un entorno real —llaves
protegidas, mTLS, gestor de secretos, TLS del ingress, cifrado interno, MFA exigida por el
IdP, reglas del SIEM y firma de imágenes—. El runbook
[verificación ASVS](docs/runbooks/verificacion-asvs.md) deja cada uno con su comando y su
resultado esperado. La revisión de seguridad por una segunda persona (TD-13) sigue abierta.

### 8.8 Resultado de la Fase 15 — Autenticación con APIM

En curso desde el 2026-09-16. Se eligió la **opción C junto con A** del
[ADR 0020](docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md) y se dejó el código listo,
probado con dobles: APIM todavía no existe.

**La credencial entre capas ahora es conectable** ([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)):

| Pieza | Hoy | Con APIM |
|---|---|---|
| Qué manda la capa Channel | El secreto del gestor de secretos | Token de vida corta con `client_credentials`, cacheado y renovado un minuto antes de vencer |
| Qué valida la capa System | Comparación en tiempo constante, con token de rotación | Firma, emisor, audiencia, vencimiento, algoritmo de una lista blanca y cliente permitido |
| Cómo se elige | `ServiceToken:Mode` y `SystemApi:Credential:Mode` | Igual, cambiando el valor |

**Tres decisiones dentro de esa:**

1. **Modo `Both` para la ventana del cambio.** La capa System acepta el secreto compartido
   y el token de APIM a la vez, así que las dos capas no tienen que cambiar en el mismo
   instante. Sin eso, el cambio pediría un corte, y un corte invita a hacerlo mal.
2. **La credencial se pide por petición.** Un token de vida corta cambia; una cabecera
   fijada al arrancar se queda vieja. Lo pone un `DelegatingHandler` — y la primera versión
   se olvidó del cliente de cargas, cosa que encontraron las pruebas.
3. **El token no se registra nunca**, ni siquiera cuando el emisor responde con error: el
   mensaje lleva el código de estado, no el cuerpo.

**Reautenticación y alcance:**

- `ApimReauthentication` cierra la sesión anterior y sale al mismo flujo de código de
  autorización, con los parámetros que APIM espera (`acr_values`, por ejemplo). Exigir MFA
  sigue siendo política del IdP o de APIM, no del código (SEC-04).
- Si el IdP no puede emitir el claim de alcance, se mapean grupos
  (`Authorization:ScopeFromGroups`). **El claim manda sobre los grupos:** sumarlos podría
  ampliar un alcance ya emitido, y eso no puede pasar por accidente (H-02).

**Medido en pruebas:** 20 llamadas simultáneas al vencer el token piden **una sola** vez el
token nuevo; un token de otra audiencia, de otro emisor, vencido, sin firma o firmado con
otra llave se rechaza; el cuerpo del error del emisor no llega al mensaje.

**Lo que falta, y por qué:** el emisor. Con APIM real hay que verificar que el token traiga
la audiencia de la capa System y un `azp` estable, que la reautenticación llegue al IdP
(SEC-19) y que el alcance entregue lo que el negocio espera. El cambio, paso a paso y con
su vuelta atrás, está en el [runbook de paso a APIM](docs/runbooks/paso-a-apim.md).

### 8.9 Las seis preguntas del portafolio (pantalla 09)

Pedido el 2026-09-16: que el sitio permita responder seis preguntas de gestión. Las seis
se responden con las ocho fuentes actuales, el score ya calculado y el catálogo de
roadmaps; ninguna necesitó un dato nuevo para existir. El método, sus supuestos y lo que
cada uno asume están en
[ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md).

| Pregunta | Dónde se responde | Con qué |
|---|---|---|
| ¿Qué tan sano está el portafolio? | Insights → *How healthy is the portfolio?* | Promedio y mediana, reparto por tramo, cobertura de dato y críticas bajo el umbral sano |
| ¿Qué modernizar primero? | Insights → *Which applications should be modernized first?* | Uplift del roadmap (motor de what-if) ÷ semanas de sus hitos, ponderado por criticidad, usuarios y capacidades |
| ¿Dónde reducir complejidad? | Insights → *Where should we reduce complexity?* | Misma capacidad y país (umbral ajustable 2–5), misma capacidad en varios países, `Eliminate` todavía activas, una capacidad en muchos lenguajes |
| ¿Dónde se concentra el riesgo? | Insights → *Where is technology risk concentrated?* | Por LOB, país, CIO y capacidad: flaws críticos y altos, Kenna bajo 50, componentes obsoletos o no aprobados, críticas expuestas a internet |
| ¿Qué causa la obsolescencia? | Insights → *What is driving obsolescence?* | Puntos perdidos por métrica = peso × (100 − score mapeado) ÷ 100, más una columna aparte con el peso sin dato |
| ¿Dónde faltan defensas? | Insights → *Where are the resilience gaps?* | Fuera del alcance de DR, sin monitoreo, score operacional bajo 60, sin RTO o RPO |

**Cómo encaja con el resto del sitio.** La pantalla usa los mismos filtros de la barra
superior, que viven en la URL ([ADR 0016](docs/adr/0016-el-filtro-vive-en-la-url-y-solo-acota.md)),
y dos controles propios que también viajan en ella: el umbral de duplicados y la
agrupación del riesgo. Un grupo de riesgo se convierte en filtro de todo el sitio con un
clic, y cada aplicación enlaza a su plan o a su detalle. El análisis se calcula sobre lo
que el alcance del usuario deja ver, no sobre el portafolio completo filtrado después.

**Dónde vive.** `src/shared/Modernization.Insights` (modelo y analizador, función pura),
`GET /system/insights` en la capa System, `GET /api/insights` como paso en la capa Channel
y `src/web/src/routes/InsightsPage.tsx` en la SPA. Doce códigos de aviso nuevos
(`insights.*`) con su frase en el catálogo de la interfaz.

**Qué se probó.** 14 pruebas del analizador sobre un portafolio sintético de once
aplicaciones (duplicados a propósito, una crítica expuesta sin DR ni monitoreo, dos
`Eliminate` activas, una sin plan), 14 de punta a punta por HTTP —las seis respuestas,
el filtro, el alcance de un usuario limitado a un LOB, los umbrales acotados por el
servidor y el portafolio vacío—, 4 de Vitest sobre las frases y la URL del cliente, y 4
de Playwright más la revisión con axe en claro y oscuro.

**Lo que mejoraría las respuestas.** Dos de las seis son hoy indicativas: el esfuerzo sale
del roadmap y no de la aplicación, y el valor de negocio es un proxy de tres atributos.
Los datos que lo cambiarían están en [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md) como
**DQ-01 a DQ-07**, con qué respuesta mejora cada uno.

### 8.10 Dashboard ejecutivo — Portfolio Health & Risk Intelligence (plan del 2026-09-18)

**Origen.** La propuesta *EA Data & Insights – MVP Proposal (DRAFT)*, su mockup y sus
refinamientos, recibidos como 16 capturas. Las capturas quedan fuera del repositorio, igual
que las del BRD, y la marca de la empresa no se lleva al sitio.

**La pregunta ejecutiva:** *dónde se concentra el riesgo del portafolio y la oportunidad de
modernización, por qué existe, y qué decisión de arquitectura hace falta.* El dashboard la
responde con las mismas seis preguntas de la pantalla 09, pero en una sola vista para
dirección: cinco indicadores arriba, seis paneles, y un detalle al hacer clic que muestra
los mismos campos en todos los paneles. El mockup tiene siete indicadores; Tech Debt queda a
decisión y Governance Adoption, fuera.

**Lo que dicen los refinamientos, y que se toma como decidido:**

| Elemento | Refinamiento |
|---|---|
| Filtros | Los del dashboard del Modernization Score: CIO, CRE, Region, Country, EAI Code, App Status, Primary Capability y Critical App |
| Indicadores | Health Score, Apps at Risk y Modernization Candidates, **sí**. EOS/EOL y Resilience, **redefinidos** según sus paneles. Tech Debt, **en duda**. Governance Adoption, **fuera** |
| Pregunta 1 | Mapa de calor de **Primary Capability contra Region, Country, CIO o Target Disposition**; detalle al hacer clic, **igual en todos los paneles** |
| Pregunta 2 | Matriz criticidad × oportunidad, tamaño = riesgo. «Rules & Matrix to be defined globally» |
| Pregunta 3 | Seis dimensiones: *Legacy Platforms* = Currency Score < X; *Tech Debt* = deudas técnicas abiertas; el resto TBD |
| Pregunta 4 | El mismo mapa de calor de la pregunta 1, midiendo riesgo |
| Excel y varias regiones del 2026-10-07 | La carga acepta libros `.xlsx` (primera hoja visible) y varios archivos de una misma fuente, que se cargan juntos; una copia exacta se rechaza (ADR 0034) |
| Plan automático del 2026-10-07 | Sin roadmap de MAPS (el export real no trae la columna), la oportunidad de la pregunta 2 sale de un plan automático con las palancas que llevan la aplicación a **90**; las marcadas Eliminate no reciben plan (`automaticPlanTarget` en el catálogo de roadmaps, ADR 0033) |
| OSS y Raw Tech del 2026-10-07 | Entran con su valor bruto por su peso, como el reporte v1.3 (37 × 10 % = 3,70), en lugar de las bandas del BRD (ADR 0032). La 14381 pasa de 79,67 a 80,70; con las bandas del BRD sigue dando 79,67 |
| Regla de DR del 2026-10-07 | DR solo puntúa en aplicaciones críticas: con el Critical Application Indicator en «N» queda sin dato, como en el reporte v1.3 (`criticalOnly`, ADR 0031) |
| Ajuste del 2026-10-06 | Filas de los mapas 1 y 4 por **CIO**; la capacidad primaria pasa a ser columna junto a Region, Country y Target Disposition. **Totales** por fila, columna y portafolio, y totales en las demás secciones donde aplican |
| Pregunta 5 | Explicar qué causa el riesgo, no solo un score; según los conceptos de **Currency Scoring** |
| Pregunta 6 | Resiliencia como dimensión propia, según los conceptos de **Stability Scoring** |

**Qué se reutiliza y qué cambia:**

| Panel | Se reutiliza | Se ajusta o es nuevo | Dato que falta |
|---|---|---|---|
| Encabezado | Filtros en la URL (ADR 0016), alcance sobre los datos (ADR 0012) | Cinco filtros más: CRE, Region, EAI Code, App Status, Primary Capability | — |
| Indicadores | Score, tramos, historia de cargas | Cinco indicadores con su tendencia por carga | — |
| 1 · Salud | Promedio, mediana, tramos, historia | Mapa de calor capacidad × eje elegible; dona de tramos; tendencia | — |
| 2 · Prioridad | Uplift del roadmap con el motor de what-if | Matriz de burbujas y top 10 con niveles de criticidad, oportunidad y riesgo | Esfuerzo por aplicación (DQ-02) |
| 3 · Complejidad | Duplicados por país, lenguajes, Eliminate activas | Treemap de seis dimensiones y cuatro oportunidades de reducción | Dependencias compartidas (DQ-04), deudas técnicas abiertas, application class (DQ-01) |
| 4 · Riesgo | Índice por grupo | Mapa de calor de riesgo, top 5 e índice de concentración de 0 a 1 | — |
| 5 · Obsolescencia | Puntos perdidos por métrica | Agrupados en conceptos de ciclo de vida y vigencia tecnológica, con el % que explican | Fechas EOS/EOL: TLM (DQ-05) |
| 6 · Resiliencia | DR, RTO/RPO, monitoreo, score operacional | Niveles por dimensión y por aplicación, distribución y titular | Disponibilidad, puntos únicos de falla, dependencias críticas (DQ-06, DQ-04) |
| Pie | — | Fuentes conectadas y no conectadas, dichas con esas palabras | TLM, Architectural Assessments, ARB, Resiliency Data |

**Definiciones provisionales.** Todo lo que la propuesta deja en TBD o remite a «conceptos
disponibles» que no llegaron se implementa como **regla versionada con un default**. Los
valores de fábrica están en `contracts/dashboard-v1.json` y se documentan en el
[ADR 0024](docs/adr/0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md); desde el
[ADR 0025](docs/adr/0025-las-definiciones-viajan-con-las-reglas.md) viajan con cada versión de
reglas y se editan desde *Rules → Portfolio analytics*, con estado y dueño por área:

| Concepto | Default | Por qué |
|---|---|---|
| Currency Score | **Decidido el 2026-09-18:** el score que entrega CMT, `Raw_Tech_Score` del Currency Model Detail Report (0–100); un 0 es falta de dato | El negocio lo definió así; es el único score de CMT |
| Legacy Platforms | Currency Score < 60 | La X del refinamiento; 60 es donde termina la banda más baja del BRD (0–59) |
| Risk Score (0–100, alto = peor) | El pilar *Modernization Risk Vectors* invertido: (100 − pilar) ÷ 80 × 100 | La propuesta asigna ese pilar a la pregunta 4 |
| Niveles de riesgo | 0–39 bajo · 40–59 moderado · 60–79 alto · 80–100 crítico | Los del mockup |
| Criticidad de negocio | Muy alta: crítica y con base de usuarios o tres capacidades · Alta: crítica · Media: base de usuarios o tres capacidades · Baja: el resto | Los tres atributos que ya pondera la pantalla 09 |
| Oportunidad de modernización | Uplift del propio roadmap: < 5 baja · 5–10 media · 10–20 alta · ≥ 20 muy alta | El único impacto medido que existe |
| Modernization Candidates | Criticidad y oportunidad altas o muy altas: el cuadrante superior derecho | El mockup ubica ahí a los candidatos |
| Índice de concentración de riesgo | Parte del riesgo total que cargan el 20 % de las celdas con más riesgo | 0,2 es riesgo parejo; 1,0 es todo en pocas celdas |
| Niveles de resiliencia | Recuperación por DR y RTO; vulnerabilidad operacional por score operacional y monitoreo; el nivel de la aplicación es el peor de sus dimensiones | Los conceptos de Stability que ya tiene el modelo |

**Fases:**

| Fase | Contenido | Criterio de aceptación |
|---|---|---|
| **D1 · Modelo, definiciones y filtros** | `contracts/dashboard-v1.json`; `DashboardAnalyzer` como función pura en `Modernization.Insights`; `GET /system/dashboard` y `GET /api/dashboard`; los cinco filtros nuevos en las dos capas; tendencia por carga con el mismo filtro | Cada indicador y panel probado con un portafolio sintético; el alcance de un usuario limitado se respeta; contrato C#↔TS en verde |
| **D2 · La pantalla** | `/dashboard` como primera pestaña; encabezado con la pregunta ejecutiva y los filtros; cinco indicadores; seis paneles; detalle lateral con los mismos campos en todos; pie de fuentes; claro y oscuro | e2e de las seis preguntas y del detalle; axe sin violaciones serias en claro y oscuro |
| **D3 · Diccionario y gobierno de las definiciones** | Una página por indicador con dueño, definición, fórmula, fuente, frecuencia y control de calidad —lo que exige el paso 3 de la propuesta—; las definiciones de D1 editables desde el portal como sección nueva de las reglas versionadas | Ningún indicador sin sus seis campos; cambiar un umbral desde el portal mueve el dashboard sin desplegar |
| **D4 · Fuentes nuevas** | TLM (EOS/EOL), datos de resiliencia, deudas técnicas abiertas, application class, dependencias | Cada fuente encendida convierte un bloque «sin dato» en una medición |

**Estado al 2026-09-18: D1 y D2 entregadas.** El backend suma 28 pruebas del analizador y 13
en la suite de API; la pantalla, 4 pruebas de flujo y 2 de accesibilidad con el detalle abierto, más
el barrido de axe en claro y oscuro. axe encontró tres textos bajo 4,5:1 —atenuados con
`opacity` o en tinta secundaria sobre una tesela teñida— y se corrigieron. El registro de
evidencia de valor queda como PF-08, en `docs/PENDING-ITEMS.md`.

**D3 entregada el mismo día ([ADR 0025](docs/adr/0025-las-definiciones-viajan-con-las-reglas.md)).**
Con tres definiciones del negocio: el Currency Score es el que entrega CMT, el dashboard es la
página de inicio (`/`; el inventario pasa a `/portfolio`) y los cálculos de las seis
preguntas son paramétricos y se configuran desde el sitio.

- **Qué se edita:** 37 parámetros en siete áreas —currency, priorización, complejidad,
  riesgo, obsolescencia, resiliencia y la pantalla Insights—, cada área con estado
  (*provisional* o *agreed*) y dueño. Viajan como cuarta parte de cada versión de reglas y
  heredan borrador, simulación, reautenticación, auditoría, comparación y vuelta atrás.
- **Qué muestra la simulación:** los cinco indicadores del dashboard antes y después. Una
  definición no mueve ningún score.
- **Qué explica el sitio:** cada panel del dashboard y de Insights tiene «How this is
  calculated», con los valores vigentes. La pantalla *Definitions* es el diccionario de
  indicadores: definición y fórmula, datos, valores, dueño, estado, frecuencia y cobertura.
- **Corrección:** el índice de riesgo de Insights contaba Kenna **< 50** como gestión débil;
  Kenna va de 0 a 1000 y más alto es peor. Ahora es Kenna ≥ 670 y es un parámetro.
- **Pruebas:** 28 más en el motor, 5 más en la API, 11 más en Vitest y 10 escenarios e2e más,
  con axe en las pantallas nuevas y con una explicación abierta.

**Fuera de este plan:** el *EA Value Evidence Register* (paso 7 de la propuesta), que es una
herramienta de registro y seguimiento y merece su propio plan; el indicador de gobierno
(N/A según el refinamiento); y lo que la propuesta deja fuera del MVP.

**Decisiones abiertas, con el default ya implementado** (DD-01 a DD-08 en
`docs/PENDING-ITEMS.md`, con lo que muestran los datos dummy): el umbral X del Currency Score;
la fórmula del Risk Score; los niveles de criticidad y oportunidad; si el indicador de Tech
Debt se queda; si los tramos de salud adoptan los nombres y cortes del mockup (*Healthy
80–100, Monitor 60–79, At Risk 40–59, Critical < 40*) o conservan los del modelo; y si las
definiciones necesitan un rol propio. Todas menos una fórmula nueva se cierran publicando
desde el portal, sin desplegar.

### 8.11 Pantalla Code — obsolescencia y riesgo desde los repositorios (2026-09-26)

**El pedido:** analizar el código de las aplicaciones directamente desde GitHub Enterprise,
sin configurar todavía la integración; dejarla en el plan y, con lo que se puede obtener de
cada repositorio, construir una vista de obsolescencia y riesgo según las librerías, el
software y los componentes, con una fuente para el fin de vida.

**C1, entregada el mismo día ([ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)).**

| Pregunta de la pantalla | Qué muestra |
|---|---|
| ¿Qué componentes llegaron o están por llegar a su fin de vida? | Cada runtime, framework, servidor, imagen de sistema operativo y base de datos que declaran los repositorios, por ciclo, con su fecha, su estado y el enlace a endoflife.date; una barra por estado que filtra la tabla |
| ¿Qué bibliotecas necesitan atención? | Las que están obsoletas, varias versiones mayores atrás o con alertas críticas y altas, con cuántas aplicaciones las usan |
| ¿Dónde el código contradice al inventario? | *Hidden obsolescence*: vigente según el Currency Score de CMT, con componentes vencidos en el código. *Inventory behind the code*: legada según CMT, sin nada vencido en el código |
| ¿Qué aplicaciones cargan más riesgo de código? | El índice de riesgo de código y su nivel; el detalle de cada aplicación muestra sus repositorios y de dónde sale cada punto |

- **Fuente del fin de vida:** endoflife.date, como foto versionada en
  `contracts/eol-catalog-v1.json` (22 productos, 273 ciclos, consultada el 2026-09-26). El
  soporte extendido pagado se muestra, pero no cuenta como soporte.
- **Reglas:** el área `code` de las definiciones, con 15 parámetros editables en
  *Rules → Portfolio analytics* y explicados en *Definitions*. El índice no entra al
  Modernization Score.
- **Datos de demostración:** generados del portafolio vigente, deterministas por EAI;
  el lenguaje del maestro elige la familia tecnológica y el Currency Score inclina las
  versiones. COBOL y ABAP casi nunca tienen repositorio, como en la realidad. La pantalla
  lo dice en ámbar, y ese proveedor no arranca fuera de Development.
- **Pruebas:** 39 más en el motor, 5 en las fuentes, 14 en la API, 5 en Vitest y 5
  escenarios e2e, más axe en la pantalla y en el detalle, en claro y en oscuro.

**C1 ampliada el 2026-09-27.** Tres pedidos más sobre la misma pantalla:

| Pedido | Cómo quedó |
|---|---|
| El estado actual contra la obsolescencia estimada en uno, dos y tres años si nada cambia | *Where is obsolescence heading if nothing changes?*: por horizonte, componentes vencidos, aplicaciones afectadas, vigencia del código (parte de los componentes con soporte) e índice de riesgo. Lo mismo por aplicación en su detalle, y una columna «In 3 years» en la tabla |
| La sugerencia de mejora que debe aplicarse | *What should change first?*: el plan del portafolio, cada acción una vez con sus repositorios y aplicaciones, por urgencia y por puntos que baja o evita; y la lista propia de cada aplicación, con sus repositorios abandonados. Destino: el ciclo con soporte que más dura, LTS cuando existe |
| Al menos tres bases dummy nuevas para comparar score, Currency y riesgo en el tiempo | `data/timeline`: el mismo portafolio en Q4 2025, Q1, Q2 y Q3 2026 (cuatro cargas con su manifiesto). El modo Dummy trae los mismos cuatro trimestres. *How did score, currency and risk move across uploads?* compara las tres medidas carga por carga, y cada aplicación muestra la suya |

El mismo día se retiró el paquete portable: la aplicación se levanta desde el código con
comandos para cmd y PowerShell, y un equipo que bloquea `.exe` propios usa el sitio compilado
servido por la Channel. Ese sitio viaja con el código en `sitio-compilado/` desde el
2026-10-05 (antes, artefacto del CI), y el CI exige que sea el build de ese commit
([ADR 0029](docs/adr/0029-el-sitio-compilado-viaja-con-el-codigo.md)): basta el ZIP de GitHub
para levantarlo sin Node ni Vite, camino C de la [guía](docs/guia-localhost.md).

**C2 — la integración con GitHub Enterprise** está en [8ter](#en-paralelo--integración-con-github-enterprise-pantalla-code)
y en GH-01 a GH-09 de [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md).

### 8.12 Mapping de columnas antes de cargar y motivos de rechazo (2026-09-28)

La primera carga con datos reales dejó el EAI Master en 0 filas y 3.869 rechazadas, sin decir
por qué, con datos que no se pueden compartir. El mismo día pidieron un módulo de mapping
previo a la carga: el linaje describe lo que ya entró, y hacía falta decidir antes **qué
archivo y qué columna cargan cada valor en la base**. Quedó así
([ADR 0027](docs/adr/0027-el-mapping-de-columnas-se-decide-antes-de-cargar.md)):

| Pedido | Cómo quedó |
|---|---|
| Saber por qué se rechazaron las filas sin compartir el archivo | *Why rows were rejected*, bajo el detalle por archivo: los rechazos agrupados por problema —columna, qué esperaba, el valor de la primera fila, cuántas filas y las primeras—, contando todas las columnas malas de cada fila, con una pista cuando el valor delata la causa. El resultado por archivo ya no dice «accepted» si ninguna fila sirvió |
| Decidir antes de cargar qué columna carga cada campo | *Review column mapping*: el navegador lee solo la primera línea de cada archivo y la System propone, por cada campo, la columna que lo cargaría (mismo nombre, alias conocido, como en la carga anterior o ninguna), con su tipo y las métricas que lo usan. Se confirma o se elige otra, y *Upload with this mapping* publica con ese mapping |
| Que quede registrado y se reutilice | Cada carga registra qué columna cargó cada campo (manifiesto; columna `ColumnMaps` en la base, migración `MapeoDeColumnas`). La próxima revisión lo propone si el archivo sigue trayendo esas columnas |
| Un módulo propio, previo a la carga | Pestaña **Data → Mapping**: por fuente, la columna de la carga vigente, los nombres con que cada campo se reconoce solo y las métricas que lo usan, sin elegir archivos |

El mapping se aplica **reescribiendo el encabezado** con los nombres canónicos: el lector no
cambia, el archivo guardado dice por sí mismo de dónde salió cada valor y las versiones
viejas se releen igual. De paso se corrigió la lectura de números con los dos separadores
(`1.234,5` se leía 1,2345) y la etiqueta del selector de archivos, que las pruebas de
accesibilidad nunca habían visto porque con datos generados la carga está apagada.

**Ampliado el 2026-10-02 con el encabezado real del EAI Master**, exportado de Power BI
([ADR 0028](docs/adr/0028-la-lectura-tolera-los-exports-reales.md)): un valor ilegible en un
campo opcional queda sin dato y la fila entra (*Values loaded as empty*); `N/A`, `-` y
`(En blanco)` son vacío; números en cualquier formato regional; `VERDADERO`/`FALSO`;
identificadores normalizados; el nombre exacto gana a un alias y se reconocen los «Suma de»;
un EAI repetido deja la última fila; y sin APP_ID en el maestro, la deuda técnica se une por el
EAI, que en la organización es el mismo código. Verificado en vivo: el export de ejemplo con ese formato carga 250
aplicaciones sin rechazos y la 14381 sigue en 79,67.

**Ampliado el 2026-10-05 para el export real de SCA**, de 73 MB
([ADR 0030](docs/adr/0030-una-carga-grande-cabe-en-el-pod.md)): el tope pasa de 64 a **128 MB
por archivo**. Antes se midió contra la memoria del pod de la System (1 GiB): con la lectura
anterior, un SCA de 350 mil filas la agotaba. Cada fila es ahora un arreglo sobre un índice de
columnas compartido, con los textos repetidos una sola vez, y entran 490 mil filas (98 MB). La
doble lectura de cada archivo al publicar queda en TD-17.

## 8bis. Ampliación solicitada — fases 10 a 12

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

### 8bis.1 Fase 10 — Experiencia de consulta (entregada, ver 8.3)

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

### 8bis.2 Fase 11 — Sitio en inglés (entregada, ver 8.4)

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

### 8bis.3 Fase 12 — Administración de reglas (entregada, ver 8.5)

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
  reescribe la historia: las cargas anteriores conservan la suya. *(Corregido al
  entregar: la historia se puntúa con las reglas vigentes; ver 8.5 y ADR 0018.)*

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

## 8quater. Plan de trabajo del 2026-10-21

Pedido del 2026-10-21, en cuatro bloques que se entregan y se suben por separado.

| Bloque | Qué | Cómo | Estado |
|---|---|---|---|
| **W1 — Navegación** | Quitar **Insights** (responde lo mismo que el dashboard). Ocultar **Code** y **Roadmap** | Insights se retira entero: pestaña, pantalla, endpoints de las dos capas, analizador y sus 13 parámetros en *Rules* (las versiones guardadas siguen cargando). Code y Roadmap se ocultan con interruptores de la capa Channel (`Features:Code`, `Features:PortfolioRoadmap`, apagados por defecto): la SPA no los ofrece, sus rutas vuelven al dashboard y sus endpoints responden 404. Se encienden sin recompilar el sitio ([ADR 0035](docs/adr/0035-navegacion-sin-insights-y-pantallas-apagables.md)) | **Entregado** el 2026-10-21 |
| **W2 — Future plan** | Al cargar datos, cada aplicación tiene su *future plan* hacia 90, con el máximo detalle de acciones, y el what-if a 90 da exactamente el mismo plan | Un solo algoritmo para los dos (`WhatIfEngine.ReachTarget`: greedy verificado con el motor y poda); si 90 no se alcanza, los dos muestran el mismo mejor plan posible. Los planes se calculan al publicar la carga. Cada paso dice la métrica, su valor hoy y el objetivo, los puntos que suma en orden y las acciones concretas con la evidencia de la aplicación (hallazgos de SCA por severidad con sus librerías y CVE, capacidades de monitoreo que faltan, productos a reclasificar…). Botón *Open in What-If* que carga el mismo plan ([ADR 0036](docs/adr/0036-future-plan-detallado-y-mismo-plan-que-el-what-if.md)) | **Entregado** el 2026-10-21 |
| **W3 — Fuentes desde bases de datos** | Leer las fuentes directamente de las bases de la organización, sin archivos, con conexiones seguras y varias formas de conexión | Conectores de solo lectura para SQL Server (usuario SQL, Windows integrado, Microsoft Entra ID), PostgreSQL (contraseña, certificado de cliente), Oracle (usuario, wallet) y ODBC (cualquier driver). Consulta o vista por fuente, definida en configuración y nunca desde la pantalla; TLS obligatorio y verificado; secretos desde variables de entorno o archivos montados (Kubernetes / Key Vault), nunca en el repositorio; tiempos y filas acotados. *Pull from databases* en Data publica una versión por el mismo camino de validación que una carga. Guía en inglés: `docs/database-connections.md` | Pendiente |
| **W4 — Auditoría** | Auditoría exhaustiva: malas prácticas y vulnerabilidades | Paquetes vulnerables (.NET y npm), analizadores, revisión manual de autenticación, CSRF, cabeceras, CSP, cookies, cargas, deserialización, expresiones regulares, registros, errores, secretos, contenedores y CI. Se corrige lo encontrado y queda el informe en `docs/security/audit-2026-10.md` (en inglés) | Pendiente |

---

## 8ter. Plan desde la Fase 14

Ordenado según las prioridades del 2026-09-14:

1. las reglas de cálculo mandan;
2. los parciales de ASVS se resuelven;
3. APIM reemplaza a OIDC;
4. los hallazgos de infraestructura H-16, H-17, H-20 y H-21 van al final.

El detalle de cada punto está en [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md), con su
identificador.

### En paralelo desde ya — decisiones de negocio sobre el score

| Punto | Decisión | Por qué antes de la Fase 14 |
|---|---|---|
| SR-01 · RFC-01 | Estrategia de los compuestos | 20 de 100 puntos de peso |
| SR-03 · RFC-03 | Peso de Tech Debt 5 o 10 | No está verificado con ningún caso dorado |
| SR-07 · RFC-13 | Lectura de *"null or zero values are excluded"* | Cambia el score de toda aplicación con ceros |
| SR-04, SR-05, SR-06, SR-10, SR-12 | Override, SCA, huecos de bandas, Containerized, nivel de agregación | Diferencias visibles contra el dashboard |

Cada decisión se aplica publicando una versión de reglas desde el portal, sin desplegar,
salvo SR-04 (override) y SR-12 (agregación), que requieren código.

### Fase 14 — Verificación de seguridad (ASVS L2)

| Trabajo | Requisitos o puntos |
|---|---|
| Primer push y primera corrida del pipeline con todos los gates; corregir lo que encuentren | SEC-20, OP-04 |
| Construir y escanear las imágenes; ZAP contra staging | SEC-20 |
| Verificar en un entorno de prueba: llaves protegidas, mTLS del mesh, gestor de secretos, TLS del ingress, cifrado interno | ASVS 1.6.2, 1.9.2, 6.4.1, 9.1.2, 9.1.3, 9.2.2 |
| MFA exigido por el IdP a `ingestion.write` y `ruleset.admin` | ASVS 4.3.1 |
| Auditar validaciones rechazadas en endpoints sensibles | ASVS 7.1.3 |
| Política de retención de cargas | ASVS 8.3.4, OP-03 |
| Reglas del SIEM por EventId | ASVS 11.1.8, PA-08 |
| Decidir validación de esquema formal para los cuerpos de reglas | ASVS 13.2.2 |
| Controles compensatorios documentados para aislamiento de terceros | ASVS 14.2.6 |
| Revisión de seguridad de las fases 09–13 por otra persona | TD-13 |
| e2e y accesibilidad (axe) en CI; barrido de idioma sobre los endpoints nuevos ✅ | TD-04, TD-05, TD-09 |

**Criterio de aceptación:** pipeline verde con todos los gates, y cada parcial en cumple o
con control compensatorio documentado y aceptado por seguridad.

### Fase 15 — Autenticación con APIM

Opciones del [ADR 0020](docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md): APIM delante
del BFF (A), JWT validado por APIM (B) o APIM entre capas (C). Se recomienda **C con A**.

| Trabajo | Puntos |
|---|---|
| Elegir la opción ✅ (C con A, ADR 0022) y obtener el producto y las políticas de APIM | PA-02 |
| Implementar `IReauthenticationFlow` para APIM ✅ y verificar la frescura de la autenticación contra el proveedor real | SEC-19 |
| Claim de alcance emitido por el IdP o APIM, con mapeo de grupos como respaldo ✅ | PA-03 |
| Reemplazar el token de servicio estático (opción C) ✅ código listo, falta el emisor | PA-05, ASVS 2.10.1 |
| Actualizar el modelo de amenazas (fronteras ② y ⑥) | — |

**Criterio de aceptación:** publicar reglas en el entorno de prueba exige reautenticarse
ante el proveedor real, y no queda ningún secreto estático entre las capas.

### Fase 16 — Escala y operación

| Trabajo | Puntos |
|---|---|
| Destino de despliegue y motor de base; migraciones y job de despliegue | PA-01 · RFC-07 |
| Redis para sesiones y llaves de Data Protection compartidas | PA-04 · RFC-11 |
| Dos réplicas por capa bajo carga; adaptador de base para varias réplicas | PA-06, TD-06, TD-07 |
| Respaldos del volumen y del almacén de reglas; prueba de restauración | OP-02 |
| Recorrer los runbooks con operación | OP-01 |
| Notificar a los responsables cuando se publican reglas | PA-09 |

**Criterio de aceptación:** dos réplicas de cada capa sirven bajo carga, una publicación
llega a todas dentro del intervalo de refresco y una restauración funciona.

### En paralelo — Integración con GitHub Enterprise (pantalla Code)

La pantalla Code ya existe con datos de demostración (8.11). Esta es la vía para llenarla
con repositorios reales, según el [ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md).
No depende de las fases 14 a 16 salvo en el destino de despliegue (PA-01), y puede empezar
en cuanto se aprueben la GitHub App y el mapeo.

| Trabajo | Puntos |
|---|---|
| Aprobar la GitHub App: organizaciones, permisos de solo lectura (*Metadata*, *Contents*, *Dependabot alerts*, *Custom properties*), versión de GitHub Enterprise que expone el SBOM y las alertas | GH-01 |
| Convención de mapeo repositorio → aplicación: propiedad personalizada `eai-code`, y cómo se tratan los monorepos y las bibliotecas compartidas | GH-02 |
| `code-collector` como tarea programada, sin ingress: lee metadatos, SBOM, alertas y la lista cerrada de manifiestos; no clona ni ejecuta nada | GH-01, GH-03 |
| Salida de red: endoflife.date y registros de paquetes, directos o por espejos internos | GH-03 |
| Fuente de vulnerabilidades: Dependabot, OSV.dev o ambas | GH-04 |
| Endpoint interno de la capa System para publicar el inventario: token de servicio, rol propio, esquema, límites, versionado y retención como una carga | — |
| Refresco del catálogo de fin de vida y excepciones por contrato de soporte extendido | GH-06 |
| Acordar los puntos y niveles del índice (área `code`) y marcarla como *agreed* | GH-07 |
| Qué hacer con lo que no vive en GitHub (COBOL, ABAP) | GH-08 |
| Circuito con los dueños de CMT cuando el código contradice al Currency Score | GH-09 |
| Imágenes de contenedor: escanear lo que corre, no solo lo que se declara | GH-05 |

**Criterio de aceptación:** la pantalla Code muestra los repositorios reales de una
organización piloto, con el proveedor de demostración apagado; ni la capa System ni la
Channel tienen credenciales de GitHub; un repositorio sin mapeo aparece en la lista de
huérfanos y no en ningún conteo; y el catálogo se refresca solo, conservando el anterior si
el nuevo no valida.

### Fase 17 — Hallazgos de infraestructura (al final, por decisión)

| Hallazgo | Requisito ASVS | Trabajo |
|---|---|---|
| H-16 · Token de servicio estático | 2.10.1 | Identidad de carga de trabajo de vida corta, si la Fase 15 no lo cerró |
| H-17 · Cadena de publicación incompleta | 7.3.3, 14.1.5 | Firma de imágenes con cosign, verificación en admisión y logs de auditoría en destino inmutable |
| H-20 · Ver y cerrar sesiones activas | 3.3.4 | Índice de tickets por usuario, panel de sesiones y revocación |
| H-21 · Antivirus en cargas | 12.4.2 | Escaneo en staging antes de publicar |

**Criterio de aceptación:** los cinco requisitos ASVS pasan a cumple.

---

## 9. Estructura del repositorio

```
modernization-score/
├─ Directory.Packages.props          # versiones centralizadas
├─ Directory.Build.props             # warnings como errores, todas las reglas de seguridad, lock files
├─ .github/workflows/                # ci.yml: tests, CodeQL, Trivy, Checkov, SBOM · seguridad.yml: Gitleaks, OSV
├─ renovate.json · .gitleaks.toml
├─ .gitignore · .gitattributes          # capturas, imágenes, salidas de build y secretos fuera del repositorio
├─ .claude/launch.json               # servidor de desarrollo para la vista previa de Claude Code
├─ HANDOFF.md                        # traspaso completo del contexto
├─ docs/
│  ├─ PENDING-ITEMS.md               # pendientes para revisar con el equipo, en inglés
│  ├─ plan/                          # copia del plan publicado como artefacto
│  ├─ adr/                           # decisiones de arquitectura (0001–0022)
│  ├─ brd/                           # índice de las capturas del BRD; las imágenes no se versionan
│  ├─ security/                      # modelo de amenazas, ASVS L2, hallazgos
│  └─ runbooks/                      # secretos, carga defectuosa, DR, incidente, reglas, respaldo, verificación ASVS, paso a APIM
├─ contracts/
│  ├─ ruleset-v1.3.json              # pesos, bandas, estrategias
│  ├─ roadmap-v1.json                # modelo MAPS
│  └─ sources/source-schemas.json    # contratos de columnas por fuente
├─ data/                             # dummy/ (CSV generados) · history/ y modernization.db (estado local de prueba)
├─ src/
│  ├─ shared/
│  │  ├─ Modernization.Domain/          # modelo canónico + puertos
│  │  ├─ Modernization.Scoring.Engine/  # motor puro — el activo crítico
│  │  ├─ Modernization.WhatIf/ · Modernization.Roadmap/ · Modernization.Rules/
│  │  └─ Modernization.Security/        # alcance, auditoría, cabeceras, token de servicio
│  ├─ channel/
│  │  └─ Modernization.ChannelApi/      # BFF / orquestador; sirve la SPA en despliegue
│  ├─ system/
│  │  ├─ Modernization.SystemApi/       # host HTTP de la capa System
│  │  ├─ Modernization.Sources/         # ensamblador + adaptadores Dummy y File
│  │  │  └─ {Dummy,Files,Export}/
│  │  └─ Modernization.Sources.Database/ # adaptador EF Core, migraciones y publicación
│  └─ web/                              # React 19 SPA · src/**/*.test.ts (Vitest) · e2e/ (Playwright)
├─ tests/
│  ├─ Modernization.Scoring.Engine.Tests/     # golden tests, tabla de casos, what-if, roadmap
│  ├─ Modernization.Sources.ContractTests/    # una suite, dos adaptadores (tres en la Fase 09)
│  └─ Modernization.Api.Tests/                # E2E por ambas capas y seguridad
├─ tools/
│  ├─ Modernization.DummyData/       # generador determinista
│  ├─ Modernization.LoadTest/        # prueba de carga sin dependencias
│  ├─ git/                           # respaldo con verificaciones y archivos bloqueados
│  └─ sesiones/                      # scripts históricos de las sesiones de trabajo
└─ deploy/
   ├─ docker/                        # imágenes distroless (Azure Linux), no root
   └─ k8s/                           # kustomize, PSA restricted, NetworkPolicy, mesh
```

---

## 10. Decisiones que requieren confirmación

1. **RFC-01** — Estrategia de los compuestos Architectural Value y Product Strategy. Default: promedio ponderado por conteo. ¿Se confirma contra el modelo Power BI real?
2. **RFC-02** — Definición del **Currency Score** (94,66 en el dashboard). No aparece en el BRD capturado. ¿Existe documentación del CMT Currency Model?
3. **RFC-03** — Peso de Tech Debt: 5 (BRD) vs 10 (dashboard v1.3). Default: 10.
4. **RFC-04** — Semántica exacta del *"Override rule"* que muestra el dashboard con dos valores por pilar.
5. **Alcance del score** — ¿por aplicación, o agregado a nivel LOB/CIO? El dashboard agrega ("Suma de Kenna_Score"), lo que sugiere ambos. Default: cálculo por app + agregación posterior.
6. **Autenticación** — ¿Entra ID corporativo o Keycloak self-hosted para el piloto?
7. **Destino de despliegue** — AKS (coherente con las menciones a AKS/ASE del BRD) o alternativa. Decide también el **motor de base de datos**: el adaptador de la Fase 09 es agnóstico y hoy se verifica contra SQLite; SQL Server o PostgreSQL entran como un paquete y un juego de migraciones.
8. **RFC-10** — Claim de alcance del IdP: nombre (`portfolio_scope` por defecto) y valores (`*`, `lob:…`, `country:…`, `cio:…`). ¿Lo emite el IdP corporativo, o se deriva de grupos?
9. **RFC-11** — Cache compartido para las sesiones (Redis) antes de escalar channel-api a más de una réplica.
10. **RFC-12** — Identidad de carga de trabajo de vida corta entre channel-api y system-api, según la plataforma del cluster.
11. **RFC-13** — ¿El negocio lee *"null or zero values are excluded"* como la implementación: el cero se excluye salvo donde la tabla lo puntúa? En particular, ¿un Kenna 0 es "sin riesgo" o "sin escaneo"?
12. **RFC-14** — DevSecOps puntúa el nulo con 20 en el BRD; sin efecto mientras tenga peso 0.
13. **RFC-15** — `Consolidate` no figura en la tabla de puntaje de la disposición.
14. **RFC-16** — Containerized "added to product strategy (2 pts) and architecture value (2 pts)": ¿texto obsoleto o bonificación?
15. **RFC-17** — Filas con `EAI Code = 0` en OSS: ¿reportarlas como huérfanas en el linaje?
16. **Autenticación** — OIDC se reemplazará por OAuth mediante APIM: elegir la opción A, B o C del [ADR 0020](docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md).
17. **Notificaciones** — ¿A quién y por qué canal se avisa cuando se publican reglas? Hoy hay un aviso dentro del sitio.
18. **Repositorio** — Resuelto el 2026-09-15: `camilarodriguez10/modscore1` es privado; guarda todo lo creado salvo las capturas del BRD y las imágenes; los commits usan el correo de la cuenta.

Cada punto, con contexto, opciones, recomendación y criterio de aceptación, está en [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md).
