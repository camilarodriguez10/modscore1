import type { AnalyticsDocument, DashboardView } from '../api/types';
import { reader, type Explanation, type Formula, type SourceRef } from './analytics';
import { axisLabel, complexityName, factorName, levelLabel } from './dashboard';
import { bandLabel } from './notices';

/**
 * Cómo se calcula cada panel del dashboard: qué responde, los pasos, las fórmulas, de qué
 * fuente y columna sale cada dato y, si llega la vista, el cálculo con los datos en pantalla.
 *
 * Las fórmulas reproducen las de la capa System (DashboardAnalyzer, ADR 0024): si una cambia
 * allí, cambia aquí. Los umbrales nunca se escriben: llegan con las definiciones vigentes, y
 * un administrador que mueve uno ve la frase cambiar con él (ADR 0025).
 */

const BANDS = 'Solid 85–100, Acceptable 70–85, At risk 50–70, Critical 20–50';

/** El score de cada aplicación, que todos los paneles promedian, cuentan o desarman. */
const SCORE_FORMULA: Formula = {
  name: 'Modernization Score of an application',
  expression: 'Score = 20 + 80 × Σ(w × s ÷ 100) ÷ Σ w',
  where: [
    'w: the weight of each metric that has data, in the active rules. A metric without data leaves both sums.',
    's: the metric’s mapped score, 20, 60 or 100 by the bands of its table (100 is best).',
    'The result runs from 20 to 100. A recorded override replaces it where one exists.',
  ],
};

const SCORE_SOURCE: SourceRef = {
  data: 'Modernization Score',
  origin: 'Score engine, with the active rules, from the eight sources of the upload',
  fields: '18 metrics in three pillars; each application’s calculation screen shows them one by one',
};

const EAI = 'EAI master (eai_master.csv)';

const KENNA: SourceRef = {
  data: 'Kenna',
  origin: `CMT · App Sec Kenna Application Report, in the ${EAI}`,
  fields: 'Kenna_Score (0–1000, higher is worse)',
};
const DR: SourceRef = {
  data: 'DR in scope',
  origin: `EAI · Application Security and IT Risk, in the ${EAI}`,
  fields: 'DR_In_Scope',
};
const SCA: SourceRef = {
  data: 'SCA flaws',
  origin: 'Veracode SCA open flaws (veracode_sca_open_flaws.csv)',
  fields: 'EAI_CD · SVR_DSC (1 - Critical, 2 - High…) · FND_STT (open findings only)',
};
const MFA: SourceRef = {
  data: 'MFA',
  origin: `EAI · Application Security and IT Risk, in the ${EAI}`,
  fields: 'Does this Application Utilize MFA? · User Count',
};
const OSS: SourceRef = {
  data: 'Open-source score',
  origin: 'RPT · OSS Score by EAI (oss_score_by_eai.csv)',
  fields: 'EAI Code · OSS Score',
};
const CURRENCY: SourceRef = {
  data: 'Currency Score',
  origin: `CMT · Currency Model Detail Report, in the ${EAI}`,
  fields: 'Raw_Tech_Score (0–100; 0 or empty is no data)',
};
const CRITICALITY: SourceRef = {
  data: 'Criticality',
  origin: EAI,
  fields: 'Critical Application Indicator · User Count · L1_Business_Capability_Count',
};
const OPERATIONAL: SourceRef = {
  data: 'Operational score',
  origin: `CMT · Operational_Score in the ${EAI} (ServiceNow incidents and problems, 12-month window)`,
  fields: 'Operational_Score (0–100)',
};
const MONITORING: SourceRef = {
  data: 'Monitoring',
  origin: 'Elastic / APM · MONITOREO (monitoreo.csv)',
  fields: 'EAI · Agent · APM · Monitoring · Synthetics',
};
const HEATMAP_AXES: SourceRef = {
  data: 'Rows and columns',
  origin: EAI,
  fields: 'CIO (rows) · Region Name · Country Name · Primary_Business_Capability · Application_Disposition (columns)',
};

