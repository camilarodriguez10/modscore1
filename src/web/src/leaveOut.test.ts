import { describe, expect, it } from 'vitest';
import type { UploadFileView, UploadResultView } from './api/types';
import { planLeaveOut, type UploadPayload } from './leaveOut';

function fileView(fileName: string, canonicalFileName: string | null, source: string, failed = false): UploadFileView {
  return {
    fileName,
    canonicalFileName,
    source,
    bytes: 10,
    rowCount: failed ? 0 : 5,
    rejectedCount: 0,
    error: failed ? { code: 'csv.fieldTooLong', args: { max: '4096', row: '527' } } : null,
    detectedColumns: [],
    candidates: [],
    rejectionReasons: [],
    ignoredValues: [],
  };
}

function rejected(files: UploadFileView[]): UploadResultView {
  return {
    accepted: false,
    version: null,
    applicationCount: 0,
    files,
    errors: [],
    summary: { code: 'upload.rejected', args: {} },
  };
}

const csv = (name: string) => new File(['a,b\n'], name, { type: 'text/csv' });

const sent: UploadPayload = {
  files: [csv('containers.csv'), csv('eai.csv'), csv('sca.csv'), csv('debt.csv')],
  label: 'Upload 1005',
  assignments: ['-', 'eai_master.csv', 'veracode_sca_open_flaws.csv', '-'],
  columnMaps: [null, { 'User Count': 'Users' }, null, null],
};

describe('planLeaveOut', () => {
  it('leaves out the files with problems and keeps the source and mapping of the rest aligned', () => {
    const plan = planLeaveOut(
      rejected([
        fileView('containers.csv', null, '-', true),
        fileView('eai.csv', 'eai_master.csv', 'EAI Master'),
        fileView('sca.csv', 'veracode_sca_open_flaws.csv', 'SCA Flaws', true),
        fileView('debt.csv', 'tech_debt.csv', 'Tech Debt'),
      ]),
      sent,
    );

    expect(plan?.leftOut).toEqual([
      { fileName: 'containers.csv', source: null, partial: false, copyOf: null },
      { fileName: 'sca.csv', source: 'SCA Flaws', partial: false, copyOf: null },
    ]);
    expect(plan?.payload.files.map((file) => file.name)).toEqual(['eai.csv', 'debt.csv']);
    expect(plan?.payload.assignments).toEqual(['eai_master.csv', '-']);
    expect(plan?.payload.columnMaps).toEqual([{ 'User Count': 'Users' }, null]);
    expect(plan?.payload.label).toBe('Upload 1005');
  });

  it('says when the source stays loaded from its other files', () => {
    const regions: UploadPayload = {
      files: [csv('eai.csv'), csv('APIM_EMEA.csv'), csv('APIM_US.xlsx')],
      label: 'Upload 1007',
    };

    const plan = planLeaveOut(
      rejected([
        fileView('eai.csv', 'eai_master.csv', 'EAI Master'),
        fileView('APIM_EMEA.csv', 'apim_onboarded_eais.csv', 'APIM Onboarding'),
        fileView('APIM_US.xlsx', 'apim_onboarded_eais.csv', 'APIM Onboarding', true),
      ]),
      regions,
    );

    expect(plan?.leftOut).toEqual([{ fileName: 'APIM_US.xlsx', source: 'APIM Onboarding', partial: true, copyOf: null }]);
    expect(plan?.payload.files.map((file) => file.name)).toEqual(['eai.csv', 'APIM_EMEA.csv']);
  });

  it('says when the file left out is a copy of one that is loaded', () => {
    const copy = { ...fileView('APIM_EMEA (1).csv', null, '-', true), error: { code: 'file.identical', args: { other: 'APIM_EMEA.csv' } } };

    const plan = planLeaveOut(
      rejected([fileView('eai.csv', 'eai_master.csv', 'EAI Master'), fileView('APIM_EMEA.csv', 'apim_onboarded_eais.csv', 'APIM Onboarding'), copy]),
      { files: [csv('eai.csv'), csv('APIM_EMEA.csv'), csv('APIM_EMEA (1).csv')], label: '' },
    );

    expect(plan?.leftOut).toEqual([{ fileName: 'APIM_EMEA (1).csv', source: null, partial: false, copyOf: 'APIM_EMEA.csv' }]);
  });

  it('offers nothing when the EAI Master is the file that failed', () => {
    const plan = planLeaveOut(
      rejected([
        fileView('containers.csv', null, '-'),
        fileView('eai.csv', 'eai_master.csv', 'EAI Master', true),
        fileView('sca.csv', 'veracode_sca_open_flaws.csv', 'SCA Flaws'),
        fileView('debt.csv', 'tech_debt.csv', 'Tech Debt'),
      ]),
      sent,
    );

    expect(plan).toBeNull();
  });

  it('offers nothing when no file failed, the upload was published or the verdicts do not match what was sent', () => {
    const allPassed = [
      fileView('containers.csv', 'monitoreo.csv', 'Monitoring'),
      fileView('eai.csv', 'eai_master.csv', 'EAI Master'),
      fileView('sca.csv', 'veracode_sca_open_flaws.csv', 'SCA Flaws'),
      fileView('debt.csv', 'tech_debt.csv', 'Tech Debt'),
    ];

    expect(planLeaveOut(rejected(allPassed), sent)).toBeNull();
    expect(planLeaveOut({ ...rejected(allPassed), accepted: true }, sent)).toBeNull();
    expect(planLeaveOut(rejected([]), sent)).toBeNull();
  });
});
