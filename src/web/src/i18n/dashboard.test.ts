import { describe, expect, it } from 'vitest';
import {
  AXIS_KEYS,
  axisLabel,
  complexityName,
  factorName,
  kpiTitle,
  levelLabel,
  opportunityUnit,
  resilienceName,
  scaleLabel,
  scaleShort,
  sourceName,
} from './dashboard';
import { axisValue } from '../components/dashboard/panels';
import type { DashboardApplication } from '../api/types';

describe('dashboard names', () => {
  it('names every indicator the backend sends', () => {
    for (const key of ['health', 'atRisk', 'candidates', 'currency', 'resilience']) {
      expect(kpiTitle(key)).not.toBe(key);
    }
  });

  it('names levels and says when there is no data', () => {
    expect(levelLabel('critical')).toBe('Critical');
    expect(levelLabel(null)).toBe('No data');
  });

  it('reads criticality and opportunity from 0 to 3', () => {
    expect(scaleLabel(3)).toBe('Very high');
    expect(scaleShort(3)).toBe('VH');
    expect(scaleShort(null)).toBe('—');
  });

  it('names the heatmap axes: the CIO is the rows, so primary capability is a column option', () => {
    expect(AXIS_KEYS).toEqual(['region', 'country', 'capability', 'disposition']);
    expect(axisLabel('capability')).toBe('Primary capability');
    expect(axisLabel('disposition')).toBe('Target disposition');
  });

  it('names complexity, obsolescence, resilience and sources, and shows unknown keys as they are', () => {
    expect(complexityName('duplicateTechnologies')).toBe('Duplicate technologies');
    expect(factorName('eosExposure')).toBe('End-of-support exposure');
    expect(resilienceName('singlePointsOfFailure')).toBe('Single points of failure');
    expect(sourceName('tlm')).toBe('TLM');
    expect(complexityName('somethingNew')).toBe('somethingNew');
  });

  it('agrees the reduction opportunity with its count', () => {
    expect(opportunityUnit('rationalize', 1)).toBe('1 duplicate application');
    expect(opportunityUnit('standardize', 8)).toBe('8 technology families');
  });
});

describe('heatmap selection', () => {
  const row = {
    region: 'LATAM',
    country: 'Chile',
    cio: null,
    disposition: 'Invest',
  } as unknown as DashboardApplication;

  it('reads the application value on the chosen axis; an empty value is "unassigned"', () => {
    expect(axisValue(row, 'region')).toBe('LATAM');
    expect(axisValue(row, 'country')).toBe('Chile');
    expect(axisValue(row, 'disposition')).toBe('Invest');
    expect(axisValue({ ...row, capability: null }, 'capability')).toBe('');
  });
});
