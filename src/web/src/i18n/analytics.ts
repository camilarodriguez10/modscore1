import type { AnalyticsDocument, AnalyticsParameterValue } from '../api/types';

/**
 * Las definiciones de las seis preguntas en palabras (ADR 0025): el nombre de cada
 * area y parametro, las reglas entre parametros y la explicacion de cada calculo.
 *
 * Las explicaciones se arman con los valores que llegan del servidor, nunca con numeros
 * escritos aqui: si un administrador cambia un umbral, la frase que lo explica cambia
 * con el. Lo que si esta escrito aqui es lo que depende del codigo —de donde sale cada
 * dato y como se combina—, que no se edita desde el portal.
 */

export const AREA_KEYS = [
  'currency',
  'prioritization',
  'complexity',
  'risk',
  'obsolescence',
  'resilience',
  'insights',
] as const;

const AREAS: Record<string, { name: string; question: string }> = {
  currency: { name: 'Currency Score', question: 'Which applications run on legacy platforms?' },
  prioritization: { name: 'Prioritization', question: 'Which applications should be modernized first?' },
  complexity: { name: 'Complexity', question: 'Where should we reduce complexity?' },
  risk: { name: 'Risk concentration', question: 'Where is technology risk concentrated?' },
  obsolescence: { name: 'Obsolescence', question: 'What is driving obsolescence?' },
  resilience: { name: 'Resilience', question: 'Where are the resilience gaps?' },
  insights: { name: 'Insights screen', question: 'The six questions answered group by group' },
};

export const areaName = (key: string) => AREAS[key]?.name ?? key;
export const areaQuestion = (key: string) => AREAS[key]?.question ?? '';

export const statusLabel = (status: string) => (status === 'agreed' ? 'Agreed' : 'Provisional');

interface ParameterText {
  label: string;
  help: string;
}