const number = (value: number, decimals = 2) =>
  new Intl.NumberFormat('en-US', { maximumFractionDigits: decimals }).format(value);

const count = (value: number, singular: string, many = `${singular}s`) =>
  `${number(value, 0)} ${value === 1 ? singular : many}`;

const SCALE = ['low', 'medium', 'high', 'very high'];

/**
 * La explicación de un panel del dashboard, por número de pregunta (0 es el encabezado de
 * indicadores). Con la vista, agrega el cálculo con los datos que se ven.
 */
export function dashboardExplanation(question: number, definitions: AnalyticsDocument, view?: DashboardView): Explanation {
  const d = reader(definitions);

  switch (question) {
    case 0:
      return {
        area: null,
        summary:
          'Five indicators summarize the filtered portfolio. Each one counts applications against a stated base, and the small line under it repeats the same count for every upload.',
        considered: [],
        rules: [
          `Portfolio Health Score: the simple average Modernization Score of the filtered applications, with its band (${BANDS}). Every application weighs the same, whatever its size.`,
          'Applications at risk: applications in the At risk (50–70) or Critical (20–50) bands, out of every filtered application.',
          `Modernization candidates: criticality at least ${d.text('candidates.minCriticality')} and opportunity at least ${d.text('candidates.minOpportunity')} (question 2), out of every filtered application.`,
          `Currency exposure: applications with a Currency Score below ${d.plain('currency.legacyBelow')}, out of those with a Currency Score from CMT. A 0 or an empty value is no data and stays out of both counts.`,
          'Resilience exposure: applications whose resilience level is high or critical, out of those with recovery or operational data (question 6).',
          'Each sparkline repeats the indicator for every upload, with the same filters and the rules in force today, so a change of rules moves every point together.',
        ],
        formulas: [
          SCORE_FORMULA,
          { name: 'Portfolio Health Score', expression: 'Health = Σ Score ÷ n', where: ['n: filtered applications with a score.'] },
          { name: 'Applications at risk', expression: 'At risk = count(Score < 70) ÷ n × 100' },
          {
            name: 'Modernization candidates',
            expression: `Candidates = count(Criticality ≥ ${d.plain('candidates.minCriticality')} and Opportunity ≥ ${d.plain('candidates.minOpportunity')}) ÷ n × 100`,
            where: ['Criticality and Opportunity run from 0 (low) to 3 (very high), as in question 2.'],
          },
          {
            name: 'Currency exposure',
            expression: `Currency = count(Raw_Tech_Score < ${d.plain('currency.legacyBelow')}) ÷ count(Raw_Tech_Score > 0) × 100`,
          },
          {
            name: 'Resilience exposure',
            expression: 'Resilience = count(level high or critical) ÷ count(level measured) × 100',
          },
        ],
        sources: [
          SCORE_SOURCE,
          CURRENCY,
          CRITICALITY,
          {
            data: 'Opportunity',
            origin: `MAPS roadmap in the ${EAI}, projected with the what-if engine; roadmap catalog (contracts/roadmap-v1.json)`,
            fields: 'Roadmap · Application_Disposition',
          },
          {
            data: 'Resilience',
            origin: `${EAI} and Monitoring (monitoreo.csv)`,
            fields: 'DR_In_Scope · Recovery_Time_Objective_Range · Operational_Score · Agent · APM · Monitoring · Synthetics',
          },
          {
            data: 'Scope and filters',
            origin: 'Your portfolio scope from the identity provider, and the filter bar',
            fields: 'scope claim (lob, country, cio) · the filters on screen',
          },
        ],
        current: view ? indicatorsNow(view) : [],
        missing: [
          'End-of-support and end-of-life dates for every inventoried component (TLM) would add an EOS/EOL indicator. The Code screen shows them for what the repositories declare.',
        ],
      };

    case 1:
      return {
        area: null,
        summary:
          'Where the score is low, by CIO and by the column you choose, and how much evidence stands behind it. A cell’s color is the band of its average score.',
        considered: [],
        rules: [
          `Each cell is the average Modernization Score of the applications of that CIO and that column; its color is the band of that average: ${BANDS}.`,
          'Rows are CIOs; columns are region, country, primary capability or target disposition, as chosen above. An application without a CIO or without a value on the axis goes to Unassigned.',
          'The Total row and column average every application of that column or CIO, not the cells; the corner is the whole portfolio. A cell with one application counts once, not as much as a full cell.',
          'Distribution: the share of applications in each band. Requiring intervention: applications in the At risk or Critical bands.',
          'Median: the middle score once sorted (the average of the two middle ones with an even count). Far from the average, a few applications are pulling it.',
          'Data coverage: the share of the model’s weight that had data, averaged over the applications. A high score with low coverage says little.',
          'The trend is the average score of every upload, with the same filters, scope and rules.',
        ],
        formulas: [
          SCORE_FORMULA,
          { name: 'Cell, row, column and corner', expression: 'Average = Σ Score ÷ applications in the cell (or row, column, portfolio)' },
          { name: 'Share of a band', expression: 'Share(band) = count(Score in band) ÷ n × 100' },
          {
            name: 'Data coverage',
            expression: 'Coverage = average over applications of (Σ w with data ÷ Σ w of every weighted metric × 100)',
            where: ['A metric with weight 0 counts in neither sum.'],
          },
        ],
        sources: [
          SCORE_SOURCE,
          HEATMAP_AXES,
          { data: 'Trend', origin: 'Every published upload, recalculated with the active rules', fields: 'one point per version' },
        ],
        current: view ? healthNow(view) : [],
        missing: [],
      };

    case 2:
      return {
        area: 'prioritization',
        summary:
          'Places each application by how much the business depends on it (criticality) and by how many points its plan would add (opportunity). The shaded quadrant holds the candidates; each bubble is sized and colored by its Risk Score.',
        considered: [],
        rules: [
          `Business criticality: very high = critical and broad; high = critical; medium = broad; low = the rest. Broad = at least ${d.users} users (roadmap catalog) or at least ${d.plain('criticality.broadCapabilities')} capabilities.`,
          `Modernization opportunity = points the application’s own roadmap adds to its score: low below ${d.plain('opportunity.mediumFrom')}, medium from ${d.plain('opportunity.mediumFrom')}, high from ${d.plain('opportunity.highFrom')}, very high from ${d.plain('opportunity.veryHighFrom')}.`,
          'An application without a MAPS roadmap uses an automatic plan: the levers with the largest effect, one after another, until the score reaches the target (90), or every lever that adds points if it cannot. Applications marked Eliminate get none.',
          'Applications whose roadmap takes them out of the portfolio, or without any plan, stay out of the matrix.',
          `Candidates: criticality at least ${d.text('candidates.minCriticality')} and opportunity at least ${d.text('candidates.minOpportunity')}, the shaded quadrant.`,
          `Top ${d.plain('candidates.topCount')}: ordered by criticality plus opportunity, then by Risk Score, then by points gained.`,
        ],
        formulas: [
          {
            name: 'Business criticality',
            expression: 'Criticality = 3 if critical and broad · 2 if critical · 1 if broad · 0 otherwise',
            where: [
              'critical: Critical Application Indicator = Y.',
              `broad: User Count ≥ ${d.users} or L1_Business_Capability_Count ≥ ${d.plain('criticality.broadCapabilities')}.`,
            ],
          },
          {
            name: 'Points gained (uplift)',
            expression: 'Uplift = score after the plan − score today',
            where: [
              'The plan is the MAPS roadmap’s levers or, without a roadmap, the automatic plan to the target. Both run in the what-if engine with the active rules, which recalculates the score from scratch.',
            ],
          },
          {
            name: 'Modernization opportunity',
            expression: `Opportunity = 3 if Uplift ≥ ${d.plain('opportunity.veryHighFrom')} · 2 if ≥ ${d.plain('opportunity.highFrom')} · 1 if ≥ ${d.plain('opportunity.mediumFrom')} · 0 otherwise`,
          },
          {
            name: 'Candidate',
            expression: `Candidate = Criticality ≥ ${d.plain('candidates.minCriticality')} and Opportunity ≥ ${d.plain('candidates.minOpportunity')}`,
          },
          {
            name: 'Order of the top list',
            expression: 'Criticality + Opportunity (high first) → Risk Score (high first) → Uplift (high first) → EAI code',
          },
        ],
        sources: [
          CRITICALITY,
          {
            data: 'Significant user base and target',
            origin: 'Roadmap catalog (contracts/roadmap-v1.json), published with the rules',
            fields: `significantUserBase = ${d.users} · automaticPlanTarget`,
          },
          {
            data: 'Plan',
            origin: `${EAI} and the roadmap catalog, run in the what-if engine`,
            fields: 'Roadmap · Application_Disposition · levers of each roadmap',
          },
          { data: 'Risk Score', origin: 'Question 4', fields: 'Modernization Risk Vectors pillar' },
        ],
        current: view ? prioritizationNow(view) : [],
        missing: [
          'Effort or cost per application (DQ-02): today the weeks come from the roadmap, the same for every application on it.',
        ],
      };

    case 3:
      return {
        area: 'complexity',
        summary:
          'Counts the applications in each measured kind of complexity and the actions that would reduce it. One application can fall in several kinds, so the counts can add up to more than the portfolio.',
        considered: [],
        rules: [
          `Legacy platforms: Currency Score below ${d.plain('currency.legacyBelow')}.`,
          `Duplicate technologies: the same primary capability in the same country at least ${d.plain('complexity.duplicateThreshold')} times.`,
          `Technical debt: mapped Tech Debt Score at or below ${d.plain('complexity.techDebtMappedAtMost')}.`,
          `Technology proliferation: one capability written in at least ${d.plain('complexity.languageThreshold')} coding languages.`,
          'Each tile’s share is its part of all measured complexity; one application can fall in several dimensions.',
          'Opportunities: rationalize = applications in duplicate clusters; standardize = distinct coding languages; consolidate = capability-and-country clusters; retire = applications marked Eliminate that are still active.',
        ],
        formulas: [
          { name: 'Legacy platform', expression: `Legacy = 0 < Raw_Tech_Score < ${d.plain('currency.legacyBelow')}` },
          {
            name: 'Duplicate technologies',
            expression: `Duplicate = applications with the same capability in the same country ≥ ${d.plain('complexity.duplicateThreshold')}`,
            where: ['Every application of such a cluster counts.'],
          },
          {
            name: 'Technical debt',
            expression: `High debt = mapped Tech Debt score ≤ ${d.plain('complexity.techDebtMappedAtMost')}`,
            where: ['The mapped score follows the Tech Debt table of the active rules: with the BRD bands, COMBINED_APP_SCORE 0 maps to 100, 0.1–2.4 to 60 and 2.5–5 to 20.'],
          },
          {
            name: 'Technology proliferation',
            expression: `Proliferation = distinct Coding_Language values within one capability ≥ ${d.plain('complexity.languageThreshold')}`,
          },
          {
            name: 'Shares',
            expression: 'Share = applications in the dimension ÷ Σ applications over measured dimensions × 100',
            where: ['Share of the portfolio: applications in the dimension ÷ n × 100.'],
          },
        ],
        sources: [
          CURRENCY,
          {
            data: 'Capability, country, language, status',
            origin: EAI,
            fields: 'Primary_Business_Capability · Country Name · Coding_Language · Application Status · Application_Disposition',
          },
          {
            data: 'Tech Debt Score',
            origin: 'EAI · V_TECH_DEBT_APP_SCORE (tech_debt.csv), mapped by the score engine',
            fields: 'APP_ID · COMBINED_APP_SCORE',
          },
        ],
        current: view ? complexityNow(view) : [],
        missing: [
          'Shared dependencies and other complexity have no source yet (DQ-04).',
          'Open technical debts per application would replace the Tech Debt Score proxy.',
        ],
      };

    case 4:
      return {
        area: 'risk',
        summary:
          'Where security and recoverability risk sits, by CIO and the column you choose, and whether it is spread out or concentrated in a few places. Higher is worse.',
        considered: [],
        rules: [
          'Risk Score = the risk pillar inverted to 0–100, higher is worse: (100 − pillar) ÷ 80 × 100.',
          `Levels: low below ${d.plain('risk.moderateFrom')}, moderate from ${d.plain('risk.moderateFrom')}, high from ${d.plain('risk.highFrom')}, critical from ${d.plain('risk.criticalFrom')}.`,
          'Each cell is the average Risk Score of its applications. The Total row and column average every application of that column or CIO, not the cells.',
          `The ${d.plain('risk.hotspotCount')} riskiest applications are the highest Risk Scores.`,
          `Risk Concentration Index = the share of all risk carried by the riskiest ${number(d.raw('concentration.topShare') * 100, 0)} % of cells. An even spread gives about ${d.plain('concentration.topShare')}. Moderate from ${d.plain('concentration.moderateFrom')}, high from ${d.plain('concentration.highFrom')}, critical from ${d.plain('concentration.criticalFrom')}.`,
        ],
        formulas: [
          {
            name: 'Risk pillar of an application',
            expression: 'Pillar = 20 + 80 × Σ(w × s ÷ 100) ÷ Σ w, over the risk metrics with data',
            where: ['Risk metrics: Kenna, DR in scope, SCA flaws by severity, MFA and the open-source score. A metric weighted 0 does not count.'],
          },
          {
            name: 'Risk Score',
            expression: 'Risk Score = (100 − Pillar) ÷ 80 × 100 = (1 − Σ(w × s ÷ 100) ÷ Σ w) × 100',
            where: [
              'It is the share of the available risk weight that is lost: 0 when every risk metric with data is in its best band, 100 when every one is in its worst.',
              'Kept within 0–100. Without any risk metric with data, there is no Risk Score.',
            ],
          },
          { name: 'Cell, row and column', expression: 'Average = Σ Risk Score ÷ applications with a Risk Score' },
          { name: 'Risk mass of a cell', expression: 'Mass = Σ Risk Score of its applications' },
          {
            name: 'Risk Concentration Index',
            expression: `Index = Σ mass of the top k cells ÷ Σ mass of all cells,  k = ceil(cells × ${d.plain('concentration.topShare')}), at least 1`,
            where: ['Cells are ordered by mass. A cell with many risky applications carries more mass than one with a single application.'],
          },
        ],
        sources: [KENNA, DR, SCA, MFA, OSS, HEATMAP_AXES],
        current: view ? riskNow(view, definitions) : [],
        missing: [],
      };

    case 5:
      return {
        area: 'obsolescence',
        summary:
          'Which kinds of weakness take the most points away from the portfolio’s score, and how much of that loss is technology currency.',
        considered: [],
        rules: [
          'Lost points per metric = weight × (100 − mapped score) ÷ 100, added across the filtered portfolio.',
          'Concepts: lifecycle (Product Strategy), components (Architectural Value), technology currency (RAW Tech Score, the Currency Score), open source (OSS score) — these four are currency concepts; technical debt; vulnerabilities (SCA flaws); host risk (Kenna); controls (MFA, DR, APIs, monitoring, operational).',
          'The headline is the share of lost points explained by currency concepts.',
          'A metric without data loses no points: that is missing data, not obsolescence.',
        ],
        formulas: [
          {
            name: 'Points an application loses on a metric',
            expression: 'Lost = w × (100 − s) ÷ 100',
            where: [
              'w: the metric’s weight; s: its mapped score. In its best band a metric loses 0; in its worst (20), 80 % of its weight.',
              'A metric without data, or weighted 0, loses nothing.',
            ],
          },
          {
            name: 'Link with the score',
            expression: 'Score = 100 − 80 × Σ Lost ÷ Σ w',
            where: ['For each application: every lost point lowers its score by 80 ÷ Σ w, where Σ w is the weight that had data.'],
          },
          { name: 'Points lost by a concept', expression: 'Lost(concept) = Σ over applications of Σ Lost of the concept’s metrics' },
          { name: 'Share of a concept', expression: 'Share = Lost(concept) ÷ Σ Lost(every concept) × 100' },
          {
            name: 'Headline (currency-driven share)',
            expression: 'Headline = Share(lifecycle) + Share(components) + Share(technology currency) + Share(open source)',
          },
          { name: 'Average Currency Score', expression: 'Σ Raw_Tech_Score ÷ applications with Raw_Tech_Score > 0' },
        ],
        sources: [
          {
            data: 'Lifecycle',
            origin: 'MTS · Product Strategy (product_strategy.csv)',
            fields: 'Invest_Count · Tolerate_Count · Eliminate_Count · Migrate_Count',
          },
          {
            data: 'Components',
            origin: 'CMT · Architectural Value by EAI (architectural_value_by_eai.csv)',
            fields: 'Modern_Count · Heritage_Count · Obsolete_Count · Not_Approved_for_Use_Count',
          },
          { ...CURRENCY, data: 'Technology currency' },
          { ...OSS, data: 'Open source' },
          {
            data: 'Technical debt',
            origin: 'EAI · V_TECH_DEBT_APP_SCORE (tech_debt.csv)',
            fields: 'COMBINED_APP_SCORE',
          },
          { ...SCA, data: 'Vulnerabilities' },
          { ...KENNA, data: 'Host risk' },
          {
            data: 'Controls',
            origin: `${EAI}, APIM onboarding (apim_onboarded_eais.csv) and Monitoring (monitoreo.csv)`,
            fields: 'Does this Application Utilize MFA? · DR_In_Scope · API Type · Agent / APM / Monitoring / Synthetics · Operational_Score',
          },
        ],
        current: view ? obsolescenceNow(view) : [],
        missing: ['End-of-support and end-of-life exposure wait for TLM (DQ-05).'],
      };

    case 6:
      return {
        area: 'resilience',
        summary:
          'Rates each application’s recovery and operational weakness and shows how many are exposed. Availability, critical dependencies and single points of failure have no source yet.',
        considered: [],
        rules: [
          `Recovery: outside the DR scope is high (critical if the application is critical). Inside it, the lower bound of the RTO range decides: high from ${d.text('resilience.recoveryHighFromHours')}, moderate from ${d.text('resilience.recoveryModerateFromHours')}, low below; no declared RTO is moderate.`,
          `Operational vulnerabilities: operational score below ${d.plain('resilience.operationalCriticalBelow')} is critical, below ${d.plain('resilience.operationalHighBelow')} high, below ${d.plain('resilience.operationalModerateBelow')} moderate. No monitoring capability is high (critical if the application is critical). The worse of the two counts.`,
          'The application’s level is the worst of its measured dimensions; exposure means high or critical.',
        ],
        formulas: [
          {
            name: 'Recovery',
            expression: `Recovery = outside DR: critical if the application is critical, else high · inside DR: high if RTO ≥ ${d.plain('resilience.recoveryHighFromHours')} h, moderate if RTO ≥ ${d.plain('resilience.recoveryModerateFromHours')} h or not declared, low otherwise`,
            where: ['RTO: the lower bound of the declared range; “24-48 hrs.” is 24 hours.', 'Without DR_In_Scope there is no recovery level.'],
          },
          {
            name: 'Operational',
            expression: `By score: critical < ${d.plain('resilience.operationalCriticalBelow')} ≤ high < ${d.plain('resilience.operationalHighBelow')} ≤ moderate < ${d.plain('resilience.operationalModerateBelow')} ≤ low
By monitoring: low with any capability; high without one (critical if the application is critical)
Operational = the worse of the two`,
          },
          { name: 'Application level', expression: 'Level = the worse of Recovery and Operational, among those measured' },
          { name: 'Resilience exposure', expression: 'Exposure = count(level high or critical) ÷ count(level measured) × 100' },
        ],
        sources: [
          { ...DR, data: 'DR and RTO', fields: 'DR_In_Scope · Recovery_Time_Objective_Range' },
          OPERATIONAL,
          MONITORING,
          { ...CRITICALITY, fields: 'Critical Application Indicator' },
        ],
        current: view ? resilienceNow(view) : [],
        missing: [
          'Availability history (DQ-06), critical dependencies and single points of failure (DQ-04) have no source yet.',
        ],
      };

    default:
      return { area: null, considered: [], rules: [], missing: [] };
  }
}

