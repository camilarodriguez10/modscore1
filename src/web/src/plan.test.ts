import { describe, expect, it } from 'vitest';
import type { ActionPlanView, ActionStepView } from './api/types';
import { futurePlanPath, ownerLabel, parsePlanQuery, parseTargetQuery, planSummary, planToQuery } from './plan';

const step: ActionStepView = {
  order: 1,
  leverKey: 'oss.improve',
  leverName: 'Raise the Open-Source Score to its best level',
  pillarKey: 'risk',
  metricKey: 'oss',
  metricName: 'Open-Source Score',
  owner: 'development',
  parameter: null,
  from: { code: 'value.plain', args: { value: '28.95' } },
  to: { code: 'value.plain', args: { value: '100' } },
  mappedFrom: 28.95,
  mappedTo: 100,
  weightAdded: 0,
  points: 9.48,
  scoreAfter: 90.18,
  actions: [],
};

const plan = (overrides: Partial<ActionPlanView> = {}): ActionPlanView => ({
  target: 90,
  isReachable: true,
  isAlreadyMet: false,
  baselineScore: 62.32,
  projectedScore: 90.88,
  projectedBand: 'solid',
  ceilingScore: 97.5,
  steps: [],
  ...overrides,
});

describe('target between the What-If Lab and the future plan', () => {
  it('accepts a score from 20 to 100 with up to two decimals', () => {
    expect(parseTargetQuery('80')).toBe(80);
    expect(parseTargetQuery('85.5')).toBe(85.5);
    expect(parseTargetQuery('100')).toBe(100);
  });

  it('ignores anything else, so the catalog target applies', () => {
    for (const value of [null, '', '10', '101', '80.123', '-80', '8e1', 'abc', '80;drop']) {
      expect(parseTargetQuery(value)).toBeNull();
    }
  });

  it('links the future plan with the target, or without it for the catalog target', () => {
    expect(futurePlanPath('14381', 80)).toBe('/applications/14381/roadmap?target=80');
    expect(futurePlanPath('14381', null)).toBe('/applications/14381/roadmap');
  });
});

describe('future plan', () => {
  it('opens in the What-If Lab with exactly its levers and quantities', () => {
    const query = planToQuery([
      { leverKey: 'sca.remediateCritical', parameter: null },
      { leverKey: 'archValue.modernize', parameter: 37 },
    ]);

    expect(query).toBe('sca.remediateCritical,archValue.modernize:37');
    expect(parsePlanQuery(query)).toEqual([{ key: 'sca.remediateCritical' }, { key: 'archValue.modernize', parameter: 37 }]);
  });

  it('drops what a link should never carry', () => {
    expect(parsePlanQuery(null)).toEqual([]);
    expect(
      parsePlanQuery('mfa.enable,../../etc,archValue.modernize:0,productStrategy.invest:2.5,kenna.reduce:1:2,mfa.enable,oss.improve:9999999'),
    ).toEqual([{ key: 'mfa.enable' }]);
    expect(parsePlanQuery(Array.from({ length: 50 }, (_, i) => `lever${'abcdefghijklmnopqrstuvwxyz'[i % 26]}`).join(','))).toHaveLength(20);
  });

  it('names who carries out each step', () => {
    expect(ownerLabel('security')).toBe('Security');
    expect(ownerLabel('architecture')).toBe('Enterprise architecture');
    expect(ownerLabel('somethingNew')).toBe('somethingNew');
  });

  it('says whether the plan reaches the target, and that it is the What-If plan', () => {
    expect(planSummary(plan({ steps: [] }))).toContain('reaching the target of 90.');
    expect(planSummary(plan({ steps: [step] }))).toMatch(/^This step takes the score from 62\.32 to 90\.88/);
    expect(planSummary(plan())).toContain('same plan the What-If Lab finds');
    expect(planSummary(plan({ isReachable: false, projectedScore: 84.1 }))).toContain('cannot be reached');
    expect(planSummary(plan({ isAlreadyMet: true, baselineScore: 93 }))).toBe(
      'The score of 93.00 already reaches the target of 90: there is nothing to plan.',
    );
  });
});
