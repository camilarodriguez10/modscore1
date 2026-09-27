/**
 * Modelos de vista que entrega el channel-api. Reflejan exactamente los records
 * de `Modernization.ChannelApi.ViewModels`; el plan contempla generarlos desde el
 * OpenAPI del channel-api para que dejen de mantenerse a mano.
 *
 * Ninguna regla de negocio vive aqui: el front pinta lo que ya viene calculado.
 */

export type PillarKey = 'risk' | 'architecture' | 'stability';

/** Tramos de score. Siempre se muestran con su nombre: el color nunca va solo. */
export type ScoreBand = 'critical' | 'at-risk' | 'acceptable' | 'solid';

export interface SessionView {
  name: string;
  email: string | null;
  roles: readonly string[];
  isAuthenticated: boolean;
  permissions: SessionPermissionsView;
}

/**
 * Lo que la sesion permite, resuelto por el servidor. Sirve para no ofrecer lo que
 * se va a negar; la autorizacion real sigue en el servidor.
 */
export interface SessionPermissionsView {
  canRead: boolean;
  fullPortfolio: boolean;
  canUpload: boolean;
  canAdministerRules: boolean;
}

/**
 * Un mensaje para personas como codigo y datos. El backend no redacta frases: la
 * interfaz las arma con `formatNotice` (ADR 0017).
 */
export interface Notice {
  code: string;
  args: Readonly<Record<string, string>>;
}

// -------------------------------------------------------------- 01 Portfolio