// ------------------------------------------------- con los datos en pantalla

const percent = (value: number) => `${number(value, 2)} %`;

function indicatorsNow(view: DashboardView): string[] {
  const kpi = (key: string) => view.dashboard.kpis.find((k) => k.key === key);
  const lines: string[] = [];

  const health = kpi('health');
  if (health?.value !== null && health?.value !== undefined) {
    lines.push(
      `Portfolio Health Score: the average of ${count(health.base, 'application')} is ${number(health.value)}${health.level ? `, ${bandLabel(health.level)}` : ''}.`,
    );
  }

  const ratio = (key: string, label: string, singular: string, many = `${singular}s`) => {
    const value = kpi(key);
    if (value) {
      lines.push(`${label}: ${number(value.count, 0)} of ${count(value.base, singular, many)} = ${percent(value.share)}.`);
    }
  };

  ratio('atRisk', 'Applications at risk', 'application');
  ratio('candidates', 'Modernization candidates', 'application');
  ratio('currency', 'Currency exposure', 'application with a Currency Score', 'applications with a Currency Score');
  ratio('resilience', 'Resilience exposure', 'application with resilience data', 'applications with resilience data');

  return lines;
}

function healthNow(view: DashboardView): string[] {
  const { health, matrix } = view.dashboard;

  return [
    `${count(health.applications, 'application')}: average ${number(health.averageScore)} (${bandLabel(bandOf(health.averageScore))}), median ${number(health.medianScore)}.`,
    `${count(matrix.rows.length, 'CIO')} × ${count(matrix.columns.length, `${axisLabel(matrix.axis).toLowerCase()} value`)}: ${count(matrix.cells.length, 'cell')} with applications.`,
    `Requiring intervention: ${number(health.requiringIntervention, 0)} of ${number(health.applications, 0)} = ${percent(health.interventionShare)}.`,
    `Data coverage: on average, ${percent(health.dataCoverage)} of the model’s weight had data.`,
  ];
}

