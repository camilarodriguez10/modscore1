import { describe, expect, it } from 'vitest';
import type { AnalyticsDocument, DashboardView } from '../api/types';
import {
  AREA_KEYS,
  areaName,
  constraintText,
  formatParameter,
  parameterLabel,
} from './analytics';
import { dashboardExplanation } from './dashboardExplain';
import { formatNotice } from './notices';

const definitions = (legacyBelow = 60): AnalyticsDocument => ({
  parameters: [
    { key: 'currency.legacyBelow', area: 'currency', kind: 'score', value: legacyBelow, min: 0.01, max: 100, decimals: 2 },
    { key: 'risk.moderateFrom', area: 'risk', kind: 'score', value: 40, min: 0.01, max: 100, decimals: 2 },
    { key: 'risk.highFrom', area: 'risk', kind: 'score', value: 60, min: 0.01, max: 100, decimals: 2 },
    { key: 'risk.criticalFrom', area: 'risk', kind: 'score', value: 80, min: 0.01, max: 100, decimals: 2 },
    { key: 'concentration.topShare', area: 'risk', kind: 'share', value: 0.2, min: 0.01, max: 1, decimals: 2 },
    { key: 'candidates.minCriticality', area: 'prioritization', kind: 'level', value: 2, min: 0, max: 3, decimals: 0 },
    { key: 'resilience.recoveryHighFromHours', area: 'resilience', kind: 'hours', value: 48, min: 1, max: 720, decimals: 0 },
  ],
  areas: AREA_KEYS.map((key) => ({ key, status: 'provisional', owner: null })),
  significantUserBase: 1000,
});

describe('definition values', () => {
  it('reads every kind with its unit', () => {
    expect(formatParameter({ kind: 'score', value: 55.5, decimals: 2 })).toBe('55.5');
    expect(formatParameter({ kind: 'level', value: 2, decimals: 0 })).toBe('High (2)');
    expect(formatParameter({ kind: 'hours', value: 48, decimals: 0 })).toBe('48 h');
    expect(formatParameter({ kind: 'weight', value: 0.25, decimals: 2 })).toBe('+0.25');
    expect(formatParameter({ kind: 'points', value: 10, decimals: 2 })).toBe('10 pts');
  });

  it('names every area, parameter and rule, and shows an unknown key as it is', () => {
    expect(areaName('resilience')).toBe('Resilience');
    expect(parameterLabel('currency.legacyBelow')).toBe('Legacy platform below');
    expect(constraintText('risk.levels')).toContain('moderate < high < critical');
    expect(parameterLabel('somethingNew')).toBe('somethingNew');
  });
});

describe('explanations', () => {
  it('say the values in force, not numbers written in the interface', () => {
    const at60 = dashboardExplanation(0, definitions(60)).rules.join(' ');
    const at75 = dashboardExplanation(0, definitions(75)).rules.join(' ');

    expect(at60).toContain('Currency Score below 60');
    expect(at75).toContain('Currency Score below 75');
  });

  it('explain the risk levels and the concentration with the published thresholds', () => {
    const risk = dashboardExplanation(4, definitions()).rules.join(' ');

    expect(risk).toContain('moderate from 40, high from 60, critical from 80');
    expect(risk).toContain('riskiest 20 % of cells');
  });

  it('say which definition area each panel belongs to', () => {
    expect(dashboardExplanation(2, definitions()).area).toBe('prioritization');
    expect(dashboardExplanation(6, definitions()).area).toBe('resilience');
    expect(dashboardExplanation(1, definitions()).area).toBeNull();
  });

  it('name what is not measured yet', () => {
    expect(dashboardExplanation(6, definitions()).missing.join(' ')).toContain('single points of failure');
  });
});

describe('definition notices', () => {
  it('name the parameter, the rule and the area instead of their keys', () => {
    expect(
      formatNotice({
        code: 'rules.analyticsValue',
        args: { parameter: 'risk.hotspotCount', min: '1', max: '50', decimals: '0' },
      }),
    ).toBe('Riskiest applications listed must be between 1 and 50, a whole number.');

    expect(formatNotice({ code: 'rules.analyticsInconsistent', args: { rule: 'risk.levels' } })).toContain(
      'moderate < high < critical',
    );

    expect(
      formatNotice({ code: 'change.analyticsStatus', args: { area: 'currency', from: 'provisional', to: 'agreed' } }),
    ).toBe('Currency Score: provisional → agreed');
  });
});

describe('detailed explanations', () => {
  it('give every dashboard panel a summary, formulas and the sources with their columns', () => {
    for (const question of [0, 1, 2, 3, 4, 5, 6]) {
      const explanation = dashboardExplanation(question, definitions());

      expect(explanation.summary, `question ${question}`).toBeTruthy();
      expect(explanation.formulas?.length ?? 0, `question ${question}`).toBeGreaterThan(0);
      expect(explanation.sources?.length ?? 0, `question ${question}`).toBeGreaterThan(0);
    }
  });

  it('write the formulas with the thresholds in force', () => {
    const indicators = dashboardExplanation(0, definitions(75)).formulas?.map((f) => f.expression).join(' ') ?? '';
    expect(indicators).toContain('count(Raw_Tech_Score < 75)');
    expect(indicators).toContain('Criticality ≥ 2');

    const risk = dashboardExplanation(4, definitions()).formulas?.map((f) => f.expression).join(' ') ?? '';
    expect(risk).toContain('(100 − Pillar) ÷ 80 × 100');
    expect(risk).toContain('k = ceil(cells × 0.2)');
  });

  it('name the report and the column each figure comes from', () => {
    const fields = (question: number) =>
      dashboardExplanation(question, definitions())
        .sources?.map((s) => `${s.origin} ${s.fields}`)
        .join(' ') ?? '';

    expect(fields(4)).toContain('Kenna_Score');
    expect(fields(4)).toContain('veracode_sca_open_flaws.csv');
    expect(fields(5)).toContain('Not_Approved_for_Use_Count');
    expect(fields(6)).toContain('Recovery_Time_Objective_Range');
  });

  it('work the numbers out with the data on screen', () => {
    const view = {
      dashboard: {
        kpis: [{ key: 'atRisk', value: 30, count: 75, base: 250, share: 30, level: null }],
        matrix: { axis: 'country', rows: [], columns: [], cells: Array.from({ length: 12 }, () => ({})) },
        applications: [{ riskScore: 50 }, { riskScore: null }],
        risk: {
          averageRisk: 50,
          concentrationIndex: 0.42,
          concentrationLevel: 'high',
          hotspots: [{ eaiCode: '14381', applicationName: 'Reference Application', riskScore: 90 }],
        },
      },
    } as unknown as DashboardView;

    expect(dashboardExplanation(0, definitions(), view).current).toContain('Applications at risk: 75 of 250 applications = 30 %.');

    const risk = dashboardExplanation(4, definitions(), view).current?.join(' ') ?? '';
    expect(risk).toContain('the riskiest 3 of 12 cells carry 42 % of the risk mass, index 0.42 (high)');
    expect(risk).toContain('Reference Application (14381), Risk Score 90');
  });

  it('show nothing computed without the data', () => {
    expect(dashboardExplanation(4, definitions()).current).toEqual([]);
  });
});
