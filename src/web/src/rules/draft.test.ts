import { describe, expect, it } from 'vitest';
import type { RulesDocument } from '../api/types';
import { countChanges, draftFrom, editorFrom, loadEditor, parse, saveEditor, type DraftStorage } from './draft';

const document: RulesDocument = {
  version: 'v1.3',
  floor: 20,
  range: 80,
  pillars: [
    { key: 'risk', name: 'Modernization Risk Vectors', nominalWeight: 15 },
    { key: 'architecture', name: 'Architecture & Maturity', nominalWeight: 10 },
  ],
  metrics: [
    {
      key: 'kenna',
      name: 'Kenna Score',
      pillar: 'risk',
      weight: 8,
      kind: 'numericBands',
      bands: [
        { min: 0, max: 339, score: 100, label: 'Low' },
        { min: 340, max: 669, score: 60, label: 'Medium' },
      ],
      cases: null,
      categoryScores: null,
      sourceSystem: 'CMT',
      note: null,
    },
    {
      key: 'mfa',
      name: 'MFA',
      pillar: 'risk',
      weight: 7,
      kind: 'categorical',
      bands: null,
      cases: { 'Yes (with users)': 100, 'No (with users)': 20 },
      categoryScores: null,
      sourceSystem: 'EAI',
      note: null,
    },
    {
      key: 'archValue',
      name: 'Architectural Value',
      pillar: 'architecture',
      weight: 10,
      kind: 'identity',
      bands: null,
      cases: null,
      categoryScores: { modern: 100, heritage: 60 },
      sourceSystem: 'CMT',
      note: null,
    },
  ],
  levers: [
    {
      key: 'mfa.enable',
      name: 'Enable MFA',
      action: 'Enable multi-factor authentication',
      metricKey: 'mfa',
      pillarKey: 'risk',
      enabled: true,
      isParameterized: false,
      defaultParameter: null,
      usedBy: ['Refactor'],
    },
    {
      key: 'arch.modernize',
      name: 'Modernize products',
      action: 'Move products to modern',
      metricKey: 'archValue',
      pillarKey: 'architecture',
      enabled: true,
      isParameterized: true,
      defaultParameter: 3,
      usedBy: ['Refactor'],
    },
  ],
  roadmap: {
    healthyScore: 70,
    significantUserBase: 500,
    roadmaps: [
      {
        key: 'Refactor',
        name: 'Refactor',
        disposition: 'Migrate',
        removesFromPortfolio: false,
        levers: ['arch.modernize', 'mfa.enable'],
        milestones: [
          { name: 'Design', owner: 'Architecture', weeks: 4 },
          { name: 'Build', owner: 'Team', weeks: 12 },
        ],
      },
    ],
  },
};

const baseline = () => draftFrom(editorFrom(document, 'v1.3'), document).draft;

describe('parse', () => {
  it('reads finite numbers and nothing else', () => {
    expect(parse(' 12.5 ')).toBe(12.5);
    expect(parse('0')).toBe(0);
    expect(parse('')).toBeNull();
    expect(parse('   ')).toBeNull();
    expect(parse('abc')).toBeNull();
    expect(parse('Infinity')).toBeNull();
  });
});

describe('draftFrom', () => {
  it('round-trips the document without invalid fields or changes', () => {
    const { draft, invalid } = draftFrom(editorFrom(document, 'v1.3'), document);

    expect(invalid.size).toBe(0);
    expect(countChanges(draft, baseline())).toBe(0);
    expect(draft.metrics.map((m) => m.weight)).toEqual([8, 7, 10]);
    expect(draft.levers.find((l) => l.key === 'mfa.enable')?.defaultParameter).toBeNull();
    expect(draft.levers.find((l) => l.key === 'arch.modernize')?.defaultParameter).toBe(3);
  });

  it('counts one change per edited value', () => {
    const editor = editorFrom(document, 'v1.3');
    editor.metrics.kenna!.weight = '5';
    editor.metrics.mfa!.cases!['No (with users)'] = '40';

    expect(countChanges(draftFrom(editor, document).draft, baseline())).toBe(2);
  });

  it('marks text that is not a number as invalid, by field', () => {
    const editor = editorFrom(document, 'v1.3');
    editor.metrics.kenna!.weight = '8,5';
    editor.metrics.kenna!.bands![1]!.score = '';

    const { invalid } = draftFrom(editor, document);

    expect([...invalid].sort()).toEqual(['kenna.band1.score', 'kenna.weight']);
  });

  it('requires whole numbers for weeks, user base and lever quantities', () => {
    const editor = editorFrom(document, 'v1.3');
    editor.paths.Refactor!.weeks[1] = '2.5';
    editor.userBase = '10.1';
    editor.levers['arch.modernize']!.defaultParameter = '1.5';

    const { invalid } = draftFrom(editor, document);

    expect([...invalid].sort()).toEqual(['Refactor.week1', 'arch.modernize.parameter', 'userBase']);
  });

  it('does not count reordering the levers of a roadmap as a change', () => {
    const editor = editorFrom(document, 'v1.3');
    editor.paths.Refactor!.levers = ['mfa.enable', 'arch.modernize'];

    expect(countChanges(draftFrom(editor, document).draft, baseline())).toBe(0);
  });

  it('takes the structure from the document, never from a saved editor', () => {
    const editor = editorFrom(document, 'v1.3');
    editor.metrics['invented'] = { weight: '50', bands: null, cases: null, categories: null };

    const { draft } = draftFrom(editor, document);

    expect(draft.metrics.map((m) => m.key)).toEqual(['kenna', 'mfa', 'archValue']);
  });
});

describe('saved editor', () => {
  function memoryStorage(): DraftStorage & { values: Map<string, string> } {
    const values = new Map<string, string>();

    return {
      values,
      getItem: (key) => values.get(key) ?? null,
      setItem: (key, value) => void values.set(key, value),
      removeItem: (key) => void values.delete(key),
    };
  }

  it('survives a round trip and is removed when cleared', () => {
    const storage = memoryStorage();
    const editor = editorFrom(document, 'v1.3', 'v1.2');

    saveEditor(editor, storage);
    expect(loadEditor(storage)).toEqual(editor);

    saveEditor(null, storage);
    expect(loadEditor(storage)).toBeNull();
  });

  it('ignores corrupt content and blocked storage', () => {
    const corrupt = memoryStorage();
    corrupt.values.set('modscore.rulesDraft', '{not json');
    expect(loadEditor(corrupt)).toBeNull();

    const blocked: DraftStorage = {
      getItem: () => {
        throw new Error('blocked');
      },
      setItem: () => {
        throw new Error('blocked');
      },
      removeItem: () => {
        throw new Error('blocked');
      },
    };

    expect(loadEditor(blocked)).toBeNull();
    expect(() => saveEditor(editorFrom(document, 'v1.3'), blocked)).not.toThrow();
  });
});