function prioritizationNow(view: DashboardView): string[] {
  const p = view.dashboard.prioritization;
  const lines = [
    `${count(p.points.length, 'application')} in the matrix${p.automaticTarget !== null ? `, ${number(p.automaticPlans, 0)} of them with an automatic plan to ${number(p.automaticTarget)}` : ''}.`,
    `${count(p.candidates, 'candidate')} in the shaded quadrant.`,
    `Outside the matrix: ${number(p.outsideMatrix, 0)}, of which ${number(p.withoutPlan, 0)} have no plan at all; the rest leave the portfolio or gain no points.`,
  ];

  const first = p.top[0];
  if (first) {
    lines.push(
      `First of the list: ${first.applicationName} (${first.eaiCode}), criticality ${SCALE[first.criticality] ?? first.criticality}, opportunity ${SCALE[first.opportunity] ?? first.opportunity}, +${number(first.uplift)} points${first.riskScore !== null ? `, Risk Score ${number(first.riskScore)}` : ''}.`,
    );
  }

  return lines;
}

function complexityNow(view: DashboardView): string[] {
  const measured = view.dashboard.complexity.dimensions.filter((dimension) => dimension.applications !== null);

  return measured.map(
    (dimension) =>
      `${complexityName(dimension.key)}: ${count(dimension.applications ?? 0, 'application')}, ${percent(dimension.share ?? 0)} of measured complexity and ${percent(dimension.portfolioShare ?? 0)} of the portfolio.`,
  );
}

