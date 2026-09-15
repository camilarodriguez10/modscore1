import { describe, expect, it } from 'vitest';
import { filterSearch, filterValueLabel, readFilters, toApiFilters } from './filters';

describe('readFilters', () => {
  it('reads only the known filters', () => {
    const filters = readFilters(new URLSearchParams('country=Mexico&lob=Individual&page=3&evil=1'));

    expect(filters).toEqual({ country: 'Mexico', lob: 'Individual' });
  });

  it('turns a legacy band name into its code (ADR 0016, Phase 11)', () => {
    expect(readFilters(new URLSearchParams('band=Critico')).band).toBe('critical');
  });

  it('ignores empty values', () => {
    expect(readFilters(new URLSearchParams('country=&cio=C.%20Duarte'))).toEqual({ cio: 'C. Duarte' });
  });
});

describe('filterSearch', () => {
  it('builds a shareable query in a stable order', () => {
    expect(filterSearch({ lob: 'Individual', country: 'Mexico' })).toBe('?country=Mexico&lob=Individual');
    expect(filterSearch({})).toBe('');
  });

  it('encodes values', () => {
    expect(filterSearch({ cio: 'A & B' })).toBe('?cio=A+%26+B');
  });
});

describe('toApiFilters', () => {
  it('maps the critical flag and keeps paging', () => {
    expect(toApiFilters({ critical: 'true', band: 'solid' }, 2, 50)).toMatchObject({
      criticalOnly: true,
      band: 'solid',
      page: 2,
      pageSize: 50,
    });

    expect(toApiFilters({ critical: 'false' }).criticalOnly).toBeUndefined();
  });
});

describe('filterValueLabel', () => {
  it('reads codes the way the screen shows them', () => {
    expect(filterValueLabel('band', 'at-risk')).toBe('At risk');
    expect(filterValueLabel('critical', 'true')).toBe('critical only');
    expect(filterValueLabel('country', 'Chile')).toBe('Chile');
  });
});
