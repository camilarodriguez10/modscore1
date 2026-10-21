import { describe, expect, it } from 'vitest';
import { FILTERED_ROUTES, NO_FEATURES, visibleTabs } from './navigation';

const labels = (code: boolean, portfolioRoadmap: boolean, admin = false) =>
  visibleTabs({ code, portfolioRoadmap }, admin).map((tab) => tab.label);

describe('navigation', () => {
  it('no longer offers Insights, and hides Code and Roadmap while they are turned off', () => {
    expect(labels(false, false)).toEqual(['Dashboard', 'Portfolio', 'Data', 'Definitions']);
    expect(visibleTabs(NO_FEATURES, false).map((tab) => tab.label)).not.toContain('Insights');
  });

  it('offers each screen the Channel layer turns on, in its usual place', () => {
    expect(labels(true, true, true)).toEqual(['Dashboard', 'Portfolio', 'Code', 'Roadmap', 'Data', 'Definitions', 'Rules']);
    expect(labels(false, true)).toEqual(['Dashboard', 'Portfolio', 'Roadmap', 'Data', 'Definitions']);
  });

  it('keeps the filter on the portfolio screens and not on the rules', () => {
    const tabs = visibleTabs({ code: true, portfolioRoadmap: true }, true);

    expect(tabs.filter((tab) => tab.carriesFilter).map((tab) => tab.to)).toEqual(['/', '/portfolio', '/code', '/roadmap', '/data']);
    expect(FILTERED_ROUTES.has('/insights')).toBe(false);
  });
});
