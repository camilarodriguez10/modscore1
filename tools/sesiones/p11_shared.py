from p11_lib import patch, translate

patch("src/shared/Modernization.Domain/ScoreBreakdown.cs", [
    ("""    /// <summary>Valor crudo formateado para mostrar, p. ej. "No (with users)".</summary>
    public required string RawValueDisplay { get; init; }""",
     """    /// <summary>
    /// Como se lee el valor crudo, como codigo y datos: la interfaz arma la frase
    /// (ADR 0017). P. ej. <c>value.mfa { state: "No (with users)", users: "120" }</c>.
    /// </summary>
    public required Notice Display { get; init; }"""),
    ("""    /// <summary>Motivo legible de la exclusion. Vacio si la metrica se incluyo.</summary>
    public required string ExclusionReason { get; init; }""",
     """    /// <summary>Por que la metrica quedo fuera del calculo. Null si se incluyo.</summary>
    public required Notice? Exclusion { get; init; }"""),
])

patch("src/shared/Modernization.Scoring.Engine/ScoringEngine.cs", [
    ("""                RawValueDisplay = "No Data Available",""", """                Display = input.Display,"""),
    ("""                ExclusionReason = input.MissingReason,""", """                Exclusion = input.Missing,"""),
    ("""            RawValueDisplay = input.Display,""", """            Display = input.Display,"""),
    ("""            ExclusionReason = string.Empty,""", """            Exclusion = null,"""),
])

patch("src/shared/Modernization.Scoring.Engine/MetricMapper.cs", [
    ('?? "valor directo en escala 0-100";', '?? "direct value on the 0-100 scale";'),
])

patch("src/shared/Modernization.WhatIf/ScenarioResults.cs", [
    ("    public string? NotActionableReason { get; init; }", "    public Notice? NotActionableReason { get; init; }"),
])

patch("src/shared/Modernization.WhatIf/WhatIfEngine.cs", [
    ("""            throw new WhatIfException(
                $"Un escenario admite hasta {MaxLeversPerScenario} palancas; se recibieron {selections.Count}.");""",
     """            throw new WhatIfException(
                "too-many-levers",
                $"A scenario accepts up to {MaxLeversPerScenario} levers; {selections.Count} were received.");"""),
    ("""            throw new WhatIfException(
                "El score objetivo debe estar entre 20 y 100.");""",
     """            throw new WhatIfException(
                "invalid-target",
                "The target score must be between 20 and 100.");"""),
    ("""            throw new WhatIfException(
                $"La palanca '{lever.Key}' necesita una cantidad de al menos 1.");""",
     """            throw new WhatIfException(
                "invalid-parameter",
                $"The lever '{lever.Key}' needs a quantity of at least 1.");"""),
])

patch("src/shared/Modernization.Roadmap/RoadmapResults.cs", [
    ("""    /// <summary>El dato que dispara la alerta, para que no haya que buscarlo.</summary>
    public required string Evidence { get; init; }""",
     """    /// <summary>
    /// El dato que dispara la alerta, para que no haya que buscarlo. Codigo y datos:
    /// la interfaz arma la frase (ADR 0017).
    /// </summary>
    public required Notice Evidence { get; init; }"""),
])