export interface PortfolioView {
  totalApplications: number;
  averageScore: number;
  medianScore: number;
  distribution: readonly ScoreBandView[];
  byLob: readonly GroupSummaryView[];
  byCountry: readonly GroupSummaryView[];
  byCio: readonly GroupSummaryView[];
  byDisposition: readonly GroupSummaryView[];
  byRoadmap: readonly GroupSummaryView[];
  byCre: readonly GroupSummaryView[];
  byRegion: readonly GroupSummaryView[];
  byStatus: readonly GroupSummaryView[];
  byCapability: readonly GroupSummaryView[];
  applications: readonly PortfolioRowView[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ScoreBandView {
  band: string;
  range: string;
  count: number;
  percent: number;
}

/**
 * Un agregado del portafolio. La clave es tambien el valor del filtro: al hacer
 * clic se envia esa misma clave, sin traducir nada por el camino.
 */
export interface GroupSummaryView {
  key: string;
  count: number;
  averageScore: number;
}

export interface PortfolioRowView {
  eaiCode: string;
  applicationName: string;
  lob: string | null;
  country: string | null;
  cio: string | null;
  cre: string | null;
  criticalApplication: boolean | null;
  disposition: string | null;
  roadmap: string | null;
  score: number;
  band: string;
  availableWeight: number;
}

// --------------------------------------------------------- 02 Score Overview

export interface ApplicationHeaderView {
  eaiCode: string;
  applicationName: string;
  lob: string | null;
  subLob: string | null;
  country: string | null;
  region: string | null;
  cio: string | null;
  cre: string | null;
  criticalApplication: boolean | null;
  disposition: string | null;
  roadmap: string | null;
  codingLanguage: string | null;
  userCount: number | null;
}

export interface ScoreOverviewView {
  application: ApplicationHeaderView;
  score: number;
  band: string;
  rulesetVersion: string;
  numerator: number;
  denominator: number;
  pillars: readonly PillarCardView[];
  override: OverrideView | null;
  calculatedAt: string;
}

export interface PillarCardView {
  key: string;
  name: string;
  score: number | null;
  totalWeight: number;
  availableWeight: number;
  numerator: number;
  metricsIncluded: number;
  metricsExcluded: number;
  metrics: readonly PillarMetricView[];
}

export interface PillarMetricView {
  metricKey: string;
  name: string;
  rawValue: Notice;
  mappedScore: number | null;
  weight: number;
  weightedScore: number | null;
  isIncluded: boolean;
}

export interface OverrideView {
  value: number;
  reason: string;
  appliedBy: string;
  appliedAt: string;
}

// -------------------------------------------------------- 03 Como se calculo

export interface ExplainView {
  application: ApplicationHeaderView;
  formula: FormulaView;
  waterfall: readonly WaterfallStepView[];
  included: readonly ExplainMetricView[];
  excluded: readonly ExcludedMetricView[];
  informational: readonly ExplainMetricView[];
  calculatedAt: string;
}

export interface FormulaView {
  expression: string;
  floor: number;
  range: number;
  numerator: number;
  denominator: number;
  score: number;
  rulesetVersion: string;
  steps: readonly Notice[];
}

export interface WaterfallStepView {
  metricKey: string;
  name: string;
  pillarKey: string;
  weightedScore: number;
  cumulative: number;
  pointsOfFinalScore: number;
}

export interface ProvenanceView {
  system: string;
  report: string;
  field: string;
  provider: string | null;
  extractedAt: string | null;
}

export interface ExplainMetricView {
  metricKey: string;
  name: string;
  pillarKey: string;
  rawValue: Notice;
  ruleApplied: string;
  mappedScore: number | null;
  weight: number;
  weightedScore: number | null;
  provenance: ProvenanceView;
}

export interface ExcludedMetricView {
  metricKey: string;
  name: string;
  pillarKey: string;
  weight: number;
  reason: Notice;
  provenance: ProvenanceView;
}

// ------------------------------------------------------------ 07 Data Lineage

export interface LineageView {
  provider: string;
  totalApplications: number;
  sources: readonly LineageSourceView[];
  capturedAt: string;
  /** La carga que se esta mirando: el linaje es por carga, no del sistema. */
  versionId: string;
  versionLabel: string;
}

export interface LineageSourceView {
  sourceName: string;
  origin: string;
  recordCount: number;
  matchedApplications: number;
  coveragePercent: number;
  rejectedCount: number;
  rejections: readonly LineageRejectionView[];
  extractedAt: string;
}

export interface LineageRejectionView {
  rowNumber: number;
  reason: Notice;
  key: string | null;
}

/**
 * Filtros del portafolio. Con `exactOptionalPropertyTypes` activo, limpiar un
 * filtro es asignarle `undefined` de forma explicita, asi que el tipo tiene que
 * admitirlo.
 */
export interface PortfolioFilters {
  lob?: string | undefined;
  country?: string | undefined;
  cio?: string | undefined;
  cre?: string | undefined;
  region?: string | undefined;
  status?: string | undefined;
  capability?: string | undefined;
  /** Codigo EAI exacto. */
  eai?: string | undefined;
  disposition?: string | undefined;
  roadmap?: string | undefined;
  /** Codigo de tramo: "critical", "at-risk", "acceptable" o "solid". */
  band?: string | undefined;
  criticalOnly?: boolean | undefined;
  page?: number | undefined;
  pageSize?: number | undefined;
}

// ------------------------------------------------------------- 04 What-If Lab

export interface WhatIfLabView {
  application: ApplicationHeaderView;
  baselineScore: number;
  baselineWeight: number;
  ceilingScore: number;
  headroom: number;
  quickWins: readonly LeverView[];
  actionable: readonly LeverView[];
  alreadyAchieved: readonly LeverView[];
  notActionable: readonly LeverView[];
}

export interface LeverView {
  key: string;
  name: string;
  action: string;
  metricKey: string;
  pillarKey: string;
  parameter: number | null;
  delta: number;
  projectedScore: number;
  weightAdded: number;
  isAlreadyAchieved: boolean;
  notActionableReason: Notice | null;
}

export interface ScenarioView {
  baselineScore: number;
  projectedScore: number;
  delta: number;
  baselineWeight: number;
  projectedWeight: number;
  sumOfIndividualDeltas: number;
  interactionEffect: number;
  applied: readonly AppliedLeverView[];
  individualEffects: readonly LeverView[];
  interactionNote: Notice | null;
}

export interface AppliedLeverView {
  key: string;
  name: string;
  action: string;
  metricKey: string;
  pillarKey: string;
  parameter: number | null;
}

export interface TargetView {
  target: number;
  baselineScore: number;
  ceilingScore: number;
  isReachable: boolean;
  isAlreadyMet: boolean;
  scenario: ScenarioView | null;
  explanation: Notice;
}

/** Palanca elegida en el laboratorio, con su cantidad si la admite. */
export interface LeverSelection {
  key: string;
  parameter?: number | undefined;
}

// ---------------------------------------------------------------- 05 Roadmap

export type AlertSeverity = 'Info' | 'Warning' | 'Critical';

export interface RoadmapPlanView {
  application: ApplicationHeaderView;
  disposition: string | null;
  dispositionDefinition: string | null;
  roadmap: string | null;
  roadmapDefinition: string | null;
  leverRationale: string | null;
  leavesPortfolio: boolean;
  baselineScore: number;
  baselineBand: string;
  baselineWeight: number;
  projectedScore: number | null;
  projectedBand: string | null;
  projectedDelta: number;
  deliveredBy: readonly AppliedLeverView[];
  milestones: readonly MilestoneView[];
  alerts: readonly AlertView[];
  startDate: string;
  completionDate: string | null;
  totalWeeks: number;
  outlook: Notice;
}

export interface MilestoneView {
  order: number;
  name: string;
  owner: string;
  weeks: number;
  start: string;
  end: string;
  sharePercent: number;
}

export interface AlertView {
  ruleKey: string;
  severity: AlertSeverity;
  title: string;
  detail: string;
  evidence: Notice;
}

export interface PortfolioRoadmapView {
  totalApplications: number;
  withoutDisposition: number;
  leavingPortfolio: number;
  dispositions: readonly DispositionGroupView[];
  alerts: readonly AlertSummaryView[];
  /** Dimension por la que se agrupo. */
  groupBy: string;
  groups: readonly RoadmapGroupView[];
}

/**
 * Un grupo del portafolio con su plan agregado. La clave es la que entienden los
 * filtros, para que abrir un grupo acote el resto de las pantallas.
 */
export interface RoadmapGroupView {
  key: string;
  name: string;
  count: number;
  sharePercent: number;
  averageScore: number;
  band: string;
  projectedScore: number | null;
  projectedBand: string | null;
  delta: number | null;
  leavingPortfolio: number;
  withoutDisposition: number;
  averageWeeks: number;
  alerts: number;
  mix: readonly RoadmapMixView[];
}

export interface RoadmapMixView {
  roadmap: string;
  name: string;
  count: number;
}

export interface DispositionGroupView {
  disposition: string;
  name: string;
  definition: string;
  count: number;
  sharePercent: number;
  averageScore: number;
  roadmaps: readonly RoadmapRowView[];
}

export interface RoadmapRowView {
  roadmap: string;
  name: string;
  definition: string;
  count: number;
  averageScore: number;
  averageProjectedScore: number | null;
  averageDelta: number | null;
  totalWeeks: number;
  removesFromPortfolio: boolean;
}

export interface AlertSummaryView {
  ruleKey: string;
  severity: AlertSeverity;
  title: string;
  count: number;
  sampleEaiCodes: readonly string[];
}

// -------------------------------------------------------------- 08 Cargas

export interface VersionView {
  id: string;
  label: string;
  capturedAt: string;
  applicationCount: number;
  isCurrent: boolean;
}

export interface HistoryPointView {
  versionId: string;
  label: string;
  capturedAt: string;
  score: number | null;
  availableWeight: number | null;
  deltaFromPrevious: number | null;
}

export interface IngestionView {
  uploadEnabled: boolean;
  provider: string;
  disabledReason: Notice | null;
  expectedFiles: readonly ExpectedFileView[];
  versions: readonly VersionView[];
  portfolioHistory: readonly HistoryPointView[];
  maxFileBytes: number;
  maxTotalBytes: number;
}

export interface ExpectedFileView {
  source: string;
  fileName: string;
  description: string | null;
  keyColumn: string;
  requiredColumns: readonly string[];
  optionalColumnCount: number;
}

export interface UploadResultView {
  accepted: boolean;
  version: VersionView | null;
  applicationCount: number;
  files: readonly UploadFileView[];
  errors: readonly Notice[];
  summary: Notice;
}

export interface UploadFileView {
  fileName: string;
  canonicalFileName: string | null;
  source: string;
  bytes: number;
  rowCount: number;
  rejectedCount: number;
  error: Notice | null;
  /** El encabezado leido, cuando no se pudo identificar la fuente. */
  detectedColumns: readonly string[];
  /** Fuentes a las que se puede asignar el archivo a mano. */
  candidates: readonly string[];
}

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
  analytics: AnalyticsDocument;
}

/**
 * Las definiciones de las seis preguntas (ADR 0025): cada parametro con su valor y su
 * rango, y cada area con su estado y su dueno. Lo edita el administrador y lo reciben
 * el dashboard e Insights para explicar sus calculos con los numeros que usaron.
 */
export interface AnalyticsDocument {
  parameters: readonly AnalyticsParameterValue[];
  areas: readonly AnalyticsAreaValue[];
  /** Del roadmap, no se edita aqui: la criticidad y el peso de negocio dependen de el. */
  significantUserBase: number;
}

export interface AnalyticsParameterValue {
  /** Clave del catalogo, como `currency.legacyBelow`. La interfaz la nombra. */
  key: string;
  area: string;
  /** score · share · points · weight · level · count · hours · kenna */
  kind: string;
  value: number;
  min: number;
  max: number;
  decimals: number;
}

export interface AnalyticsAreaValue {
  key: string;
  /** "provisional" o "agreed". */
  status: string;
  owner: string | null;
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
  /** Los indicadores del dashboard sobre todo el portafolio, antes y despues. */
  indicators: readonly IndicatorShift[];
}

export interface IndicatorShift {
  key: string;
  before: number | null;
  after: number | null;
  countBefore: number;
  countAfter: number;
}

export interface RulesSimulationView {
  valid: boolean;
  stale: boolean;
  activeVersion: string;
  errors: readonly Notice[];
  changes: readonly Notice[];
  impact: RulesImpact | null;
  /** La aplicacion en foco, metrica por metrica; null si no se pidio o no esta en la carga. */
  application: ApplicationComparison | null;
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
  analytics: AnalyticsDraft | null;
}

/** Valores por clave del catalogo y el estado y dueno de cada area. */
export interface AnalyticsDraft {
  values: Readonly<Record<string, number>> | null;
  areas: readonly AnalyticsAreaDraft[] | null;
}

export interface AnalyticsAreaDraft {
  key: string;
  status: string;
  owner: string | null;
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

// --------------------------------------------------- 10 Rules comparison

/** Una version en la linea de tiempo que ve quien consulta. No dice quien la publico. */
export interface RulesTimelineEntry {
  id: string;
  basedOn: string | null;
  publishedAt: string | null;
  reason: string;
  isActive: boolean;
  isBuiltIn: boolean;
  changeCount: number;
}

export interface RulesTimelineView {
  active: RulesTimelineEntry;
  versions: readonly RulesTimelineEntry[];
}

export interface MetricComparison {
  key: string;
  name: string;
  pillar: string;
  display: Notice;
  weightFrom: number;
  weightTo: number;
  mappedFrom: number | null;
  mappedTo: number | null;
  weightedFrom: number | null;
  weightedTo: number | null;
  includedFrom: boolean;
  includedTo: boolean;
  ruleFrom: string | null;
  ruleTo: string | null;
  exclusionFrom: Notice | null;
  exclusionTo: Notice | null;
  changed: boolean;
}

export interface PillarComparison {
  key: string;
  name: string;
  scoreFrom: number | null;
  scoreTo: number | null;
  availableFrom: number;
  availableTo: number;
  totalFrom: number;
  totalTo: number;
}

/** Una aplicacion con dos juegos de reglas. Un score null es un dato fuera de toda banda. */
export interface ApplicationComparison {
  eaiCode: string;
  applicationName: string;
  scoreFrom: number | null;
  scoreTo: number | null;
  delta: number | null;
  bandFrom: string | null;
  bandTo: string | null;
  numeratorFrom: number | null;
  numeratorTo: number | null;
  denominatorFrom: number | null;
  denominatorTo: number | null;
  pillars: readonly PillarComparison[];
  metrics: readonly MetricComparison[];
}

export interface RulesCompareView {
  from: RulesVersionView;
  to: RulesVersionView;
  changes: readonly Notice[];
  impact: RulesImpact;
  application: ApplicationComparison | null;
}


// ------------------------------------------------------------- 09 Insights

/**
 * Las seis preguntas del portafolio, respondidas sobre el recorte filtrado.
 * Nada se calcula aqui: el analisis llega hecho desde la capa System.
 */
export interface InsightsView {
  rulesetVersion: string;
  capturedAt: string;
  health: PortfolioHealthView;
  priorities: readonly ModernizationCandidateView[];
  complexity: readonly ComplexityClusterView[];
  risk: readonly RiskConcentrationView[];
  obsolescence: readonly ObsolescenceDriverView[];
  resilience: ResilienceGapsView;
  options: InsightsOptionsView;
  /** Pesos y umbrales vigentes: la pantalla explica sus calculos con ellos. */
  definitions: AnalyticsDocument;
}

/** Que tan sano esta el portafolio. `dataCoverage` es el peso del modelo que tuvo dato. */
export interface PortfolioHealthView {
  applications: number;
  averageScore: number;
  medianScore: number;
  dataCoverage: number;
  criticalBelowHealthy: number;
  bands: readonly BandShareView[];
}

export interface BandShareView {
  code: string;
  name: string;
  applications: number;
  share: number;
}

/**
 * Que modernizar primero. `impact` es el uplift ponderado por negocio y `weeks` el
 * esfuerzo que sostienen los hitos del roadmap; la lista se ordena por `valuePerWeek`.
 */
export interface ModernizationCandidateView {
  eaiCode: string;
  applicationName: string;
  lob: string | null;
  country: string | null;
  currentScore: number;
  projectedScore: number | null;
  uplift: number;
  weeks: number;
  businessWeight: number;
  impact: number;
  valuePerWeek: number;
  roadmap: string | null;
  roadmapName: string | null;
  evidence: readonly Notice[];
}

/** Donde reducir complejidad. `kind` dice que clase de concentracion se encontro. */
export interface ComplexityClusterView {
  kind: string;
  dimension: string;
  applications: number;
  users: number;
  averageScore: number;
  eaiCodes: readonly string[];
  evidence: Notice;
}

/** Donde se concentra el riesgo tecnologico. `riskIndex` ordena dentro de este portafolio. */
export interface RiskConcentrationView {
  grouping: string;
  dimension: string;
  applications: number;
  criticalFlaws: number;
  highFlaws: number;
  weakVulnerabilityManagement: number;
  obsoleteComponents: number;
  internetFacingCritical: number;
  averageScore: number;
  riskIndex: number;
}

/** Que causa la obsolescencia. `weightWithoutData` es ceguera, no obsolescencia. */
export interface ObsolescenceDriverView {
  metricKey: string;
  name: string;
  pillarKey: string;
  lostPoints: number;
  weightWithoutData: number;
  applicationsAffected: number;
  share: number;
}

/** Donde faltan defensas de continuidad. Los contadores miran solo aplicaciones criticas. */
export interface ResilienceGapsView {
  criticalWithoutDisasterRecovery: number;
  criticalWithoutMonitoring: number;
  withLowOperationalScore: number;
  withoutRecoveryObjectives: number;
  applications: readonly ResilienceGapApplicationView[];
}

export interface ResilienceGapApplicationView {
  eaiCode: string;
  applicationName: string;
  lob: string | null;
  country: string | null;
  critical: boolean;
  score: number;
  recoveryTimeObjective: string | null;
  recoveryPointObjective: string | null;
  gaps: readonly Notice[];
}

/** Los umbrales con que se calculo, para que la pantalla los muestre y los deje ajustar. */
export interface InsightsOptionsView {
  healthyScore: number;
  significantUserBase: number;
  duplicateThreshold: number;
  topCount: number;
}


// ------------------------------------------------------------ Dashboard

/**
 * El dashboard ejecutivo del portafolio filtrado. Todo llega calculado: la pantalla
 * pinta, y el detalle de cada panel filtra las mismas filas de `applications`.
 */
export interface DashboardView {
  rulesetVersion: string;
  definitionsVersion: string;
  /** "provisional" mientras alguna area lo sea; "agreed" cuando el negocio confirmo todas. */
  definitionsStatus: string;
  capturedAt: string;
  trend: readonly DashboardTrendPoint[];
  sources: readonly DashboardSource[];
  dashboard: PortfolioDashboard;
  /** Las definiciones vigentes, con que se explica cada panel. */
  definitions: AnalyticsDocument;
}

export interface DashboardTrendPoint {
  /** Currency Score promedio de CMT en esa carga. */
  currency: number | null;
  /** Risk Score promedio en esa carga (0-100, mas alto es peor). */
  riskScore: number | null;
  versionId: string;
  label: string;
  capturedAt: string;
  applications: number;
  healthScore: number | null;
  atRiskShare: number | null;
}

export interface DashboardSource {
  key: string;
  connected: boolean;
}

export interface PortfolioDashboard {
  kpis: readonly DashboardKpi[];
  matrix: DashboardHeatmap;
  health: DashboardHealth;
  prioritization: DashboardPrioritization;
  complexity: DashboardComplexity;
  risk: DashboardRisk;
  obsolescence: DashboardObsolescence;
  resilience: DashboardResilience;
  applications: readonly DashboardApplication[];
}

/** `value` es el numero grande; `count` de `base` es lo que ese numero mide. */
export interface DashboardKpi {
  key: string;
  value: number | null;
  count: number;
  base: number;
  share: number;
  level: string | null;
}

export interface DashboardHeatmap {
  axis: string;
  rows: readonly string[];
  columns: readonly string[];
  cells: readonly DashboardHeatmapCell[];
}

export interface DashboardHeatmapCell {
  row: string;
  column: string;
  applications: number;
  averageScore: number;
  band: string;
  averageRisk: number | null;
  riskLevel: string | null;
  riskMass: number;
}

export interface DashboardHealth {
  applications: number;
  averageScore: number;
  medianScore: number;
  dataCoverage: number;
  bands: readonly BandShareView[];
  requiringIntervention: number;
  interventionShare: number;
}

export interface DashboardPrioritization {
  points: readonly DashboardMatrixPoint[];
  top: readonly DashboardMatrixPoint[];
  candidates: number;
  outsideMatrix: number;
  /** Minimos del cuadrante de candidatos, con la regla vigente. */
  minCriticality: number;
  minOpportunity: number;
}

/** Criticidad y oportunidad van de 0 (baja) a 3 (muy alta). */
export interface DashboardMatrixPoint {
  eaiCode: string;
  applicationName: string;
  criticality: number;
  opportunity: number;
  riskScore: number | null;
  riskLevel: string | null;
  uplift: number;
  weeks: number;
  roadmapName: string | null;
  candidate: boolean;
}

export interface DashboardComplexity {
  dimensions: readonly DashboardComplexityDimension[];
  opportunities: readonly DashboardComplexityOpportunity[];
}

/** `status`: "provisional" o "needs-data"; sin fuente, los numeros son null. */
export interface DashboardComplexityDimension {
  key: string;
  status: string;
  applications: number | null;
  share: number | null;
  portfolioShare: number | null;
}

export interface DashboardComplexityOpportunity {
  key: string;
  count: number;
}

export interface DashboardRisk {
  averageRisk: number | null;
  hotspots: readonly DashboardHotspot[];
  concentrationIndex: number;
  concentrationLevel: string;
}

export interface DashboardHotspot {
  eaiCode: string;
  applicationName: string;
  capability: string | null;
  riskScore: number;
  riskLevel: string;
}

export interface DashboardObsolescence {
  factors: readonly DashboardObsolescenceFactor[];
  currencyDrivenShare: number;
  averageCurrency: number | null;
  withCurrency: number;
}

export interface DashboardObsolescenceFactor {
  key: string;
  status: string;
  currency: boolean;
  lostPoints: number;
  share: number;
  applicationsAffected: number;
}

export interface DashboardResilience {
  dimensions: readonly DashboardResilienceDimension[];
  overall: readonly DashboardLevelShare[];
  exposed: number;
  exposedShare: number;
  withData: number;
}

export interface DashboardResilienceDimension {
  key: string;
  status: string;
  withData: number;
  levels: readonly DashboardLevelShare[];
}

export interface DashboardLevelShare {
  level: string;
  applications: number;
  share: number;
}

/** Una fila del detalle: los mismos campos en todos los paneles. */
export interface DashboardApplication {
  eaiCode: string;
  applicationName: string;
  region: string | null;
  country: string | null;
  cio: string | null;
  cre: string | null;
  capability: string | null;
  status: string | null;
  critical: boolean;
  disposition: string | null;
  roadmap: string | null;
  score: number;
  band: string;
  riskScore: number | null;
  riskLevel: string | null;
  currency: number | null;
  legacy: boolean;
  criticality: number;
  opportunity: number | null;
  candidate: boolean;
  resilience: string | null;
  recovery: string | null;
  operational: string | null;
  complexity: readonly string[];
}

// ------------------------------------------------------------------ pantalla Code

/**
 * La pantalla Code (ADR 0026): obsolescencia y riesgo segun lo que declaran los
 * repositorios, cruzado con las fechas de fin de vida de endoflife.date.
 */
export interface CodeView {
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
  /** Hoy y en uno, dos y tres anos, si nada cambia. */
  outlook: readonly CodeHorizon[];
  /** El plan de mejora: las sugerencias de todas las aplicaciones, por accion. */
  actions: readonly CodeActionRow[];
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
  /** Componentes vencidos dentro de tres anos si nada cambia. */
  projectedEolComponents: number;
  projectedRiskLevel: string | null;
}

export interface CodeApplicationDetail {
  application: CodeApplication;
  factors: readonly CodeRiskFactor[];
  repositories: readonly CodeRepository[];
  outlook: readonly CodeApplicationHorizon[];
  suggestions: readonly CodeSuggestion[];
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
