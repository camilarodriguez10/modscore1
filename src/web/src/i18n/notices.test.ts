import { describe, expect, it } from 'vitest';
import {
  bandCode,
  bandLabel,
  countOf,
  formatNotice,
  groupLabel,
  rejectionHint,
  rejectionReasonText,
  sampleRowsText,
} from './notices';

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

  it('names the column of an oversized cell when the System knows it', () => {
    expect(formatNotice({ code: 'csv.fieldTooLong', args: { max: '65536', row: '5624', column: 'SUM_DSCR' } })).toBe(
      'The SUM_DSCR cell in row 5624 is longer than 65,536 characters: check that cell in the file. An unclosed quote makes it swallow the rows below it.',
    );
    expect(formatNotice({ code: 'csv.fieldTooLong', args: { max: '65536', row: '1', column: '' } })).toBe(
      'A field in row 1 is longer than 65,536 characters: the file is corrupt or is not the expected export.',
    );
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

describe('upload rejection reasons', () => {
  const invalid = (column: string, expected: string, value: string) => ({
    code: 'row.invalidValue',
    args: { column, expected, value },
  });

  it('names the column, what it expected and the first value', () => {
    expect(rejectionReasonText(invalid('User Count', 'integer', 'N/A'), 2)).toBe(
      "'User Count' expected an integer. For example, 'N/A' in row 2.",
    );
    expect(rejectionReasonText({ code: 'row.missingKey', args: { column: 'EAI_Code' } }, 5)).toBe(
      "No value in the key column 'EAI_Code'.",
    );
  });

  it('lists the first rows and how many more', () => {
    expect(sampleRowsText([2, 3, 4, 5, 6], 3869)).toBe('rows 2, 3, 4, 5, 6 and 3,864 more');
    expect(sampleRowsText([7], 1)).toBe('row 7');
    expect(sampleRowsText([7, 9], 2)).toBe('rows 7 and 9');
    expect(sampleRowsText([], 4)).toBe('');
  });

  it('recognizes the usual causes without guessing', () => {
    expect(rejectionHint(invalid('Kenna_Score', 'integer', '712.5'))).toMatch(/whole numbers/);
    expect(rejectionHint(invalid('User Count', 'integer', '1.234'))).toMatch(/thousands separator/);
    expect(rejectionHint(invalid('User Count', 'integer', 'Internal'))).toMatch(/wrong column/);
    expect(rejectionHint({ code: 'row.duplicateKey', args: { column: 'EAI_Code', key: '14381' } })).toMatch(/one row per date/);
    expect(rejectionHint(invalid('Internet_Facing', 'boolean', 'maybe'))).toMatch(/TRUE\/FALSE/);
    expect(rejectionHint(invalid('Kenna_Score', 'integer', '1-100'))).toBeNull();
  });
});
