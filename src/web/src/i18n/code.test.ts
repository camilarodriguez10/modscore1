import { describe, expect, it } from 'vitest';
import type { AnalyticsDocument } from '../api/types';
import { codeExplanation, formatParameter, parameterLabel, constraintText } from './analytics';
import {
  STATUS_KEYS,
  actionText,
  formatDay,
  mismatchLabel,
  monthsFromNow,
  statusLabel,
  statusText,
  urgencyLabel,
  urgencyStatus,
} from './code';

const definitions = (months = 12): AnalyticsDocument => ({
  parameters: [
    { key: 'code.approachingMonths', area: 'code', kind: 'months', value: months, min: 1, max: 60, decimals: 0 },
    { key: 'code.staleMonths', area: 'code', kind: 'months', value: 12, min: 1, max: 60, decimals: 0 },
    { key: 'code.outdatedMajorsBehind', area: 'code', kind: 'count', value: 2, min: 1, max: 20, decimals: 0 },
    { key: 'code.points.eolComponent', area: 'code', kind: 'points', value: 10, min: 0, max: 100, decimals: 2 },
    { key: 'code.moderateFrom', area: 'code', kind: 'points', value: 10, min: 0.01, max: 100, decimals: 2 },
    { key: 'code.highFrom', area: 'code', kind: 'points', value: 25, min: 0.01, max: 100, decimals: 2 },
    { key: 'code.criticalFrom', area: 'code', kind: 'points', value: 50, min: 0.01, max: 100, decimals: 2 },
    { key: 'currency.legacyBelow', area: 'currency', kind: 'score', value: 60, min: 0.01, max: 100, decimals: 2 },
  ],
  areas: [{ key: 'code', status: 'provisional', owner: 'Enterprise Architecture' }],
  significantUserBase: 1000,
});

describe('code screen words', () => {
  it('names every lifecycle status from the worst, with unknown last and never colored as good', () => {
    expect(STATUS_KEYS[0]).toBe('eol');
    expect(STATUS_KEYS.at(-1)).toBe('unknown');
    expect(statusLabel('eol')).toBe('Past end of life');
    expect(statusLabel('security')).toBe('Support ended');
    expect(statusLabel('unknown')).toBe('Not assessable');
    expect(statusLabel('somethingNew')).toBe('somethingNew');
    expect(statusText('unknown')).toBe('var(--app-ink-2)');
    expect(mismatchLabel('hiddenObsolescence')).toBe('Hidden obsolescence');
  });

  it('reads a day without shifting it by the time zone', () => {
    expect(formatDay('2026-04-30')).toBe('Apr 30, 2026');
    expect(formatDay('2020-01-01')).toBe('Jan 1, 2020');
    expect(formatDay(null)).toBe('—');
  });

  it('says how long ago or how far away the end of life is', () => {
    expect(monthsFromNow(-40)).toBe('40 months ago');
    expect(monthsFromNow(1)).toBe('in 1 month');
    expect(monthsFromNow(0)).toBe('this month');
    expect(monthsFromNow(null)).toBe('');
  });

  it('reads months as months and names the code parameters and levels', () => {
    expect(formatParameter({ kind: 'months', value: 12, decimals: 0 })).toBe('12 months');
    expect(formatParameter({ kind: 'months', value: 1, decimals: 0 })).toBe('1 month');
    expect(parameterLabel('code.approachingMonths')).toBe('Ending soon within');
    expect(constraintText('code.levels')).toContain('moderate < high < critical');
  });

  it('explains the index and the lifecycle with the values in force', () => {
    const index = codeExplanation(0, definitions(18));
    expect(index.area).toBe('code');
    expect(index.rules.join(' ')).toContain('ending within 18 months');
    expect(index.rules.join(' ')).toContain('moderate from 10, high from 25, critical from 50');
    expect(index.missing.join(' ')).toContain('GitHub Enterprise integration');

    expect(codeExplanation(1, definitions(6)).rules.join(' ')).toContain('it ends within 6 months');
    expect(codeExplanation(3, definitions()).rules[0]).toContain('Currency Score is 60 or more');
  });

  it('says each improvement as an action a person can take', () => {
    const base = { ecosystem: null, targetSubject: null, target: null };
    expect(actionText({ ...base, kind: 'upgrade', subject: 'Node.js', current: '14', targetSubject: 'Node.js', target: '26' })).toBe(
      'Upgrade Node.js 14 → 26',
    );
    expect(actionText({ ...base, kind: 'migrate', subject: 'AngularJS', current: '1.8', targetSubject: 'Angular', target: '22' })).toBe(
      'Migrate AngularJS 1.8 → Angular 22',
    );
    expect(actionText({ ...base, kind: 'awaitRelease', subject: 'Spring Boot', current: '4.1' })).toBe(
      'Spring Boot 4.1: no newer supported release yet',
    );
    expect(actionText({ ...base, kind: 'replaceLibrary', subject: 'request', ecosystem: 'npm', current: '2.88.2' })).toBe(
      'Replace request 2.88.2 (deprecated)',
    );
    expect(actionText({ ...base, kind: 'maintainRepository', subject: 'org/app', current: '2025-06-01' })).toContain('Jun 1, 2025');
  });

  it('names the urgency and colors it with the lifecycle palette', () => {
    expect(urgencyLabel('now')).toBe('Now');
    expect(urgencyLabel('year2')).toBe('Within 2 years');
    expect(urgencyStatus('now')).toBe('eol');
    expect(urgencyStatus('year1')).toBe('approaching');
    expect(urgencyStatus('planned')).toBe('unknown');
  });

  it('explains the outlook, the uploads and the plan with the values in force', () => {
    expect(codeExplanation(4, definitions(18)).rules.join(' ')).toContain('ending-soon window of 18 months');
    expect(codeExplanation(5, definitions()).area).toBeNull();
    expect(codeExplanation(6, definitions()).rules.join(' ')).toContain('10 pts each');
  });
});
