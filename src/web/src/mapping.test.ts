import { describe, expect, it } from 'vitest';
import type { MappingFileView } from './api/types';
import {
  columnMapToSend,
  decisionProblems,
  effectiveMatch,
  initialDecisions,
  matchLabel,
  readHeaderLine,
  unusedHeaders,
} from './mapping';

const file: MappingFileView = {
  fileName: 'export 0928.csv',
  canonicalFileName: 'eai_master.csv',
  source: 'EAI Master',
  description: null,
  problem: null,
  candidates: [],
  headers: ['EAI_Code', 'Total Users', 'Users', 'Kenna Score', 'Tech Score (raw)'],
  fields: [
    { column: 'EAI_Code', type: 'text', required: true, isKey: true, header: 'EAI_Code', match: 'name', feeds: [] },
    { column: 'User Count', type: 'integer', required: false, isKey: false, header: 'Users', match: 'alias', feeds: [] },
    { column: 'Kenna_Score', type: 'integer', required: false, isKey: false, header: 'Kenna Score', match: 'alias', feeds: ['Kenna Score'] },
    { column: 'Raw_Tech_Score', type: 'number', required: false, isKey: false, header: 'Tech Score (raw)', match: 'remembered', feeds: [] },
    { column: 'Operational_Score', type: 'number', required: false, isKey: false, header: null, match: 'missing', feeds: [] },
  ],
  unusedHeaders: ['Total Users'],
  problems: [],
};

describe('column mapping', () => {
  it('reads only the header line, without the BOM', async () => {
    const blob = new Blob(['﻿EAI_Code,Users\r\n1001,secret\n1002,data']);
    await expect(readHeaderLine(blob)).resolves.toBe('EAI_Code,Users');
  });

  it('starts from what the System layer proposed', () => {
    const decisions = initialDecisions({ files: [file] });
    expect(decisions['export 0928.csv']).toEqual({
      source: 'eai_master.csv',
      columns: {
        EAI_Code: 'EAI_Code',
        'User Count': 'Users',
        Kenna_Score: 'Kenna Score',
        Raw_Tech_Score: 'Tech Score (raw)',
        Operational_Score: null,
      },
    });
  });

  it('a different column is a choice; none is "not in this file"', () => {
    const userCount = file.fields[1]!;
    expect(effectiveMatch(userCount, 'Users')).toBe('alias');
    expect(effectiveMatch(userCount, 'Total Users')).toBe('manual');
    expect(effectiveMatch(userCount, null)).toBe('excluded');
    expect(matchLabel('excluded', false)).toBe('Not in this file');
    expect(matchLabel('missing', true)).toBe('Required: choose a column');
  });

  it('sends only the decisions, not what was recognized on its own', () => {
    const decision = {
      ...initialDecisions({ files: [file] })['export 0928.csv']!,
    };
    const changed = { ...decision, columns: { ...decision.columns, 'User Count': 'Total Users', Kenna_Score: null } };

    expect(columnMapToSend(file, changed)).toEqual({
      'User Count': 'Total Users',
      Kenna_Score: null,
      Raw_Tech_Score: 'Tech Score (raw)',
    });
    expect(unusedHeaders(file, changed)).toEqual(['Users', 'Kenna Score']);
  });

  it('warns before uploading about a missing key or a column chosen twice', () => {
    const decision = initialDecisions({ files: [file] })['export 0928.csv']!;

    expect(decisionProblems(file, decision)).toEqual([]);
    expect(decisionProblems(file, { ...decision, columns: { ...decision.columns, EAI_Code: null } })).toEqual([
      "'EAI_Code' is required: choose the column of the file that holds it.",
    ]);
    expect(
      decisionProblems(file, { ...decision, columns: { ...decision.columns, Kenna_Score: 'Users' } }),
    ).toEqual(["The column 'Users' is chosen for both 'User Count' and 'Kenna_Score'."]);
    expect(decisionProblems({ ...file, canonicalFileName: null }, { source: null, columns: {} })).toEqual([
      'Choose the source of this file.',
    ]);
  });
});