patch("src/shared/Modernization.Roadmap/RoadmapPlanner.cs", [
    ("void Raise(string key, string evidence) => alerts.Add(new CoherenceAlert",
     "void Raise(string key, Notice evidence) => alerts.Add(new CoherenceAlert"),
    ("""                Raise("roadmap.missing", $"Disposition {MapsNames.Display(disposition)} sin roadmap asignado.");""",
     """                Raise(
                    "roadmap.missing",
                    Notice.Of(NoticeCodes.AlertRoadmapMissing, ("disposition", MapsNames.Display(disposition))));"""),
    ("""                    $"{MapsNames.Display(disposition)} con {MapsNames.Display(snapshot.Roadmap.Value)}: "
                    + "ese par no existe en MAPS.");""",
     """                    Notice.Of(
                        NoticeCodes.AlertPairInvalid,
                        ("disposition", MapsNames.Display(disposition)),
                        ("roadmap", MapsNames.Display(snapshot.Roadmap.Value))));"""),
    ("""                    $"{invest} producto(s) clasificado(s) como Invest en una aplicacion marcada para salir.");""",
     """                    Notice.Of(NoticeCodes.AlertInvestProducts, ("count", invest)));"""),
    ("""                    $"{snapshot.UserCount:N0} usuarios registrados.");""",
     """                    Notice.Of(NoticeCodes.AlertManyUsers, ("users", snapshot.UserCount)));"""),
    ("""                $"{snapshot.ScaFlaws.Critical} flaw(s) SCA de severidad Critical abiertos.");""",
     """                Notice.Of(NoticeCodes.AlertCriticalFlaws, ("count", snapshot.ScaFlaws.Critical)));"""),
    ("""                $"{snapshot.UserCount:N0} usuarios sin multifactor; el rehost los deja igual.");""",
     """                Notice.Of(NoticeCodes.AlertUsersWithoutMfa, ("users", snapshot.UserCount)));"""),
    ("""                $"Score actual {baseline.EffectiveScore:0.00}, por encima de {catalog.Parameters.HealthyScore:0}.");""",
     """                Notice.Of(
                    NoticeCodes.AlertHealthyScore,
                    ("score", baseline.EffectiveScore),
                    ("threshold", catalog.Parameters.HealthyScore)));"""),
    ("""            Raise("critical.withoutDisasterRecovery", "Critical Application Indicator = Y, DR In-Scope = No.");""",
     """            Raise("critical.withoutDisasterRecovery", Notice.Of(NoticeCodes.AlertCriticalWithoutDr));"""),
])

patch("src/shared/Modernization.Roadmap/PortfolioRoadmapAnalyzer.cs", [
    ("""    /// <summary>Grupo de los que no declaran el atributo por el que se agrupa.</summary>
    private const string Unassigned = "Sin asignar";""",
     """    /// <summary>
    /// Clave del grupo de los que no declaran el atributo por el que se agrupa. Vacia
    /// y no "Unassigned": nombrarla es trabajo de la interfaz (ADR 0017), y una clave
    /// vacia tampoco sirve como filtro, que es exactamente lo correcto.
    /// </summary>
    private const string Unassigned = "";"""),
    ("""            return [Build("*", "Portafolio filtrado", planned)];""",
     """            // Una sola fila: la interfaz la nombra segun la agrupacion.
            return [Build("*", string.Empty, planned)];"""),
])

patch("src/shared/Modernization.Domain/Sources/ISourceReader.cs", [
    ("public sealed record SourceRejection(int RowNumber, string Reason, string? Key = null);",
     "public sealed record SourceRejection(int RowNumber, Notice Reason, string? Key = null);"),
    ("/// <summary>Fila descartada durante la lectura, con el motivo legible.</summary>",
     "/// <summary>Fila descartada durante la lectura, con el motivo como codigo y datos (ADR 0017).</summary>"),
])

patch("src/shared/Modernization.Domain/Sources/ISnapshotPublisher.cs", [
    ("""    IReadOnlyList<PublishedFileOutcome> Files,
    IReadOnlyList<string> Errors,
    int ApplicationCount)
{
    public static PublishOutcome Rejected(params string[] errors)
        => new(false, null, [], errors, 0);

    public static PublishOutcome Rejected(IReadOnlyList<PublishedFileOutcome> files, IReadOnlyList<string> errors)
        => new(false, null, files, errors, 0);""",
     """    IReadOnlyList<PublishedFileOutcome> Files,
    IReadOnlyList<Notice> Errors,
    int ApplicationCount)
{
    public static PublishOutcome Rejected(params Notice[] errors)
        => new(false, null, [], errors, 0);

    public static PublishOutcome Rejected(IReadOnlyList<PublishedFileOutcome> files, IReadOnlyList<Notice> errors)
        => new(false, null, files, errors, 0);"""),
    ("""    int RejectedCount,
    string? Error)
{""", """    int RejectedCount,
    Notice? Error)
{"""),
])

