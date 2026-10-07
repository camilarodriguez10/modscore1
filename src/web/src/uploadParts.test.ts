import { describe, expect, it } from 'vitest';
import type { UploadFileView } from './api/types';
import { partLabels } from './uploadParts';

const file = (fileName: string, canonicalFileName: string | null): UploadFileView => ({
  fileName,
  canonicalFileName,
  source: canonicalFileName ?? '-',
  bytes: 10,
  rowCount: 1,
  rejectedCount: 0,
  error: null,
  detectedColumns: [],
  candidates: [],
  rejectionReasons: [],
  ignoredValues: [],
});

describe('partLabels', () => {
  it('numbers the files of a source that arrived in several, and only those', () => {
    expect(
      partLabels([
        file('master.csv', 'eai_master.csv'),
        file('APIM_EMEA.csv', 'apim_onboarded_eais.csv'),
        file('notes.csv', null),
        file('APIM_US.xlsx', 'apim_onboarded_eais.csv'),
        file('APIM_LATAM.csv', 'apim_onboarded_eais.csv'),
      ]),
    ).toEqual([null, 'file 1 of 3', null, 'file 2 of 3', 'file 3 of 3']);
  });
});
