import type {
  ExplainView,
  LeverSelection,
  InsightsView,
  LineageView,
  PortfolioFilters,
  PortfolioView,
  HistoryPointView,
  IngestionView,
  PortfolioRoadmapView,
  RulesAdminView,
  RulesCompareView,
  RulesDraft,
  RulesPublishView,
  RulesSimulationView,
  RulesTimelineView,
  RulesVersionDocumentView,
  RoadmapPlanView,
  UploadResultView,
  ScenarioView,
  ScoreOverviewView,
  SessionView,
  TargetView,
  WhatIfLabView,
} from './types';

/**
 * Cliente del channel-api.
 *
 * Patron BFF: la sesion viaja en una cookie httpOnly que este codigo nunca ve ni
 * puede leer. Por eso todas las peticiones van con `credentials: 'same-origin'`
 * y no hay ningun token en memoria ni en localStorage.
 */

const CSRF_COOKIE = '__Host-modscore.csrf';
const CSRF_HEADER = 'X-CSRF-Token';

/** Prefijo del `type` estable de los ProblemDetails del backend. */
const PROBLEM_TYPE_PREFIX = 'urn:modscore:problem:';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly title: string,
    detail: string,
    /** Codigo estable del problema (`invalid-filter`), vacio si la respuesta no lo trae. */
    readonly code = '',
  ) {
    super(detail);
    this.name = 'ApiError';
  }
}

