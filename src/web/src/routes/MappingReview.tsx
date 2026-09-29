import type { MappingFieldView, MappingFileView, MappingReviewView } from '../api/types';
import { Card, Label } from '../components/primitives';
import { formatNotice } from '../i18n/notices';
import {
  decisionProblems,
  decisionSummary,
  effectiveMatch,
  matchLabel,
  matchTone,
  typeLabel,
  unusedHeaders,
  type MappingDecision,
  type MappingDecisions,
} from '../mapping';

/**
 * El mapping de columnas, antes de cargar.
 *
 * Para cada archivo, cada campo de la base con la columna del archivo que lo va a cargar.
 * La capa System propone por nombre, por alias o como en la carga anterior; quien carga
 * confirma o elige otra. Solo se leyó la línea de encabezado de cada archivo: ninguna fila
 * de datos salió del navegador.
 */
export function MappingReview({
  review,
  decisions,
  onChange,
  onRecheck,
  onUpload,
  onCancel,
  checking,
  uploading,
}: {
  review: MappingReviewView;
  decisions: MappingDecisions;
  onChange: (fileName: string, decision: MappingDecision) => void;
  onRecheck: () => void;
  onUpload: () => void;
  onCancel: () => void;
  checking: boolean;
  uploading: boolean;
}) {
  const blocked = review.files.some((file) => decisionProblems(file, decisions[file.fileName]).length > 0);

  return (
    <Card
      title="Column mapping"
      subtitle="Which column of each file loads each field of the database. Only the header row of each file was read: no data leaves this browser until you upload."
    >
      <div className="flex flex-col divide-y divide-rule">
        {review.files.map((file) => (
          <FileMapping
            key={file.fileName}
            file={file}
            decision={decisions[file.fileName]}
            onChange={(decision) => onChange(file.fileName, decision)}
          />
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-3 border-t border-rule px-4 py-3">
        <button
          type="button"
          onClick={onUpload}
          disabled={blocked || uploading || checking}
          className="rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {uploading ? 'Validating and recalculating…' : 'Upload with this mapping'}
        </button>
        <button
          type="button"
          onClick={onRecheck}
          disabled={checking || uploading}
          className="rounded-sm border border-rule px-3 py-2 text-[13px] text-ink-2 hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {checking ? 'Checking…' : 'Check again'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          disabled={uploading}
          className="text-[13px] text-ink-3 underline-offset-2 hover:underline"
        >
          Close
        </button>
        {blocked && <span className="text-[12.5px] text-ink-3">Fix the problems marked above to upload.</span>}
      </div>
    </Card>
  );
}

function FileMapping({
  file,
  decision,
  onChange,
}: {
  file: MappingFileView;
  decision: MappingDecision | undefined;
  onChange: (decision: MappingDecision) => void;
}) {
  const problems = decisionProblems(file, decision);
  const identified = file.canonicalFileName !== null;
  const summary = decisionSummary(file, decision);
  const unused = unusedHeaders(file, decision);

  const choose = (column: string, header: string | null) =>
    onChange({ source: decision?.source ?? file.canonicalFileName, columns: { ...decision?.columns, [column]: header } });

  return (
    <section aria-label={`Column mapping of ${file.fileName}`} className="flex flex-col gap-3 px-4 py-4">
      <header className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h3 className="flex flex-wrap items-baseline gap-x-2 text-[13.5px]">
          <span className="font-mono text-[12.5px] text-ink">{file.fileName}</span>
          <span aria-hidden="true" className="text-ink-3">
            →
          </span>
          <span className="font-semibold text-ink">{identified ? file.source : 'Source not recognized'}</span>
        </h3>
        {identified && (
          <span className="tnum font-mono text-[11.5px] text-ink-3">
            {summary.loaded} fields loaded · {summary.notLoaded} not in this file
          </span>
        )}
      </header>

      {file.description && <p className="max-w-[80ch] text-[12.5px] text-ink-3">{file.description}</p>}

      {!identified && (
        <div className="flex flex-col gap-2">
          <p className="text-[12.5px] text-ink-2">{formatNotice(file.problem)}</p>
          {file.candidates.length > 0 && (
            <label className="flex flex-wrap items-center gap-2">
              <Label>Source</Label>
              <select
                value={decision?.source ?? ''}
                onChange={(event) => onChange({ source: event.target.value || null, columns: {} })}
                className="rounded-sm border border-rule bg-surface px-2 py-1 text-[12.5px] text-ink"
              >
                <option value="">Choose…</option>
                {file.candidates.map((candidate) => (
                  <option key={candidate} value={candidate}>
                    {candidate}
                  </option>
                ))}
              </select>
              <span className="text-[12px] text-ink-3">then press “Check again” to map its columns.</span>
            </label>
          )}
          {file.headers.length > 0 && (
            <p className="font-mono text-[11px] break-words text-ink-3">header read: {file.headers.join(' · ')}</p>
          )}
        </div>
      )}

      {problems.length > 0 && (
        <ul className="flex flex-col gap-1" aria-label={`Problems in ${file.fileName}`}>
          {problems.map((problem) => (
            <li key={problem} className="text-[12.5px]" style={{ color: 'var(--app-critical-text)' }}>
              · {problem}
            </li>
          ))}
        </ul>
      )}

      {identified && file.problems.length > 0 && problems.length === 0 && (
        <ul className="flex flex-col gap-1">
          {file.problems.map((problem, index) => (
            <li key={`${problem.code}-${index}`} className="text-[12.5px]" style={{ color: 'var(--app-serious-text)' }}>
              · {formatNotice(problem)}
            </li>
          ))}
        </ul>
      )}

      {identified && (
        <div className="overflow-x-auto rounded-md border border-rule">
          <table className="w-full min-w-[720px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>Field in the database</Th>
                <Th>Column in your file</Th>
                <Th>How it was decided</Th>
              </tr>
            </thead>
            <tbody>
              {file.fields.map((field) => (
                <FieldRow
                  key={field.column}
                  field={field}
                  headers={file.headers}
                  chosen={decision?.columns[field.column]}
                  onChoose={(header) => choose(field.column, header)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {identified && unused.length > 0 && (
        <p className="font-mono text-[11px] break-words text-ink-3">
          columns of the file that load nothing: {unused.join(' · ')}
        </p>
      )}
    </section>
  );
}

function FieldRow({
  field,
  headers,
  chosen,
  onChoose,
}: {
  field: MappingFieldView;
  headers: readonly string[];
  chosen: string | null | undefined;
  onChoose: (header: string | null) => void;
}) {
  const value = chosen === undefined ? field.header : chosen;
  const match = effectiveMatch(field, value);
  const tone = matchTone(match, field.required);

  return (
    <tr className="border-b border-rule align-top last:border-0">
      <td className="px-4 py-2.5">
        <span className="flex flex-wrap items-center gap-1.5">
          <span className="font-mono text-[12.5px] text-ink">{field.column}</span>
          {field.isKey && <Badge>key</Badge>}
          {field.required && !field.isKey && <Badge>required</Badge>}
        </span>
        <span className="mt-0.5 block text-[11.5px] text-ink-3">
          {typeLabel(field.type)}
          {field.feeds.length > 0 && ` · feeds ${field.feeds.join(', ')}`}
        </span>
      </td>
      <td className="px-4 py-2.5">
        <select
          aria-label={`Column for ${field.column}`}
          value={value ?? ''}
          onChange={(event) => onChoose(event.target.value === '' ? null : event.target.value)}
          className="w-full max-w-[32ch] rounded-sm border border-rule bg-surface px-2 py-1 font-mono text-[12px] text-ink"
        >
          <option value="">— not in this file —</option>
          {headers.map((header) => (
            <option key={header} value={header}>
              {header}
            </option>
          ))}
        </select>
      </td>
      <td className="px-4 py-2.5">
        <MatchChip tone={tone}>{matchLabel(match, field.required)}</MatchChip>
      </td>
    </tr>
  );
}

const TONES = {
  good: { color: 'var(--app-good-text)', background: 'var(--app-good-wash)' },
  accent: { color: 'var(--app-accent)', background: 'var(--app-accent-wash)' },
  muted: { color: 'var(--app-ink-3)', background: 'var(--app-surface-2)' },
  critical: { color: 'var(--app-critical-text)', background: 'var(--app-critical-wash)' },
} as const;

function MatchChip({ tone, children }: { tone: keyof typeof TONES; children: React.ReactNode }) {
  return (
    <span className="inline-block rounded-sm px-1.5 py-0.5 text-[11.5px] font-medium whitespace-nowrap" style={TONES[tone]}>
      {children}
    </span>
  );
}

function Badge({ children }: { children: React.ReactNode }) {
  return (
    <span className="rounded-sm border border-rule px-1 font-mono text-[10px] tracking-wide text-ink-3 uppercase">
      {children}
    </span>
  );
}

function Th({ children }: { children: React.ReactNode }) {
  return (
    <th
      scope="col"
      className="px-4 py-2.5 text-left font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase"
    >
      {children}
    </th>
  );
}