# ------------------------------------------------------------ contratos
translate("contracts/roadmap-v1.json", [
    ("Modelo MAPS del BRD: tres dispositions, siete roadmaps. Cada roadmap declara los hitos que lo componen y las palancas del what-if que su ejecucion entrega de forma natural, para poder proyectar el score futuro con el mismo motor que calcula el actual en lugar de con un numero escrito a mano.",
     "The BRD's MAPS model: three dispositions, seven roadmaps. Each roadmap declares its milestones and the what-if levers its execution naturally delivers, so the future score is projected with the same engine that calculates today's instead of with a hand-written number."),
    ("Optimizar preservando. No se requieren cambios, o la aplicacion necesita modernizacion pero no puede lograrse en el ano calendario en curso.",
     "Optimize while preserving. No changes are required, or the application needs modernization that cannot be achieved within the current calendar year."),
    ("Aplicaciones con encaje funcional al negocio, necesarias para sostener objetivos esenciales. Puede que requieran capacidades adicionales o que tengan restricciones tecnicas que resolver refactorizando o reimaginando.",
     "Applications with a functional fit to the business, needed to sustain essential objectives. They may require additional capabilities or have technical constraints to resolve by refactoring or reimagining."),
    ("La aplicacion sale del portafolio, reduciendo el conteo de aplicaciones.",
     "The application leaves the portfolio, reducing the application count."),
    ("Tambien llamado Retain. Inversion necesaria para mantener la aplicacion vigente y segura.",
     "Also called Retain. The investment needed to keep the application current and secure."),
    ("Mantener vigente y seguro es exactamente remediar vulnerabilidades y actualizar componentes. No toca arquitectura.",
     "Keeping it current and secure is exactly remediating vulnerabilities and upgrading components. It does not touch the architecture."),
    ("Inventario de vulnerabilidades abiertas", "Inventory of open vulnerabilities"),
    ("Plan de remediacion priorizado por severidad", "Remediation plan prioritized by severity"),
    ("Actualizacion de librerias y componentes", "Library and component upgrades"),
    ("Re-escaneo y verificacion", "Rescan and verification"),
    ("La aplicacion se migra a otro servicio de hosting o computo (AKS, ASE u otro) sin alterar su arquitectura ni su funcionalidad. No hay recompilacion.",
     "The application moves to another hosting or compute service (AKS, ASE or other) without changing its architecture or functionality. There is no recompilation."),
    ("La plataforma destino trae observabilidad y estabilidad. Al no haber recompilacion, la arquitectura y la deuda tecnica quedan igual.",
     "The target platform brings observability and stability. With no recompilation, the architecture and the technical debt stay the same."),
    ("Evaluacion de destino (AKS, ASE u otro)", "Target assessment (AKS, ASE or other)"),
    ("Preparacion de infraestructura y pipeline", "Infrastructure and pipeline preparation"),
    ("Migracion en ambiente no productivo", "Migration to a non-production environment"),
    ("Pruebas de carga y resiliencia", "Load and resilience testing"),
    ("Cutover y estabilizacion", "Cutover and stabilization"),
    ("Nueva funcionalidad o capacidades agregadas sobre la aplicacion actual.",
     "New functionality or capabilities added on top of the current application."),
    ("Evolucionar una aplicacion en la que se invierte implica mantenerla sana: se remedia, se salda deuda y se instrumenta.",
     "Evolving an application you invest in means keeping it healthy: it gets remediated, its debt paid down and it gets instrumented."),
    ("Definicion de capacidades a incorporar", "Definition of the capabilities to add"),
    ("Saneamiento de deuda tecnica bloqueante", "Clean-up of blocking technical debt"),
    ("Desarrollo incremental", "Incremental development"),
    ("Instrumentacion y observabilidad", "Instrumentation and observability"),
    ("Liberacion y medicion de adopcion", "Release and adoption measurement"),
    ("Cambios al codigo o a la arquitectura preservando la funcionalidad original. Aprovecha patrones arquitectonicos modernos. A diferencia de rehost, exige un compromiso mas profundo con la estructura de la aplicacion. Incluye las dispositions previas de rearchitect y re-platform.",
     "Changes to the code or the architecture that preserve the original functionality. It takes advantage of modern architectural patterns. Unlike rehost, it requires a deeper commitment to the application's structure. It includes the former rearchitect and re-platform dispositions."),
    ("Tocar la estructura es la oportunidad de modernizar componentes, exponer APIs nativas y saldar deuda. Es el roadmap con mas palancas a su alcance.",
     "Touching the structure is the opportunity to modernize components, expose native APIs and pay down debt. It is the roadmap with the most levers within reach."),
    ("Analisis de arquitectura actual y objetivo", "Analysis of the current and target architecture"),
    ("Definicion de patrones y limites de servicio", "Definition of patterns and service boundaries"),
    ("Refactorizacion por dominio", "Refactoring by domain"),
    ("Exposicion de APIs nativas via APIM", "Native API exposure through APIM"),
    ("Modernizacion de componentes obsoletos", "Modernization of obsolete components"),
    ("Verificacion de paridad funcional", "Functional parity verification"),
    ("Tambien llamado Replace. Crear una version nueva de la aplicacion desde cero empleando frameworks y herramientas de desarrollo modernos, o sustituir la aplicacion existente por un COTS nuevo, con frecuencia SaaS.",
     "Also called Replace. Build a new version of the application from scratch with modern frameworks and development tools, or replace the existing application with a new COTS product, often SaaS."),
    ("Construir desde cero con estandares actuales alcanza todas las palancas. Es tambien el roadmap mas caro y mas largo: el techo del score no justifica por si solo la decision.",
     "Building from scratch to current standards reaches every lever. It is also the most expensive and longest roadmap: the score ceiling alone does not justify the decision."),
    ("Evaluacion construir contra comprar", "Build versus buy assessment"),
    ("Diseno de la solucion objetivo", "Target solution design"),
    ("Construccion del nucleo funcional", "Build of the core functionality"),
    ("Migracion de datos historicos", "Historical data migration"),
    ("Operacion en paralelo", "Parallel operation"),
    ("Baja de la aplicacion anterior", "Decommissioning of the previous application"),
    ("Dar de baja aplicaciones que se han vuelto obsoletas, redundantes, o que ya no aportan valor de negocio suficiente.",
     "Decommission applications that have become obsolete or redundant, or that no longer deliver enough business value."),
    ("La aplicacion sale del portafolio. Su score deja de existir, no mejora: invertir en subirlo seria gastar en algo que se va a apagar.",
     "The application leaves the portfolio. Its score stops existing rather than improving: investing to raise it would mean spending on something about to be switched off."),
    ("Confirmacion de ausencia de uso activo", "Confirmation that there is no active use"),
    ("Notificacion a consumidores e integraciones", "Notification to consumers and integrations"),
    ("Archivado de datos segun retencion legal", "Data archiving under legal retention"),
    ("Apagado y desmantelamiento", "Shutdown and dismantling"),
    ("Baja del inventario EAI", "Removal from the EAI inventory"),
    ("La funcionalidad sigue siendo necesaria y se consolida dentro de otra aplicacion.",
     "The functionality is still needed and is consolidated into another application."),
    ("La aplicacion sale del portafolio y su funcionalidad vive en otra. El score que importa despues es el de la aplicacion receptora.",
     "The application leaves the portfolio and its functionality lives on in another one. The score that matters afterwards is the receiving application's."),
    ("Identificacion de la aplicacion receptora", "Identification of the receiving application"),
    ("Analisis de brecha funcional", "Functional gap analysis"),
    ("Incorporacion de la funcionalidad faltante", "Addition of the missing functionality"),
    ("Migracion de usuarios y datos", "User and data migration"),
    ("Baja de la aplicacion origen", "Decommissioning of the source application"),
    ("Equipo de aplicacion", "Application team"),
    ("Equipo receptor", "Receiving team"),
    ("Arquitectura", "Architecture"),
    ("Plataforma", "Platform"),
    ("Datos", "Data"),
    ("healthyScore usa el piso del tramo Solido que muestra la interfaz, para que la alerta hable el mismo idioma que el resto de la aplicacion.",
     "healthyScore uses the floor of the Solid band shown by the interface, so the alert speaks the same language as the rest of the application."),
    ("Marcada para salir del portafolio, pero con productos en Invest", "Marked to leave the portfolio, but with products in Invest"),
    ("La disposition dice retirar la aplicacion y la estrategia de producto dice invertir en sus componentes. Una de las dos esta desactualizada.",
     "The disposition says to retire the application and the product strategy says to invest in its components. One of the two is out of date."),
    ("Tolerate con vulnerabilidades criticas abiertas", "Tolerate with open critical vulnerabilities"),
    ("Tolerar no es lo mismo que aceptar riesgo critico. Maintain (Patch) existe justamente para esto: la aplicacion deberia estar en ese roadmap, o los flaws deberian estar remediados.",
     "Tolerating is not the same as accepting critical risk. Maintain (Patch) exists precisely for this: the application should be on that roadmap, or the flaws should be remediated."),
    ("Aplicacion critica fuera del plan de DR", "Critical application outside the DR plan"),
    ("Esta marcada como critica para el negocio pero no esta en el alcance de recuperacion ante desastres.",
     "It is marked as business-critical but it is not in disaster recovery scope."),
    ("Se va del portafolio con una base de usuarios significativa", "Leaving the portfolio with a significant user base"),
    ("Retirar o consolidar una aplicacion con esta cantidad de usuarios exige un plan de migracion explicito antes de fijar la fecha de baja.",
     "Retiring or consolidating an application with this many users requires an explicit migration plan before the decommission date is set."),
    ("Reimagine sobre una aplicacion que ya esta sana", "Reimagine on an application that is already healthy"),
    ("Reconstruir desde cero es el camino mas caro y mas largo. Con un score ya solido, la decision deberia justificarse por necesidad funcional, no por deuda tecnica.",
     "Rebuilding from scratch is the most expensive and longest path. With a score that is already solid, the decision should be justified by functional need, not by technical debt."),
    ("Disposition sin roadmap asignado", "Disposition without an assigned roadmap"),
    ("La aplicacion tiene disposition pero no roadmap, asi que no hay plan futuro que proyectar.",
     "The application has a disposition but no roadmap, so there is no future plan to project."),
    ("El roadmap no corresponde a su disposition", "The roadmap does not match its disposition"),
    ("El par disposition/roadmap no es uno de los siete que define MAPS. El dato de origen esta mal.",
     "The disposition/roadmap pair is not one of the seven that MAPS defines. The source data is wrong."),
    ("Rehost no resuelve la ausencia de MFA", "Rehost does not fix missing MFA"),
    ("Migrar de hosting no altera la autenticacion. La aplicacion tiene usuarios y no usa multifactor: eso sigue igual despues del rehost.",
     "Changing hosting does not change authentication. The application has users and does not use multi-factor: that stays the same after the rehost."),
])