const PARAMETERS: Record<string, ParameterText> = {
  'currency.legacyBelow': {
    label: 'Legacy platform below',
    help: 'An application whose Currency Score is below this value counts as a legacy platform. The BRD scores 0–59 in its lowest band.',
  },
  'criticality.broadCapabilities': {
    label: 'Broad from (capabilities)',
    help: 'An application covering at least this many L1 business capabilities is broad, like one with a significant user base.',
  },
  'opportunity.mediumFrom': {
    label: 'Medium opportunity from',
    help: 'Points the application’s own roadmap adds to its score.',
  },
  'opportunity.highFrom': { label: 'High opportunity from', help: 'Points the roadmap adds to the score.' },
  'opportunity.veryHighFrom': { label: 'Very high opportunity from', help: 'Points the roadmap adds to the score.' },
  'candidates.minCriticality': {
    label: 'Candidates: criticality at least',
    help: '0 low · 1 medium · 2 high · 3 very high.',
  },
  'candidates.minOpportunity': {
    label: 'Candidates: opportunity at least',
    help: '0 low · 1 medium · 2 high · 3 very high.',
  },
  'candidates.topCount': { label: 'Priority list length', help: 'How many applications the top list shows.' },
  'complexity.duplicateThreshold': {
    label: 'Duplicate technologies from',
    help: 'The same primary capability in the same country at least this many times.',
  },
  'complexity.languageThreshold': {
    label: 'Technology proliferation from',
    help: 'One primary capability written in at least this many coding languages.',
  },
  'complexity.techDebtMappedAtMost': {
    label: 'High technical debt at or below',
    help: 'The mapped Tech Debt Score at or below this value. With the BRD bands, 20 is the worst.',
  },
  'risk.moderateFrom': { label: 'Moderate risk from', help: 'Risk Score, 0–100, higher is worse.' },
  'risk.highFrom': { label: 'High risk from', help: 'Risk Score, 0–100, higher is worse.' },
  'risk.criticalFrom': { label: 'Critical risk from', help: 'Risk Score, 0–100, higher is worse.' },
  'risk.hotspotCount': { label: 'Riskiest applications listed', help: 'How many applications the risk panel lists.' },
  'concentration.topShare': {
    label: 'Share of cells measured',
    help: 'The index is the share of all risk carried by this fraction of the riskiest heatmap cells.',
  },
  'concentration.moderateFrom': { label: 'Moderate concentration from', help: 'Index from 0 to 1.' },
  'concentration.highFrom': { label: 'High concentration from', help: 'Index from 0 to 1.' },
  'concentration.criticalFrom': { label: 'Critical concentration from', help: 'Index from 0 to 1.' },
  'resilience.recoveryModerateFromHours': {
    label: 'Recovery: moderate from',
    help: 'Lower bound of the declared RTO range, in hours.',
  },
  'resilience.recoveryHighFromHours': {
    label: 'Recovery: high from',
    help: 'Lower bound of the declared RTO range, in hours.',
  },
  'resilience.operationalModerateBelow': { label: 'Operational: moderate below', help: 'Operational score, 0–100.' },
  'resilience.operationalHighBelow': { label: 'Operational: high below', help: 'Operational score, 0–100.' },
  'resilience.operationalCriticalBelow': { label: 'Operational: critical below', help: 'Operational score, 0–100.' },
  'insights.businessWeight.critical': {
    label: 'Business weight: critical application',
    help: 'Added to the base weight of 1 when the application is critical.',
  },
  'insights.businessWeight.significantUsers': {
    label: 'Business weight: significant user base',
    help: 'Added when the application serves at least the significant user base.',
  },
  'insights.businessWeight.broadCapabilities': {
    label: 'Business weight: broad capabilities',
    help: 'Added when the application covers at least the capabilities below.',
  },
  'insights.businessWeight.capabilitiesFrom': {
    label: 'Business weight: broad from (capabilities)',
    help: 'Capabilities from which the broad-capabilities weight applies.',
  },
  'insights.riskIndex.criticalFlaw': { label: 'Risk index: per critical flaw', help: 'Open critical SCA flaws.' },
  'insights.riskIndex.highFlaw': { label: 'Risk index: per high flaw', help: 'Open high SCA flaws.' },
  'insights.riskIndex.weakKenna': {
    label: 'Risk index: per weak Kenna',
    help: 'Applications whose Kenna score reaches the threshold below.',
  },
  'insights.riskIndex.weakKennaFrom': {
    label: 'Weak Kenna from',
    help: 'Kenna runs from 0 to 1000 and higher is worse. The BRD’s Critical/High band starts at 670.',
  },
  'insights.riskIndex.obsoleteComponent': {
    label: 'Risk index: per obsolete component',
    help: 'Obsolete and not-approved components reported by CMT.',
  },
  'insights.riskIndex.exposedCritical': {
    label: 'Risk index: per exposed critical application',
    help: 'Critical applications facing the internet.',
  },
  'insights.lowOperationalBelow': {
    label: 'Resilience gap: operational score below',
    help: 'An application under this operational score counts as a resilience gap.',
  },
  'insights.duplicateDefault': {
    label: 'Default concentration threshold',
    help: 'Applications from which a match is a concentration, when the link does not say. The screen offers 2 to 5.',
  },
};

export const parameterLabel = (key: string) => PARAMETERS[key]?.label ?? key;
export const parameterHelp = (key: string) => PARAMETERS[key]?.help ?? '';

const CONSTRAINTS: Record<string, string> = {
  'opportunity.levels': 'The opportunity levels must go up: medium < high < very high.',
  'risk.levels': 'The risk levels must go up: moderate < high < critical.',
  'concentration.levels': 'The concentration levels must go up: moderate < high < critical.',
  'resilience.recovery': 'The recovery hours must go up: moderate < high.',
  'resilience.operational': 'The operational cut-offs must go down: moderate > high > critical.',
};

/** Una regla entre parametros que no se cumple, o un parametro fuera de rango. */
export const constraintText = (key: string) =>
  CONSTRAINTS[key] ?? (PARAMETERS[key] ? `${parameterLabel(key)} is out of its range.` : `Rule ${key} does not hold.`);

const LEVELS = ['Low', 'Medium', 'High', 'Very high'];

const number = (value: number, decimals = 2) =>
  new Intl.NumberFormat('en-US', { maximumFractionDigits: decimals }).format(value);

/** El valor como se lee: con su unidad y sin decimales de mas. */
export function formatParameter(parameter: Pick<AnalyticsParameterValue, 'kind' | 'value' | 'decimals'>): string {
  const { kind, value, decimals } = parameter;

  switch (kind) {
    case 'level':
      return `${LEVELS[value] ?? value} (${value})`;
    case 'hours':
      return `${number(value, 0)} h`;
    case 'points':
      return `${number(value, decimals)} pts`;
    case 'weight':
      return `+${number(value, decimals)}`;
    default:
      return number(value, decimals);
  }
}

