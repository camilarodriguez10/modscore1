import { describe, expect, it } from 'vitest';
import { bandCode, bandLabel, countOf, formatNotice, groupLabel } from './notices';

describe('formatNotice', () => {
  it('writes the sentence from the code and its data', () => {
    expect(formatNotice({ code: 'exclusion.notReported', args: { system: 'CMT' } })).toBe('Not reported by CMT');
    expect(formatNotice({ code: 'exclusion.notReported', args: {} })).toBe('Not reported');
  });

  it('explains a zero excluded by the BRD rule (ADR 0019)', () => {
    expect(formatNotice({ code: 'exclusion.zeroValue', args: { system: 'RPT' } })).toBe(
      'RPT reported 0, which the BRD excludes from scoring like a missing value',
    );
  });

  it('agrees singular and plural', () => {
    expect(formatNotice({ code: 'value.scaFlaws', args: { severity: 'High', count: '1' } })).toBe('1 open High flaw');
    expect(formatNotice({ code: 'value.scaFlaws', args: { severity: 'High', count: '3' } })).toBe('3 open High flaws');
    expect(formatNotice({ code: 'value.scaFlaws', args: { severity: 'High', count: '0' } })).toBe('No open High flaws');
  });

  it('shows an unknown code instead of breaking the screen', () => {
    expect(formatNotice({ code: 'something.new', args: {} })).toBe('Unrecognized message (something.new)');
  });

  it('prefixes the file when the notice carries one', () => {
    const text = formatNotice({ code: 'exclusion.notReported', args: { system: 'EAI', file: 'eai_master.csv' } });
    expect(text).toBe('eai_master.csv: Not reported by EAI');
  });

  it('returns nothing for a missing notice', () => {
    expect(formatNotice(null)).toBe('');
    expect(formatNotice(undefined)).toBe('');
  });
});

describe('insights notices', () => {
  it('writes the evidence of a modernization candidate', () => {
    expect(formatNotice({ code: 'insights.criticalApplication', args: {} })).toBe('Critical application');
    expect(formatNotice({ code: 'insights.largeUserBase', args: { users: '12000' } })).toBe('12,000 users');
    expect(formatNotice({ code: 'insights.largeUserBase', args: { users: '1' } })).toBe('1 user');
    expect(formatNotice({ code: 'insights.upliftFromRoadmap', args: { uplift: '6.5', weeks: '12' } })).toBe(
      'Its roadmap adds 6.50 points over 12 weeks',
    );
  });

  it('names a concentration with its own numbers', () => {
    expect(
      formatNotice({
        code: 'insights.duplicateCapability',
        args: { capability: 'Policy Servicing', country: 'Chile', count: '4' },
      }),
    ).toBe('4 applications cover \u201cPolicy Servicing\u201d in Chile');

    expect(
      formatNotice({
        code: 'insights.languageSpread',
        args: { capability: 'Claims', languages: 'Java, COBOL, C#', count: '3' },
      }),
    ).toBe('\u201cClaims\u201d is written in 3 languages: Java, COBOL, C#');

    expect(formatNotice({ code: 'insights.decommissionBacklog', args: { count: '1' } })).toBe(
      '1 application marked Eliminate is still active',
    );
    expect(formatNotice({ code: 'insights.decommissionBacklog', args: { count: '7' } })).toBe(
      '7 applications marked Eliminate are still active',
    );
  });

  it('names a resilience gap without inventing a value', () => {
    expect(formatNotice({ code: 'insights.gapDisasterRecovery', args: {} })).toBe(
      'Out of the disaster recovery scope',
    );
    expect(formatNotice({ code: 'insights.gapMonitoring', args: {} })).toBe('No monitoring reported');
    expect(formatNotice({ code: 'insights.gapOperational', args: { score: '41.5' } })).toBe(
      'Operational score 41.50',
    );
    expect(formatNotice({ code: 'insights.gapRecoveryObjectives', args: {} })).toBe('No RTO or RPO declared');
  });
});

describe('counts and bands', () => {
  it('formats counts in English with agreement', () => {
    expect(countOf(1, 'application')).toBe('1 application');
    expect(countOf(1250, 'application')).toBe('1,250 applications');
    expect(countOf(2, 'match', 'matches')).toBe('2 matches');
  });

  it('names band codes and keeps unknown ones', () => {
    expect(bandLabel('at-risk')).toBe('At risk');
    expect(bandLabel('whatever')).toBe('whatever');
  });

  it('turns Phase 10 band names into codes', () => {
    expect(bandCode('Critico')).toBe('critical');
    expect(bandCode(' en riesgo ')).toBe('at-risk');
    expect(bandCode('solid')).toBe('solid');
  });

  it('names the empty group', () => {
    expect(groupLabel('')).toBe('Unassigned');
    expect(groupLabel('Chile')).toBe('Chile');
  });
});
