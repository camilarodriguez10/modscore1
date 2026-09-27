from patchlib import patch

patch(r"src\web\src\api\types.ts", [
("""export interface DashboardTrendPoint {
  versionId: string;""",
"""export interface DashboardTrendPoint {
  /** Currency Score promedio de CMT en esa carga. */
  currency: number | null;
  /** Risk Score promedio en esa carga (0-100, mas alto es peor). */
  riskScore: number | null;
  versionId: string;"""),
("""export interface CodeView {
  rulesetVersion: string;
  source: CodeSource;
  analysis: CodePortfolio;
  definitions: AnalyticsDocument;
}

export interface CodeApplicationView {
  rulesetVersion: string;
  /** La fecha contra la que se evalua cada fin de vida, yyyy-mm-dd. */
  asOf: string;
  source: CodeSource;
  detail: CodeApplicationDetail;
  definitions: AnalyticsDocument;
}""",
"""export interface CodeView {
  rulesetVersion: string;
  source: CodeSource;
  analysis: CodePortfolio;
  /** El portafolio filtrado en cada carga: score, Currency Score y Risk Score promedio. */
  trend: readonly DashboardTrendPoint[];
  definitions: AnalyticsDocument;
}

export interface CodeApplicationView {
  rulesetVersion: string;
  /** La fecha contra la que se evalua cada fin de vida, yyyy-mm-dd. */
  asOf: string;
  source: CodeSource;
  detail: CodeApplicationDetail;
  history: readonly CodeHistoryPoint[];
  definitions: AnalyticsDocument;
}

/** Una aplicacion en una carga. */
export interface CodeHistoryPoint {
  versionId: string;
  label: string;
  capturedAt: string;
  score: number | null;
  currency: number | null;
  riskScore: number | null;
}

/** El portafolio filtrado en un horizonte si nada cambia: 0 es hoy. */
export interface CodeHorizon {
  years: number;
  on: string;
  eolComponents: number;
  eolApplications: number;
  /** Parte de los componentes evaluables que sigue con soporte, 0-100. */
  currency: number | null;
  highRiskApplications: number;
  averageRiskIndex: number | null;
  statuses: readonly CodeStatusShare[];
}

export interface CodeApplicationHorizon {
  years: number;
  on: string;
  components: number;
  eolComponents: number;
  approachingComponents: number;
  currency: number | null;
  riskIndex: number | null;
  riskLevel: string | null;
}

/** Una mejora sugerida en un repositorio. */
export interface CodeSuggestion {
  /** upgrade, migrate, awaitRelease, updateLibrary, replaceLibrary, maintainRepository o retireRepository. */
  kind: string;
  /** now, year1, year2, year3 o planned. */
  urgency: string;
  repository: string;
  subject: string;
  ecosystem: string | null;
  current: string;
  targetSubject: string | null;
  target: string | null;
  deadline: string | null;
  targetEol: string | null;
  pointsNow: number;
  pointsAvoided: number;
}

/** Una accion del plan del portafolio. */
export interface CodeActionRow {
  kind: string;
  urgency: string;
  subject: string;
  ecosystem: string | null;
  current: string;
  targetSubject: string | null;
  target: string | null;
  deadline: string | null;
  targetEol: string | null;
  repositories: number;
  applications: number;
  pointsNow: number;
  pointsAvoided: number;
}"""),
("""  riskLevels: readonly CodeLevelShare[];
  applications: readonly CodeApplication[];
}""",
"""  riskLevels: readonly CodeLevelShare[];
  applications: readonly CodeApplication[];
  /** Hoy y en uno, dos y tres anos, si nada cambia. */
  outlook: readonly CodeHorizon[];
  /** El plan de mejora: las sugerencias de todas las aplicaciones, por accion. */
  actions: readonly CodeActionRow[];
}"""),
("""  /** hiddenObsolescence o inventoryBehind: el codigo y CMT no cuentan lo mismo. */
  mismatch: string | null;
}""",
"""  /** hiddenObsolescence o inventoryBehind: el codigo y CMT no cuentan lo mismo. */
  mismatch: string | null;
  /** Componentes vencidos dentro de tres anos si nada cambia. */
  projectedEolComponents: number;
  projectedRiskLevel: string | null;
}"""),
("""export interface CodeApplicationDetail {
  application: CodeApplication;
  factors: readonly CodeRiskFactor[];
  repositories: readonly CodeRepository[];
}""",
"""export interface CodeApplicationDetail {
  application: CodeApplication;
  factors: readonly CodeRiskFactor[];
  repositories: readonly CodeRepository[];
  outlook: readonly CodeApplicationHorizon[];
  suggestions: readonly CodeSuggestion[];
}"""),
])