/** Unidad corta para el campo de edicion. */
export function unitOf(kind: string): string {
  switch (kind) {
    case 'hours':
      return 'hours';
    case 'points':
      return 'points';
    case 'level':
      return '0–3';
    case 'share':
      return '0–1';
    case 'kenna':
      return '0–1000';
    case 'weight':
      return 'weight';
    case 'score':
      return '0–100';
    default:
      return 'count';
  }
}

// ------------------------------------------------------------- explicaciones

/** Lo que responde una explicacion: que datos entran, como se calcula y que falta. */
export interface Explanation {
  area: string | null;
  considered: readonly string[];
  rules: readonly string[];
  missing: readonly string[];
}

/** Lee los valores vigentes por su clave. Una clave que no llega no rompe la frase. */
export function reader(definitions: AnalyticsDocument) {
  const byKey = new Map(definitions.parameters.map((p) => [p.key, p]));

  const raw = (key: string) => byKey.get(key)?.value ?? Number.NaN;
  const text = (key: string) => {
    const parameter = byKey.get(key);
    return parameter ? formatParameter(parameter) : '—';
  };
  const plain = (key: string) => {
    const parameter = byKey.get(key);
    return parameter ? number(parameter.value, parameter.decimals) : '—';
  };

  return { raw, text, plain, users: number(definitions.significantUserBase, 0) };
}

const SCORE_MODEL =
  'The Modernization Score v1.3 of each application: 18 metrics in three pillars (Modernization Risk Vectors, Architecture & Maturity, Stability), normalized to 20–100 with only the metrics that have data.';