/** Lee el token CSRF de la cookie de doble envio. La de sesion sigue siendo httpOnly. */
function readCsrfToken(): string | null {
  const match = document.cookie
    .split('; ')
    .find((entry) => entry.startsWith(`${CSRF_COOKIE}=`));

  return match ? decodeURIComponent(match.slice(CSRF_COOKIE.length + 1)) : null;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method ?? 'GET';
  const headers = new Headers(init?.headers);
  headers.set('Accept', 'application/json');

  if (method !== 'GET' && method !== 'HEAD') {
    const csrf = readCsrfToken();
    if (csrf) {
      headers.set(CSRF_HEADER, csrf);
    }
  }

  const response = await fetch(path, {
    ...init,
    method,
    headers,
    credentials: 'same-origin',
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function toApiError(response: Response): Promise<ApiError> {
  let title = response.statusText || 'Error';
  let detail = 'The request could not be completed.';
  let code = '';

  try {
    const problem = (await response.json()) as { title?: string; detail?: string; type?: string };
    title = problem.title ?? title;
    detail = problem.detail ?? detail;
    code = problem.type?.startsWith(PROBLEM_TYPE_PREFIX)
      ? problem.type.slice(PROBLEM_TYPE_PREFIX.length)
      : '';
  } catch {
    // Una respuesta sin cuerpo JSON no es un fallo adicional: se usa el estado.
  }

  if (response.status === 401) {
    title = 'Not signed in';
    detail = 'Sign in to see the portfolio.';
  }

  return new ApiError(response.status, title, detail, code);
}

/** Los filtros, sin paginacion: las pantallas que agregan filtran igual pero no paginan. */
function buildFilterQuery(filters: PortfolioFilters): URLSearchParams {
  const params = new URLSearchParams();

  if (filters.lob) params.set('lob', filters.lob);
  if (filters.country) params.set('country', filters.country);
  if (filters.cio) params.set('cio', filters.cio);
  if (filters.disposition) params.set('disposition', filters.disposition);
  if (filters.roadmap) params.set('roadmap', filters.roadmap);
  if (filters.band) params.set('band', filters.band);
  if (filters.criticalOnly) params.set('criticalOnly', 'true');

  return params;
}

function buildQuery(filters: PortfolioFilters): string {
  const params = buildFilterQuery(filters);
  params.set('page', String(filters.page ?? 1));
  params.set('pageSize', String(filters.pageSize ?? 25));

  return params.toString();
}

/** Una query string con '?' delante, o vacia si no hay nada que preguntar. */
function withQuery(params: URLSearchParams): string {
  const query = params.toString();
  return query ? `?${query}` : '';
}

export const api = {
  session: () => request<SessionView>('/api/session'),

  login: () => request<void>('/api/login'),

  logout: () => request<void>('/api/logout', { method: 'POST' }),

  portfolio: (filters: PortfolioFilters) =>
    request<PortfolioView>(`/api/portfolio?${buildQuery(filters)}`),

  overview: (eaiCode: string) =>
    request<ScoreOverviewView>(`/api/applications/${encodeURIComponent(eaiCode)}/overview`),

  explain: (eaiCode: string) =>
    request<ExplainView>(`/api/applications/${encodeURIComponent(eaiCode)}/explain`),

  lineage: (version?: string) =>
    request<LineageView>(
      `/api/lineage${version ? `?version=${encodeURIComponent(version)}` : ''}`,
    ),

  ingestion: () => request<IngestionView>('/api/ingestion'),

  history: (eaiCode: string) =>
    request<readonly HistoryPointView[]>(
      `/api/applications/${encodeURIComponent(eaiCode)}/history`,
    ),

  /**
   * Carga de archivos. El body es FormData, asi que no se fija Content-Type: el
   * navegador lo escribe con el boundary correcto. El token CSRF lo agrega
   * `request` como en cualquier otra mutacion.
   */
  upload: (files: readonly File[], label: string, assignments?: readonly string[]) => {
    const body = new FormData();
    for (const file of files) {
      body.append('files', file, file.name);
    }

    // Una asignacion por archivo y en su mismo orden: el servidor las empareja por
    // posicion, asi que se envian todas o ninguna.
    if (assignments && assignments.length === files.length) {
      for (const assignment of assignments) {
        body.append('assign', assignment || '-');
      }
    }

    if (label.trim()) {
      body.append('label', label.trim());
    }

    return request<UploadResultView>('/api/ingestion/upload', { method: 'POST', body });
  },

  roadmap: (eaiCode: string) =>
    request<RoadmapPlanView>(`/api/applications/${encodeURIComponent(eaiCode)}/roadmap`),

  portfolioRoadmap: (filters: PortfolioFilters = {}, groupBy?: string) => {
    const params = buildFilterQuery(filters);
    if (groupBy) params.set('groupBy', groupBy);

    return request<PortfolioRoadmapView>(`/api/roadmap${withQuery(params)}`);
  },

  /**
   * Las seis preguntas del portafolio. Una sola llamada: la pantalla no compone
   * seis respuestas ni calcula nada, solo pinta lo que llega.
   */
  insights: (filters: PortfolioFilters = {}, options: { duplicates?: number; top?: number } = {}) => {
    const params = buildFilterQuery(filters);
    if (options.duplicates) params.set('duplicates', String(options.duplicates));
    if (options.top) params.set('top', String(options.top));

    return request<InsightsView>(`/api/insights${withQuery(params)}`);
  },

  whatIf: (eaiCode: string) =>
    request<WhatIfLabView>(`/api/applications/${encodeURIComponent(eaiCode)}/whatif`),

  // Los POST llevan el token CSRF automaticamente: lo agrega `request`.
  scenario: (eaiCode: string, levers: readonly LeverSelection[]) =>
    request<ScenarioView>(`/api/applications/${encodeURIComponent(eaiCode)}/whatif/scenario`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ levers }),
    }),

  target: (eaiCode: string, targetScore: number) =>
    request<TargetView>(`/api/applications/${encodeURIComponent(eaiCode)}/whatif/target`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ targetScore }),
    }),

  // ------------------------------------------------------ reglas (Fase 12)

  rules: () => request<RulesAdminView>('/api/admin/rules'),

  rulesVersion: (id: string) =>
    request<RulesVersionDocumentView>(`/api/admin/rules/versions/${encodeURIComponent(id)}`),

  /** Con `eaiCode` la simulacion trae ademas esa aplicacion metrica por metrica. */
  simulateRules: (basedOn: string, draft: RulesDraft, eaiCode?: string) =>
    request<RulesSimulationView>('/api/admin/rules/simulate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(eaiCode ? { basedOn, draft, eaiCode } : { basedOn, draft }),
    }),

  publishRules: (basedOn: string, reason: string, draft: RulesDraft) =>
    request<RulesPublishView>('/api/admin/rules/publish', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ basedOn, reason, draft }),
    }),

  // --------------------------------------- comparacion y linea de tiempo

  compareRules: (from: string, to: string, eaiCode?: string) => {
    const params = new URLSearchParams({ from, to });
    if (eaiCode) params.set('eai', eaiCode);

    return request<RulesCompareView>(`/api/admin/rules/compare?${params.toString()}`);
  },

  rulesTimeline: () => request<RulesTimelineView>('/api/rules/timeline'),
};

/**
 * Descargas en CSV. Son enlaces con `download`, no fetch: la cookie de sesion viaja sola
 * por ser el mismo origen, un GET no lleva token CSRF y el navegador guarda el archivo
 * con el nombre que da el servidor.
 */
export const exportUrls = {
  portfolio: (filters: PortfolioFilters) =>
    `/api/portfolio/export.csv${withQuery(buildFilterQuery(filters))}`,

  roadmap: (filters: PortfolioFilters, groupBy: string) => {
    const params = buildFilterQuery(filters);
    params.set('groupBy', groupBy);

    return `/api/roadmap/export.csv?${params.toString()}`;
  },

  rulesComparison: (from: string, to: string) =>
    `/api/admin/rules/compare/export.csv?${new URLSearchParams({ from, to }).toString()}`,
};

/**
 * Reautenticacion antes de publicar reglas. Es una navegacion, no un fetch: con OIDC
 * el navegador tiene que ir al IdP y volver. La vuelta la fija el servidor.
 */
export const STEP_UP_URL = '/api/login/step-up?returnTo=%2Fadmin%2Frules';
