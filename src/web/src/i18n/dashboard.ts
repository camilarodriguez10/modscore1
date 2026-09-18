/**
 * Nombres de lo que muestra el dashboard ejecutivo.
 *
 * El backend manda claves y niveles; aqui se nombran (ADR 0017). Una clave que este
 * catalogo no conoce se muestra tal cual: mejor ver la clave que un hueco.
 */

const KPIS: Record<string, { title: string; sub: string }> = {
  health: { title: 'Portfolio health score', sub: 'average Modernization Score' },
  atRisk: { title: 'Applications at risk', sub: 'in the critical and at-risk bands' },
  candidates: { title: 'Modernization candidates', sub: 'high criticality and high opportunity' },
  currency: { title: 'Currency exposure', sub: 'legacy platforms by Currency Score' },
  resilience: { title: 'Resilience exposure', sub: 'high or critical resilience gaps' },
};

export function kpiTitle(key: string): string {
  return KPIS[key]?.title ?? key;
}

export function kpiSub(key: string): string {
  return KPIS[key]?.sub ?? '';
}

const LEVELS: Record<string, string> = {
  low: 'Low',
  moderate: 'Moderate',
  high: 'High',
  critical: 'Critical',
};

/** Nivel de riesgo o de resiliencia. */
export function levelLabel(level: string | null | undefined): string {
  if (!level) return 'No data';
  return LEVELS[level] ?? level;
}

/** Criticidad y oportunidad llegan de 0 a 3. */
const SCALE = ['Low', 'Medium', 'High', 'Very high'] as const;
const SCALE_SHORT = ['L', 'M', 'H', 'VH'] as const;

export function scaleLabel(value: number | null | undefined): string {
  return value === null || value === undefined ? 'No data' : (SCALE[value] ?? String(value));
}

export function scaleShort(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : (SCALE_SHORT[value] ?? String(value));
}

const AXES: Record<string, string> = {
  region: 'Region',
  country: 'Country',
  cio: 'CIO',
  disposition: 'Target disposition',
};

export const AXIS_KEYS = ['region', 'country', 'cio', 'disposition'] as const;

export function axisLabel(axis: string): string {
  return AXES[axis] ?? axis;
}

const COMPLEXITY: Record<string, { name: string; definition: string }> = {
  legacyPlatforms: { name: 'Legacy platforms', definition: 'Currency Score below the legacy threshold' },
  duplicateTechnologies: {
    name: 'Duplicate technologies',
    definition: 'the same primary capability several times in one country',
  },
  technicalDebt: { name: 'Technical debt clusters', definition: 'Tech Debt Score in its worst band' },
  sharedDependencies: { name: 'Shared dependencies', definition: 'needs application dependency data' },
  technologyProliferation: {
    name: 'Technology proliferation',
    definition: 'one capability written in many languages',
  },
  otherComplexity: { name: 'Other complexity', definition: 'still to be defined by the business' },
};

export function complexityName(key: string): string {
  return COMPLEXITY[key]?.name ?? key;
}

export function complexityDefinition(key: string): string {
  return COMPLEXITY[key]?.definition ?? '';
}

const OPPORTUNITIES: Record<string, { action: string; unit: (count: number) => string }> = {
  rationalize: { action: 'Rationalize', unit: (n) => `${n} duplicate ${n === 1 ? 'application' : 'applications'}` },
  standardize: { action: 'Standardize', unit: (n) => `${n} technology ${n === 1 ? 'family' : 'families'}` },
  consolidate: { action: 'Consolidate platforms', unit: (n) => `${n} platform ${n === 1 ? 'opportunity' : 'opportunities'}` },
  retire: { action: 'Retire / decommission', unit: (n) => `${n} ${n === 1 ? 'application' : 'applications'} marked Eliminate` },
};

export function opportunityAction(key: string): string {
  return OPPORTUNITIES[key]?.action ?? key;
}

export function opportunityUnit(key: string, count: number): string {
  return OPPORTUNITIES[key]?.unit(count) ?? String(count);
}

const FACTORS: Record<string, string> = {
  lifecycle: 'Lifecycle status (products to eliminate or migrate)',
  components: 'Obsolete and unapproved components',
  technologyCurrency: 'Technology currency (RAW Tech)',
  openSource: 'Open-source health',
  technicalDebt: 'Technical debt',
  vulnerabilities: 'Open vulnerabilities (SCA)',
  hostRisk: 'Host risk (Kenna)',
  controls: 'Missing controls (MFA, DR, API, monitoring, operations)',
  eosExposure: 'End-of-support exposure',
  eolExposure: 'End-of-life exposure',
};

export function factorName(key: string): string {
  return FACTORS[key] ?? key;
}

const RESILIENCE: Record<string, { name: string; source: string }> = {
  availability: { name: 'Availability', source: 'needs availability history' },
  recovery: { name: 'Recovery', source: 'DR scope and RTO' },
  criticalDependencies: { name: 'Critical dependencies', source: 'needs dependency data' },
  singlePointsOfFailure: { name: 'Single points of failure', source: 'needs resiliency data' },
  operationalVulnerabilities: { name: 'Operational vulnerabilities', source: 'operational score and monitoring' },
};

export function resilienceName(key: string): string {
  return RESILIENCE[key]?.name ?? key;
}

export function resilienceSource(key: string): string {
  return RESILIENCE[key]?.source ?? '';
}

const SOURCES: Record<string, string> = {
  eai: 'EAI',
  maps: 'MAPS',
  cmt: 'CMT',
  mts: 'MTS',
  sca: 'Veracode SCA',
  rpt: 'Risk Profile Tool',
  monitoring: 'Monitoring',
  tlm: 'TLM',
  architecturalAssessments: 'Architectural assessments',
  arb: 'ARB governance platform',
  resiliency: 'Resiliency data',
};

export function sourceName(key: string): string {
  return SOURCES[key] ?? key;
}
