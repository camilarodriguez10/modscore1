"""Fase 12: tipos, cliente, hooks, ruta y memoria de filtros de la SPA."""
import io

WEB = "C:/Users/kmiro/Downloads/mod score/src/web/"


def patch(rel, pairs):
    path = WEB + rel
    raw = io.open(path, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        assert s.count(old) == 1, (rel, old[:70], s.count(old))
        s = s.replace(old, new)
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print("ok", rel)


TYPES = r"""
// ---------------------------------------------------------- 09 Scoring rules

/** Lo esencial del impacto de una version, guardado con ella. */
export interface RulesImpactSummary {
  applications: number;
  up: number;
  down: number;
  unchanged: number;
  averageBefore: number | null;
  averageAfter: number | null;
  averageDelta: number | null;
}

export interface RulesVersionView {
  id: string;
  basedOn: string | null;
  publishedAt: string | null;
  publishedBy: string;
  reason: string;
  isActive: boolean;
  isBuiltIn: boolean;
  changes: readonly Notice[];
  impact: RulesImpactSummary | null;
}

export interface BandRule {
  min: number;
  max: number;
  score: number;
  label: string;
}

export type MappingKind = 'numericBands' | 'categorical' | 'identity';

export interface RulesPillar {
  key: string;
  name: string;
  nominalWeight: number;
}

export interface RulesMetric {
  key: string;
  name: string;
  pillar: string;
  weight: number;
  kind: MappingKind;
  bands: readonly BandRule[] | null;
  cases: Readonly<Record<string, number>> | null;
  categoryScores: Readonly<Record<string, number>> | null;
  sourceSystem: string;
  note: string | null;
}

export interface RulesLever {
  key: string;
  name: string;
  action: string;
  metricKey: string;
  pillarKey: string;
  enabled: boolean;
  isParameterized: boolean;
  defaultParameter: number | null;
  /** Roadmaps que entregan esta palanca, por clave. */
  usedBy: readonly string[];
}

export interface RulesMilestone {
  name: string;
  owner: string;
  weeks: number;
}

export interface RulesRoadmapPath {
  key: string;
  name: string;
  disposition: string;
  removesFromPortfolio: boolean;
  levers: readonly string[];
  milestones: readonly RulesMilestone[];
}

export interface RulesRoadmap {
  healthyScore: number;
  significantUserBase: number;
  roadmaps: readonly RulesRoadmapPath[];
}

/** Un juego de reglas en la forma en que se edita. */
export interface RulesDocument {
  version: string;
  floor: number;
  range: number;
  pillars: readonly RulesPillar[];
  metrics: readonly RulesMetric[];
  levers: readonly RulesLever[];
  roadmap: RulesRoadmap;
}

export interface RulesLimitsView {
  totalWeight: number;
  maxBands: number;
  maxBandLabelLength: number;
  minReasonLength: number;
  maxReasonLength: number;
  maxWeeks: number;
  maxLeverParameter: number;
}

export interface RulesAdminView {
  active: RulesVersionView;
  versions: readonly RulesVersionView[];
  document: RulesDocument;
  store: string;
  /** Si la sesion puede publicar ahora, sin volver a autenticarse. */
  reauthenticated: boolean;
  reauthenticationMinutes: number;
  limits: RulesLimitsView;
}

export interface RulesVersionDocumentView {
  version: RulesVersionView;
  document: RulesDocument;
}

export interface BandShift {
  band: string;
  before: number;
  after: number;
}

export interface ApplicationShift {
  eaiCode: string;
  applicationName: string;
  before: number | null;
  after: number | null;
  delta: number | null;
  bandBefore: string | null;
  bandAfter: string | null;
}

export interface RulesImpact {
  applications: number;
  up: number;
  down: number;
  unchanged: number;
  noLongerScored: number;
  newlyScored: number;
  averageBefore: number | null;
  averageAfter: number | null;
  averageDelta: number | null;
  bands: readonly BandShift[];
  movers: readonly ApplicationShift[];
  projectedBefore: number | null;
  projectedAfter: number | null;
  alertsBefore: number;
  alertsAfter: number;
}

export interface RulesSimulationView {
  valid: boolean;
  stale: boolean;
  activeVersion: string;
  errors: readonly Notice[];
  changes: readonly Notice[];
  impact: RulesImpact | null;
}

export interface RulesPublishView {
  published: boolean;
  version: RulesVersionView | null;
  errors: readonly Notice[];
  changes: readonly Notice[];
  impact: RulesImpact | null;
}

/** Lo que la pantalla envia: solo lo editable. */
export interface RulesDraft {
  metrics: readonly MetricDraft[];
  levers: readonly LeverDraft[];
  roadmap: RoadmapDraft;
}

export interface MetricDraft {
  key: string;
  weight: number;
  bands: readonly BandRule[] | null;
  cases: Readonly<Record<string, number>> | null;
  categoryScores: Readonly<Record<string, number>> | null;
}

export interface LeverDraft {
  key: string;
  enabled: boolean;
  defaultParameter: number | null;
}

export interface RoadmapDraft {
  healthyScore: number;
  significantUserBase: number;
  roadmaps: readonly RoadmapPathDraft[];
}

export interface RoadmapPathDraft {
  key: string;
  levers: readonly string[];
  milestoneWeeks: readonly number[];
}
"""

patch("src/api/types.ts", [
    ("  canUpload: boolean;\n}", "  canUpload: boolean;\n  canAdministerRules: boolean;\n}"),
    ("  candidates: readonly string[];\n}\n", "  candidates: readonly string[];\n}\n" + TYPES),
])

CLIENT_METHODS = """      body: JSON.stringify({ targetScore }),
    }),

  // ------------------------------------------------------ reglas (Fase 12)

  rules: () => request<RulesAdminView>('/api/admin/rules'),

  rulesVersion: (id: string) =>
    request<RulesVersionDocumentView>(`/api/admin/rules/versions/${encodeURIComponent(id)}`),

  simulateRules: (basedOn: string, draft: RulesDraft) =>
    request<RulesSimulationView>('/api/admin/rules/simulate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ basedOn, draft }),
    }),

  publishRules: (basedOn: string, reason: string, draft: RulesDraft) =>
    request<RulesPublishView>('/api/admin/rules/publish', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ basedOn, reason, draft }),
    }),
};

/**
 * Reautenticacion antes de publicar reglas. Es una navegacion, no un fetch: con OIDC
 * el navegador tiene que ir al IdP y volver. La vuelta la fija el servidor.
 */
export const STEP_UP_URL = '/api/login/step-up?returnTo=%2Fadmin%2Frules';"""

patch("src/api/client.ts", [
    ("  PortfolioRoadmapView,\n",
     "  PortfolioRoadmapView,\n  RulesAdminView,\n  RulesDraft,\n  RulesPublishView,\n  RulesSimulationView,\n  RulesVersionDocumentView,\n"),
    ("      body: JSON.stringify({ targetScore }),\n    }),\n};", CLIENT_METHODS),
])

HOOKS = """export function useTarget(eaiCode: string) {
  return useMutation({
    mutationFn: (targetScore: number) => api.target(eaiCode, targetScore),
  });
}

// ------------------------------------------------------------- reglas

export function useRules() {
  return useQuery({ queryKey: ['rules'], queryFn: api.rules, retry: false });
}

/** Una version anterior, pedida en el momento de restaurarla. */
export function useRulesVersion() {
  return useMutation({ mutationFn: (id: string) => api.rulesVersion(id) });
}

/** Simular no cambia nada, pero lleva cuerpo y va por POST: es una mutacion. */
export function useSimulateRules() {
  return useMutation({
    mutationFn: ({ basedOn, draft }: { basedOn: string; draft: RulesDraft }) =>
      api.simulateRules(basedOn, draft),
  });
}

/**
 * Publicar reglas cambia el score de todo el portafolio: todo lo que haya en cache se
 * calculo con las reglas anteriores.
 */
export function usePublishRules() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ basedOn, reason, draft }: { basedOn: string; reason: string; draft: RulesDraft }) =>
      api.publishRules(basedOn, reason, draft),
    onSuccess: (result) => {
      if (result.published) {
        queryClient.invalidateQueries();
      }
    },
  });
}
"""

patch("src/api/hooks.ts", [
    ("import type { LeverSelection, PortfolioFilters } from './types';",
     "import type { LeverSelection, PortfolioFilters, RulesDraft } from './types';"),
    ("export function useTarget(eaiCode: string) {\n  return useMutation({\n    mutationFn: (targetScore: number) => api.target(eaiCode, targetScore),\n  });\n}\n",
     HOOKS),
])

patch("src/App.tsx", [
    ("import { DataPage } from './routes/DataPage';",
     "import { DataPage } from './routes/DataPage';\nimport { RulesPage } from './routes/RulesPage';"),
    ('        <Route path="data" element={<DataPage />} />',
     '        <Route path="data" element={<DataPage />} />\n        <Route path="admin/rules" element={<RulesPage />} />'),
    ("  return <Workspace name={session.data.name} />;",
     "  return (\n    <Workspace\n      name={session.data.name}\n      canAdministerRules={session.data.permissions.canAdministerRules}\n    />\n  );"),
    ("function Workspace({ name }: { name: string }) {",
     "function Workspace({ name, canAdministerRules }: { name: string; canAdministerRules: boolean }) {"),
    ('            <Tab to="/data" search={search}>\n              Data\n            </Tab>\n',
     '            <Tab to="/data" search={search}>\n              Data\n            </Tab>\n'
     "            {/* Solo a quien administra reglas. El servidor lo exige igual: esto solo\n"
     "                evita ofrecer una pantalla que responderia 403. */}\n"
     "            {canAdministerRules && (\n"
     '              <Tab to="/admin/rules" search="">\n'
     "                Rules\n"
     "              </Tab>\n"
     "            )}\n"),
    ("        {FILTERED_ROUTES.has(pathname) ? <FilterBar /> : <ActiveFilters />}",
     "        {/* Las reglas son de todo el portafolio: un filtro ahi no acota nada. */}\n"
     "        {FILTERED_ROUTES.has(pathname) ? (\n"
     "          <FilterBar />\n"
     "        ) : pathname.startsWith('/admin') ? null : (\n"
     "          <ActiveFilters />\n"
     "        )}"),
])

patch("src/filters.tsx", [
    ("import { Link, useNavigationType, useSearchParams } from 'react-router';",
     "import { Link, useLocation, useNavigationType, useSearchParams } from 'react-router';"),
    ("  const navigation = useNavigationType();\n  const pageLoad = useRef(true);",
     "  const navigation = useNavigationType();\n  const { pathname } = useLocation();\n  const pageLoad = useRef(true);"),
    ("    const isPageLoad = pageLoad.current;\n    pageLoad.current = false;\n",
     "    const isPageLoad = pageLoad.current;\n    pageLoad.current = false;\n\n"
     "    // La administracion no filtra: restituir el filtro ahi solo ensuciaria la URL.\n"
     "    if (pathname.startsWith('/admin')) return;\n"),
    ("  }, [hasFilters, search, navigation, params, setParams]);",
     "  }, [hasFilters, search, navigation, pathname, params, setParams]);"),
])