/** La explicacion de un panel del dashboard, por numero de pregunta (0 es el encabezado). */
export function dashboardExplanation(question: number, definitions: AnalyticsDocument): Explanation {
  const d = reader(definitions);

  switch (question) {
    case 0:
      return {
        area: null,
        considered: [
          SCORE_MODEL,
          'Currency Score: the score CMT delivers, Raw_Tech_Score from the Currency Model Detail Report (0–100, computed per component from its end-of-life date). A 0 is no data.',
          'The filters and your portfolio scope: every indicator counts only the applications you can see and have selected.',
        ],
        rules: [
          'Portfolio Health Score: the simple average Modernization Score of the filtered applications, with its band.',
          'Applications at risk: applications in the At risk (50–70) or Critical (20–50) bands.',
          `Modernization candidates: criticality at least ${d.text('candidates.minCriticality')} and opportunity at least ${d.text('candidates.minOpportunity')} (question 2).`,
          `Currency exposure: applications with a Currency Score below ${d.plain('currency.legacyBelow')}, out of those with a Currency Score from CMT.`,
          'Resilience exposure: applications with high or critical resilience gaps, out of those with recovery or operational data (question 6).',
          'Each sparkline repeats the indicator for every upload, with the same filters.',
        ],
        missing: ['End-of-support and end-of-life dates (TLM) would add an EOS/EOL indicator.'],
      };
    case 1:
      return {
        area: null,
        considered: [
          SCORE_MODEL,
          'Primary capability, region, country and CIO from the EAI master; target disposition from MAPS.',
        ],
        rules: [
          'Each cell is the average Modernization Score of its applications; its color is the score band: Solid 85–100, Acceptable 70–85, At risk 50–70, Critical 20–50.',
          'Requiring intervention: applications in the At risk or Critical bands.',
          'Data coverage: the share of the model’s weight that had data. A high score with low coverage says little.',
          'The trend is the average score of every upload, with the same filters and scope.',
        ],
        missing: [],
      };
    case 2:
      return {
        area: 'prioritization',
        considered: [
          'Critical application indicator, user count and L1 capability count from the EAI master.',
          'The application’s own roadmap (MAPS) projected with the what-if engine.',
          'The Risk Score (question 4) for the size and color of each bubble.',
        ],
        rules: [
          `Business criticality: very high = critical and broad; high = critical; medium = broad; low = the rest. Broad = at least ${d.users} users (roadmap catalog) or at least ${d.plain('criticality.broadCapabilities')} capabilities.`,
          `Modernization opportunity = points the application’s own roadmap adds to its score: low below ${d.plain('opportunity.mediumFrom')}, medium from ${d.plain('opportunity.mediumFrom')}, high from ${d.plain('opportunity.highFrom')}, very high from ${d.plain('opportunity.veryHighFrom')}.`,
          'Applications without a plan, or whose roadmap takes them out of the portfolio, stay out of the matrix.',
          `Candidates: criticality at least ${d.text('candidates.minCriticality')} and opportunity at least ${d.text('candidates.minOpportunity')}, the shaded quadrant.`,
          `Top ${d.plain('candidates.topCount')}: ordered by criticality plus opportunity, then by Risk Score, then by points gained.`,
        ],
        missing: [
          'Effort or cost per application (DQ-02): today the weeks come from the roadmap, the same for every application on it.',
        ],
      };
    case 3:
      return {
        area: 'complexity',
        considered: [
          'Currency Score from CMT, primary capability, country and coding language from the EAI master.',
          'The mapped Tech Debt Score and the target disposition.',
        ],
        rules: [
          `Legacy platforms: Currency Score below ${d.plain('currency.legacyBelow')}.`,
          `Duplicate technologies: the same primary capability in the same country at least ${d.plain('complexity.duplicateThreshold')} times.`,
          `Technical debt: mapped Tech Debt Score at or below ${d.plain('complexity.techDebtMappedAtMost')}.`,
          `Technology proliferation: one capability written in at least ${d.plain('complexity.languageThreshold')} coding languages.`,
          'Each tile’s share is its part of all measured complexity; one application can fall in several dimensions.',
          'Opportunities: rationalize = applications in duplicate clusters; standardize = distinct coding languages; consolidate = capability-and-country clusters; retire = applications marked Eliminate that are still active.',
        ],
        missing: [
          'Shared dependencies and other complexity have no source yet (DQ-04).',
          'Open technical debts per application would replace the Tech Debt Score proxy.',
        ],
      };
    case 4:
      return {
        area: 'risk',
        considered: [
          'The Modernization Risk Vectors pillar: Kenna, DR in scope, critical and high SCA flaws, MFA and the open-source score.',
          'Primary capability and the chosen axis for the heatmap.',
        ],
        rules: [
          'Risk Score = the risk pillar inverted to 0–100, higher is worse: (100 − pillar) ÷ 80 × 100.',
          `Levels: low below ${d.plain('risk.moderateFrom')}, moderate from ${d.plain('risk.moderateFrom')}, high from ${d.plain('risk.highFrom')}, critical from ${d.plain('risk.criticalFrom')}.`,
          'Each cell is the average Risk Score of its applications.',
          `The ${d.plain('risk.hotspotCount')} riskiest applications are the highest Risk Scores.`,
          `Risk Concentration Index = the share of all risk carried by the riskiest ${number(d.raw('concentration.topShare') * 100, 0)} % of cells. An even spread gives about ${d.plain('concentration.topShare')}. Moderate from ${d.plain('concentration.moderateFrom')}, high from ${d.plain('concentration.highFrom')}, critical from ${d.plain('concentration.criticalFrom')}.`,
        ],
        missing: [],
      };
    case 5:
      return {
        area: 'obsolescence',
        considered: [
          'The points each application loses on every weighted metric of the score model.',
          'Currency Score from CMT for the average shown next to the gauge.',
        ],
        rules: [
          'Lost points per metric = weight × (100 − mapped score) ÷ 100, added across the filtered portfolio.',
          'Concepts: lifecycle (Product Strategy), components (Architectural Value), technology currency (RAW Tech Score, the Currency Score), open source (OSS score) — these four are currency concepts; technical debt; vulnerabilities (SCA flaws); host risk (Kenna); controls (MFA, DR, APIs, monitoring, operational).',
          'The headline is the share of lost points explained by currency concepts.',
          'A metric without data loses no points: that is missing data, not obsolescence.',
        ],
        missing: ['End-of-support and end-of-life exposure wait for TLM (DQ-05).'],
      };
    case 6:
      return {
        area: 'resilience',
        considered: [
          'DR in scope and the declared RTO range from the EAI master.',
          'The operational score and the monitoring capabilities (Agent, APM, Monitoring, Synthetics).',
          'The critical application indicator.',
        ],
        rules: [
          `Recovery: outside the DR scope is high (critical if the application is critical). Inside it, the lower bound of the RTO range decides: high from ${d.text('resilience.recoveryHighFromHours')}, moderate from ${d.text('resilience.recoveryModerateFromHours')}, low below; no declared RTO is moderate.`,
          `Operational vulnerabilities: operational score below ${d.plain('resilience.operationalCriticalBelow')} is critical, below ${d.plain('resilience.operationalHighBelow')} high, below ${d.plain('resilience.operationalModerateBelow')} moderate. No monitoring capability is high (critical if the application is critical). The worse of the two counts.`,
          'The application’s level is the worst of its measured dimensions; exposure means high or critical.',
        ],
        missing: [
          'Availability history (DQ-06), critical dependencies and single points of failure (DQ-04) have no source yet.',
        ],
      };
    default:
      return { area: null, considered: [], rules: [], missing: [] };
  }
}

