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
