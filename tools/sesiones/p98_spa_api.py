from pathlib import Path
from patchlib import patch, ROOT

# ---------------------------------------------------------------- types.ts (append)
types = ROOT / r"src\web\src\api\types.ts"
t = types.read_text(encoding="utf-8", newline="")
nl = "\r\n" if "\r\n" in t else "\n"
block = """
// ------------------------------------------------------------------ pantalla Code

/**
 * La pantalla Code (ADR 0026): obsolescencia y riesgo segun lo que declaran los
 * repositorios, cruzado con las fechas de fin de vida de endoflife.date.
 */
export interface CodeView {
  rulesetVersion: string;
  source: CodeSource;
  analysis: CodePortfolio;
  definitions: AnalyticsDocument;
}

export interface CodeApplicationView {
  rulesetVersion: string;
  source: CodeSource;
  detail: CodeApplicationDetail;
  definitions: AnalyticsDocument;
}

/** De donde sale lo que se ve. `connected` es false con datos de demostracion. */
export interface CodeSource {
  /** "Sample" (demostracion) o "None" (sin integracion). */
  provider: string;
  connected: boolean;
  scannedAt: string | null;
  catalog: string;
  catalogUrl: string;
  /** yyyy-mm-dd */
  catalogRetrievedOn: string;
  catalogProducts: number;
  catalogCycles: number;
}

export interface CodePortfolio {
  /** La fecha contra la que se evalua cada fin de vida, yyyy-mm-dd. */
  asOf: string;
  kpis: CodeKpis;
  statuses: readonly CodeStatusShare[];
  lifecycle: readonly CodeLifecycleRow[];
  libraries: readonly CodeLibraryRow[];
  riskLevels: readonly CodeLevelShare[];
  applications: readonly CodeApplication[];
}

export interface CodeKpis {
  applications: number;
  applicationsWithRepositories: number;
  repositories: number;
  components: number;
  eolComponents: number;
  eolApplications: number;
  approachingComponents: number;
  approachingApplications: number;
  unknownComponents: number;
  criticalAlerts: number;
  highAlerts: number;
  vulnerableApplications: number;
  flaggedLibraries: number;
  highRiskApplications: number;
  mismatchApplications: number;
}

/** eol, approaching, security, supported o unknown. */
export interface CodeStatusShare {
  status: string;
  components: number;
  applications: number;
}

export interface CodeLifecycleRow {
  product: string;
  productName: string;
  kind: string;
  cycle: string;
  cycleLabel: string | null;
  eol: string | null;
  supportEnds: string | null;
  extendedSupportEnds: string | null;
  discontinued: boolean;
  status: string;
  monthsToEol: number | null;
  repositories: number;
  applications: number;
  sourceUrl: string | null;
}

export interface CodeLibraryRow {
  ecosystem: string;
  name: string;
  versions: readonly string[];
  latest: string | null;
  majorsBehind: number | null;
  deprecated: boolean;
  outdated: boolean;
  critical: number;
  high: number;
  repositories: number;
  applications: number;
}

/** critical, high, moderate, low o none (sin repositorios). */
export interface CodeLevelShare {
  level: string;
  applications: number;
  share: number;
}

export interface CodeApplication {
  eaiCode: string;
  applicationName: string;
  lob: string | null;
  country: string | null;
  critical: boolean;
  currency: number | null;
  legacy: boolean;
  repositories: number;
  components: number;
  eolComponents: number;
  approachingComponents: number;
  unknownComponents: number;
  criticalAlerts: number;
  highAlerts: number;
  deprecatedLibraries: number;
  outdatedLibraries: number;
  staleRepositories: number;
  riskIndex: number | null;
  riskLevel: string | null;
  worstStatus: string | null;
  /** hiddenObsolescence o inventoryBehind: el codigo y CMT no cuentan lo mismo. */
  mismatch: string | null;
}

export interface CodeApplicationDetail {
  application: CodeApplication;
  factors: readonly CodeRiskFactor[];
  repositories: readonly CodeRepository[];
}

export interface CodeRiskFactor {
  key: string;
  count: number;
  points: number;
  total: number;
}

export interface CodeRepository {
  name: string;
  defaultBranch: string;
  primaryLanguage: string | null;
  lastCommit: string | null;
  archived: boolean;
  stale: boolean;
  components: readonly CodeComponent[];
  dependencies: readonly CodeDependency[];
}

export interface CodeComponent {
  product: string;
  productName: string;
  kind: string;
  version: string;
  detectedIn: string;
  cycle: string | null;
  cycleLabel: string | null;
  eol: string | null;
  supportEnds: string | null;
  extendedSupportEnds: string | null;
  monthsToEol: number | null;
  status: string;
  sourceUrl: string | null;
}

export interface CodeDependency {
  ecosystem: string;
  name: string;
  version: string;
  latest: string | null;
  majorsBehind: number | null;
  direct: boolean;
  deprecated: boolean;
  outdated: boolean;
  critical: number;
  high: number;
  medium: number;
  low: number;
}
"""
t = t.rstrip("\r\n") + nl + block.replace("\n", nl)
types.write_text(t, encoding="utf-8", newline="")
print("ok types")

patch(r"src\web\src\api\client.ts", [
("""import type {
  ExplainView,""",
"""import type {
  CodeApplicationView,
  CodeView,
  ExplainView,"""),
("""  whatIf: (eaiCode: string) =>
    request<WhatIfLabView>(`/api/applications/${encodeURIComponent(eaiCode)}/whatif`),""",
"""  /** La pantalla Code sobre el mismo recorte que el resto (ADR 0026). */
  code: (filters: PortfolioFilters = {}) => request<CodeView>(`/api/code${withQuery(buildFilterQuery(filters))}`),

  /** Los repositorios, componentes y dependencias de una aplicacion. */
  codeApplication: (eaiCode: string) =>
    request<CodeApplicationView>(`/api/code/applications/${encodeURIComponent(eaiCode)}`),

  whatIf: (eaiCode: string) =>
    request<WhatIfLabView>(`/api/applications/${encodeURIComponent(eaiCode)}/whatif`),"""),
])

patch(r"src\web\src\api\hooks.ts", [
("""export function useWhatIfLab(eaiCode: string) {""",
"""/** La pantalla Code, con el filtro de la barra. */
export function useCode(filters: PortfolioFilters = {}) {
  return useQuery({
    queryKey: ['code', filters],
    queryFn: () => api.code(filters),
    placeholderData: (previous) => previous,
  });
}

/** Los repositorios de una aplicacion; sin codigo, no pregunta. */
export function useCodeApplication(eaiCode: string | null) {
  return useQuery({
    queryKey: ['code', 'application', eaiCode],
    queryFn: () => api.codeApplication(eaiCode ?? ''),
    enabled: eaiCode !== null && eaiCode.length > 0,
  });
}

export function useWhatIfLab(eaiCode: string) {"""),
])