patch(r"src\web\src\i18n\code.ts", [
("""/** Una fecha yyyy-mm-dd como se lee en el sitio, sin correrla por la zona horaria. */""",
"""export const URGENCY_KEYS = ['now', 'year1', 'year2', 'year3', 'planned'] as const;

const URGENCIES: Record<string, string> = {
  now: 'Now',
  year1: 'Within 1 year',
  year2: 'Within 2 years',
  year3: 'Within 3 years',
  planned: 'Plan it',
};

export const urgencyLabel = (urgency: string) => URGENCIES[urgency] ?? urgency;

/** El color de la urgencia: la misma paleta de estados que el fin de vida. */
export const urgencyStatus = (urgency: string) =>
  urgency === 'now' ? 'eol' : urgency === 'year1' ? 'approaching' : urgency === 'planned' ? 'unknown' : 'security';

/** Lo que hay que hacer, en una frase corta. */
export function actionText(action: {
  kind: string;
  subject: string;
  ecosystem: string | null;
  current: string;
  targetSubject: string | null;
  target: string | null;
}): string {
  const { kind, subject, current, targetSubject, target } = action;

  switch (kind) {
    case 'upgrade':
      return `Upgrade ${subject} ${current} → ${target ?? '?'}`;
    case 'migrate':
      return `Migrate ${subject} ${current} → ${targetSubject ?? ''} ${target ?? ''}`.trim();
    case 'awaitRelease':
      return `${subject} ${current}: no newer supported release yet`;
    case 'updateLibrary':
      return target ? `Update ${subject} ${current} → ${target}` : `Update ${subject} ${current}`;
    case 'replaceLibrary':
      return `Replace ${subject} ${current} (deprecated)`;
    case 'maintainRepository':
      return current ? `Resume maintenance: no commits since ${formatDay(current)}` : 'Resume maintenance';
    case 'retireRepository':
      return 'Archived: unmap it or confirm the application no longer uses it';
    default:
      return `${kind} ${subject}`;
  }
}

/** Una fecha yyyy-mm-dd como se lee en el sitio, sin correrla por la zona horaria. */"""),
])

patch(r"src\web\src\i18n\analytics.ts", [
("""  'code.topLibraries': { label: 'Libraries listed', help: 'How many libraries with findings the Code screen lists.' },""",
 """  'code.topLibraries': { label: 'Libraries listed', help: 'How many libraries with findings the Code screen lists.' },
  'code.topActions': { label: 'Improvement actions listed', help: 'How many actions the improvement plan of the Code screen lists.' },"""),
("""/**
 * La explicacion de un panel de la pantalla Code: 0 el indice y los indicadores, 1 el
 * ciclo de vida, 2 las bibliotecas, 3 las discrepancias con CMT.
 */""",
"""/**
 * La explicacion de un panel de la pantalla Code: 0 el indice y los indicadores, 1 el
 * ciclo de vida, 2 las bibliotecas, 3 las discrepancias con CMT, 4 la proyeccion a tres
 * anos, 5 la evolucion por carga y 6 el plan de mejora.
 */"""),
("""    case 3:
      return {
        area: 'code',
        considered: [
          'Currency Score: the score CMT delivers (Raw_Tech_Score, 0–100), declared for the application in the inventory.',
          CODE_MANIFESTS,
        ],""",
"""    case 4:
      return {
        area: 'code',
        considered: [CODE_MANIFESTS, CODE_CATALOG],
        rules: [
          'If nothing changes means the same versions, the same open advisories and no new commits. Only the calendar moves.',
          `Each horizon — today, in one, two and three years — evaluates every component against its end of life on that date, with the same ending-soon window of ${months}.`,
          'Code currency: the share of assessable components still within free security support. 100 is all supported; components not in the catalog are left out.',
          `The code risk index is recomputed on each date with the points in force; a repository crosses the stale threshold of ${d.text('code.staleMonths')} as time passes without commits.`,
        ],
        missing: [
          'Advisories that will be published in the future, and end-of-life dates vendors have not announced yet: a cycle without a date counts as supported.',
        ],
      };
    case 5:
      return {
        area: null,
        considered: [
          SCORE_MODEL,
          'Currency Score: the score CMT delivers (Raw_Tech_Score, 0–100) in each upload. A 0 is no data.',
          'Risk Score: the Modernization Risk Vectors pillar inverted to 0–100, higher is worse — the same as the dashboard.',
        ],
        rules: [
          'One point per upload, oldest first, with the filters on screen and the rules in force: a change of rules moves every point together.',
          'Each value is the average over the applications with that data in that upload.',
        ],
        missing: ['Code obsolescence per upload: repositories are not versioned with the uploads yet.'],
      };
    case 6:
      return {
        area: 'code',
        considered: [CODE_MANIFESTS, CODE_CATALOG],
        rules: [
          'Upgrade to the supported release cycle that lasts longest, preferring LTS where the product has it and skipping any that ends within the ending-soon window. With no supported cycle left, migrate to the product that replaces it (AngularJS → Angular).',
          'Urgency: now when the component is past its end of life, the library is deprecated or has critical or high advisories, or the repository is stale; within 1, 2 or 3 years by the end-of-life date; plan it for a library only behind on major versions.',
          `Points now: what the code risk index drops today when the action is applied. Avoided: what it would add when the component reaches its end of life (${d.text('code.points.eolComponent')} each).`,
          `The plan groups the same action across repositories and lists the ${d.plain('code.topActions')} most urgent. Each application shows its own list, including stale repositories.`,
        ],
        missing: ['Effort and cost of each action, and dependencies between upgrades (a framework that requires a newer runtime).'],
      };
    case 3:
      return {
        area: 'code',
        considered: [
          'Currency Score: the score CMT delivers (Raw_Tech_Score, 0–100), declared for the application in the inventory.',
          CODE_MANIFESTS,
        ],"""),
])

patch(r"src\web\src\routes\DefinitionsPage.tsx", [
("""      const parts = [0, 1, 2, 3].map((panel) => codeExplanation(panel, d));""",
 """      const parts = [0, 1, 4, 6, 2, 3].map((panel) => codeExplanation(panel, d));"""),
])