/** La explicacion de una pregunta de la pantalla Insights (1 a 6). */
export function insightsExplanation(question: number, definitions: AnalyticsDocument, healthyScore: number): Explanation {
  const d = reader(definitions);

  switch (question) {
    case 1:
      return {
        area: null,
        considered: [SCORE_MODEL],
        rules: [
          'Average and median of every score; the median shows when a tail moves the average.',
          'Data coverage: the share of the model’s weight that had data.',
          `Critical applications below the healthy score of ${number(healthyScore, 2)} (roadmap catalog).`,
        ],
        missing: [],
      };
    case 2:
      return {
        area: 'insights',
        considered: [
          'The application’s own roadmap, projected with the what-if engine, and the weeks of its milestones.',
          'Critical indicator, user count and capability count from the EAI master.',
        ],
        rules: [
          `Business weight = 1, plus ${d.plain('insights.businessWeight.critical')} if critical, plus ${d.plain('insights.businessWeight.significantUsers')} with at least ${d.users} users, plus ${d.plain('insights.businessWeight.broadCapabilities')} with at least ${d.plain('insights.businessWeight.capabilitiesFrom')} capabilities.`,
          'Impact = points gained × business weight. The list is ordered by impact ÷ weeks.',
        ],
        missing: ['Effort per application (DQ-02) and business value (DQ-03).'],
      };
    case 3:
      return {
        area: 'insights',
        considered: [
          'Primary capability, country, coding language, disposition and status from the EAI master and MAPS.',
        ],
        rules: [
          `A match is a concentration from the number chosen above (the default is ${d.plain('insights.duplicateDefault')}): the same capability in one country, the same capability in several countries, or one capability in several languages.`,
          'Applications marked Eliminate that are still active are listed as retirement backlog.',
        ],
        missing: ['Application class as the business words it (DQ-01).'],
      };
    case 4:
      return {
        area: 'insights',
        considered: ['Open SCA flaws, Kenna, CMT component counts, internet exposure and the critical indicator.'],
        rules: [
          `Risk index = critical flaws × ${d.plain('insights.riskIndex.criticalFlaw')} + high flaws × ${d.plain('insights.riskIndex.highFlaw')} + applications with Kenna at least ${d.plain('insights.riskIndex.weakKennaFrom')} × ${d.plain('insights.riskIndex.weakKenna')} + obsolete or not-approved components × ${d.plain('insights.riskIndex.obsoleteComponent')} + critical internet-facing applications × ${d.plain('insights.riskIndex.exposedCritical')}.`,
          'Kenna runs from 0 to 1000 and higher is worse. The index ranks groups inside this portfolio; it does not compare with another one.',
        ],
        missing: [],
      };
    case 5:
      return {
        area: null,
        considered: ['Every weighted metric of the score model.'],
        rules: [
          'Lost points = weight × (100 − mapped score) ÷ 100, added across the portfolio.',
          'Weight with no data is listed apart: that is blindness, not obsolescence.',
        ],
        missing: ['End-of-support and end-of-life dates (DQ-05).'],
      };
    case 6:
      return {
        area: 'insights',
        considered: ['DR scope, monitoring, operational score and declared RTO and RPO.'],
        rules: [
          'Critical applications outside the DR scope or without any monitoring capability.',
          `Applications with an operational score below ${d.plain('insights.lowOperationalBelow')}.`,
          'Applications with no RTO or RPO declared.',
        ],
        missing: ['Incident and availability history (DQ-06).'],
      };
    default:
      return { area: null, considered: [], rules: [], missing: [] };
  }
}