translate("contracts/ruleset-v1.3.json", [
    ("Modernization Score v1.3. Pesos y bandas extraidos del BRD 'Modernization Scoring Tool' y verificados contra el dashboard Power BI (EAI 10969 = 79.61, EAI 14381 = 79.67). Los rulesets son inmutables: cualquier cambio publica una version nueva.",
     "Modernization Score v1.3. Weights and bands taken from the BRD 'Modernization Scoring Tool' and verified against the Power BI dashboard (EAI 10969 = 79.61, EAI 14381 = 79.67). Rulesets are immutable: any change publishes a new version."),
    ("Riesgo de seguridad del host, derivado de escaneos de vulnerabilidad Qualys.", "Host security risk, derived from Qualys vulnerability scans."),
    ("RFC-05: por defecto se evalua presencia de al menos un flaw abierto, no el conteo.", "RFC-05: by default the presence of at least one open flaw is evaluated, not the count."),
    ("BRD: 'Medium (removed from score)'. Peso 0, se conserva para mostrar.", "BRD: 'Medium (removed from score)'. Weight 0, kept for display."),
    ("BRD: 'Low (removed from score)'. Peso 0, se conserva para mostrar.", "BRD: 'Low (removed from score)'. Weight 0, kept for display."),
    ("Solo penaliza la ausencia de MFA cuando la aplicacion tiene usuarios.", "Missing MFA is only penalized when the application has users."),
    ("Calculado a nivel de componente segun su fecha de fin de vida.", "Calculated at component level from each component's end-of-life date."),
    ("promedio ponderado por conteo de productos", "average weighted by product count"),
    ("RFC-01: si CMT entrega el compuesto ya calculado, ese valor gana sobre los conteos.", "RFC-01: if CMT delivers the composite already calculated, that value wins over the counts."),
    ("sin deuda", "no debt"),
    ("0,1-2,4", "0.1-2.4"),
    ("2,5-5", "2.5-5"),
    ("RFC-03: el BRD indica peso 5, el dashboard v1.3 indica 10. Se usa 10, que reproduce los valores publicados.", "RFC-03: the BRD states weight 5 and the v1.3 dashboard states 10. 10 is used because it reproduces the published values."),
    ("BRD: 'Removed from calculation'. Peso 0.", "BRD: 'Removed from calculation'. Weight 0."),
    ("BRD: tachado en la tabla de pesos. 'No' no puntua (NULL).", "BRD: struck through in the weights table. 'No' does not score (NULL)."),
    ("BRD: 'Currently removed from calculation'. Peso 0, pero mapeada: alimenta la seccion de plan futuro.", "BRD: 'Currently removed from calculation'. Weight 0, but mapped: it feeds the future plan section."),
    ("Basta una capacidad habilitada (Elastic Agent, Synthetics o APM) para puntuar 100.", "A single enabled capability (Elastic Agent, Synthetics or APM) is enough to score 100."),
    ("0-59,9", "0-59.9"),
    ("60-79,99", "60-79.99"),
])