function riskNow(view: DashboardView, definitions: AnalyticsDocument): string[] {
  const { risk, matrix, applications } = view.dashboard;
  const d = reader(definitions);
  const withRisk = applications.filter((application) => application.riskScore !== null).length;
  const cells = matrix.cells.length;
  const top = cells === 0 ? 0 : Math.max(1, Math.ceil(cells * d.raw('concentration.topShare')));
  const lines: string[] = [];

  if (risk.averageRisk !== null) {
    lines.push(`Average Risk Score: ${number(risk.averageRisk)} over ${count(withRisk, 'application')} with a Risk Score.`);
  }

  if (cells > 0 && Number.isFinite(top)) {
    lines.push(
      `Concentration: the riskiest ${number(top, 0)} of ${count(cells, 'cell')} carry ${percent(risk.concentrationIndex * 100)} of the risk mass, index ${number(risk.concentrationIndex)} (${levelLabel(risk.concentrationLevel).toLowerCase()}).`,
    );
  }

  const first = risk.hotspots[0];
  if (first) {
    lines.push(`Riskiest application: ${first.applicationName} (${first.eaiCode}), Risk Score ${number(first.riskScore)}.`);
  }

  return lines;
}

function obsolescenceNow(view: DashboardView): string[] {
  const o = view.dashboard.obsolescence;
  const measured = o.factors.filter((factor) => factor.status === 'measured');
  const lines = [`Currency concepts explain ${percent(o.currencyDrivenShare)} of the points lost.`];

  for (const factor of measured.slice(0, 3)) {
    lines.push(
      `${factorName(factor.key)}: ${number(factor.lostPoints)} points lost (${percent(factor.share)}), across ${count(factor.applicationsAffected, 'application')}.`,
    );
  }

  if (o.averageCurrency !== null) {
    lines.push(`Average Currency Score: ${number(o.averageCurrency)} over ${count(o.withCurrency, 'application')}.`);
  }

  return lines;
}

function resilienceNow(view: DashboardView): string[] {
  const r = view.dashboard.resilience;
  const lines = [`Exposed: ${number(r.exposed, 0)} of ${count(r.withData, 'application')} with data = ${percent(r.exposedShare)}.`];

  for (const dimension of r.dimensions.filter((item) => item.status !== 'needs-data')) {
    const parts = dimension.levels.map((level) => `${levelLabel(level.level).toLowerCase()} ${number(level.applications, 0)}`);
    lines.push(`${dimensionName(dimension.key)}: ${count(dimension.withData, 'application')} measured — ${parts.join(', ')}.`);
  }

  return lines;
}

const dimensionName = (key: string) =>
  key === 'recovery' ? 'Recovery' : key === 'operationalVulnerabilities' ? 'Operational vulnerabilities' : key;

/** El tramo de un promedio, con los mismos cortes que la capa System. */
function bandOf(score: number): string {
  if (score >= 85) return 'solid';
  if (score >= 70) return 'acceptable';
  if (score >= 50) return 'at-risk';
  return 'critical';
}