translate("contracts/sources/source-schemas.json", [
    ("Contratos de columna de cada archivo fuente, transcritos de los exports reales. El adaptador de archivos valida contra esto antes de leer una sola fila: una columna que falta o cambia de nombre falla con un error accionable en lugar de producir un score silenciosamente incorrecto.",
     "Column contracts for each source file, transcribed from the real exports. The file adapter validates against them before reading a single row: a missing or renamed column fails with an actionable error instead of producing a silently wrong score."),
    ("Export del inventario EAI. Es la espina dorsal del portafolio: una aplicacion que no esta aqui no existe.",
     "EAI inventory export. It is the backbone of the portfolio: an application that is not here does not exist."),
    ("Composicion arquitectonica por aplicacion, exportada desde CMT.", "Architectural composition per application, exported from CMT."),
    ("Conteo de productos por estrategia, exportado desde MTS.", "Product count by strategy, exported from MTS."),
    ("Deuda tecnica por APP_ID, no por EAI. El ensamblador resuelve la correspondencia.", "Technical debt by APP_ID, not by EAI. The assembler resolves the mapping."),
    ("Una fila por vulnerabilidad. Una aplicacion presente aqui se considera escaneada; una ausente, no (ADR 0003).",
     "One row per vulnerability. An application present here is considered scanned; an absent one is not (ADR 0003)."),
    ("Open-Source Score por aplicacion, exportado desde el Risk Profile Tool.", "Open-Source Score per application, exported from the Risk Profile Tool."),
    ("Capacidades de monitoreo por aplicacion. Booleanos como TRUE/FALSE.", "Monitoring capabilities per application. Booleans as TRUE/FALSE."),
    ("Aplicaciones incorporadas a gestion de APIs. Sin fila, la exposicion queda sin dato.", "Applications onboarded to API management. Without a row, exposure has no data."),
])
