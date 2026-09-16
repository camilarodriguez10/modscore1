import { useRef, useState } from 'react';
import { useRulesTimeline, useUpload } from '../api/hooks';
import type { ExpectedFileView, IngestionView, UploadFileView, UploadResultView } from '../api/types';
import { Card, Label, fmt } from '../components/primitives';
import { ScoreTrend } from '../components/ScoreTrend';
import { countOf, formatNotice } from '../i18n/notices';
import { formatDate } from './RoadmapPage';
import { ApiError } from '../api/client';

/**
 * Cargas, dentro de la pantalla de Datos.
 *
 * Sube archivos, valida, publica y recalcula. Una carga rechazada no toca el
 * portafolio vigente: la capa System valida y ensambla en un directorio temporal
 * antes de publicar nada.
 *
 * Desde la Fase 10 el archivo se reconoce por su encabezado y no por su nombre, asi
 * que ya no hay que renombrar los exports antes de subirlos. Cuando el encabezado no
 * alcanza, la pantalla muestra lo que leyo y deja asignar la fuente a mano.
 */
export function IngestionPanel({
  data,
  selectedVersion,
}: {
  data: IngestionView;
  selectedVersion: string;
}) {
  const upload = useUpload();
  const timeline = useRulesTimeline();

  // Cada version publicada es una marca en la tendencia. La de fabrica no se marca: rige
  // desde antes de la primera carga.
  const markers = (timeline.data?.versions ?? [])
    .filter((version) => !version.isBuiltIn && version.publishedAt !== null)
    .map((version) => ({
      id: version.id,
      at: version.publishedAt ?? '',
      label: version.id,
      detail: version.reason,
    }));

  const [selected, setSelected] = useState<readonly File[]>([]);
  const [label, setLabel] = useState('');
  const [assigned, setAssigned] = useState<Record<string, string>>({});
  const inputRef = useRef<HTMLInputElement>(null);

  const totalBytes = selected.reduce((sum, file) => sum + file.size, 0);
  const tooLarge = totalBytes > data.maxTotalBytes;
  const oversized = selected.filter((file) => file.size > data.maxFileBytes);
  const canSubmit = data.uploadEnabled && selected.length > 0 && !tooLarge && oversized.length === 0;

  // Los archivos que el encabezado no basto para identificar. Traen consigo lo que
  // se leyo y las fuentes posibles, que es todo lo que hace falta para preguntar.
  // `!` y no `=== null`: la capa Channel escribe los nulos, pero la capa System los
  // omite, y esta pantalla no deberia depender de cual de las dos la alimenta.
  const unidentified = (upload.data?.files ?? []).filter(
    (file) => !file.canonicalFileName && (file.candidates?.length ?? 0) > 0,
  );

  const submit = (withAssignments: boolean) => {
    if (!canSubmit) return;

    const assignments = withAssignments ? selected.map((file) => assigned[file.name] ?? '-') : undefined;

    upload.mutate(
      { files: selected, label, assignments },
      {
        onSuccess: (result) => {
          if (result.accepted) {
            setSelected([]);
            setLabel('');
            setAssigned({});
            if (inputRef.current) inputRef.current.value = '';
          }
        },
      },
    );
  };

  const reset = () => {
    setSelected([]);
    setAssigned({});
    upload.reset();
    if (inputRef.current) inputRef.current.value = '';
  };

  return (
    <div className="flex flex-col gap-4">
      {data.portfolioHistory.length > 1 && (
        <Card
          title="Portfolio trend"
          subtitle={`Average score in each upload, every point calculated with the active rules${
            timeline.data ? ` (${timeline.data.active.id})` : ''
          }. Dashed lines mark when the rules changed; the available weight explains jumps that are not real improvement.`}
        >
          <div className="px-4 py-5">
            <ScoreTrend points={data.portfolioHistory} markers={markers} />
          </div>
        </Card>
      )}

      <Card title="Upload files">
        {!data.uploadEnabled ? (
          <p className="px-4 py-5 text-[13px] text-ink-2">{formatNotice(data.disabledReason)}</p>
        ) : (
          <div className="flex flex-col gap-4 px-4 py-5">
            <div className="flex flex-col gap-1.5">
              <Label>CSV files</Label>
              <input
                ref={inputRef}
                type="file"
                multiple
                accept=".csv,text/csv"
                onChange={(event) => {
                  setSelected(Array.from(event.target.files ?? []));
                  setAssigned({});
                  upload.reset();
                }}
                className="rounded-sm border border-rule bg-surface px-3 py-2 text-[13px] text-ink-2 file:mr-3 file:rounded-sm file:border-0 file:bg-accent file:px-3 file:py-1.5 file:text-[12.5px] file:font-medium file:text-on-accent"
              />
              <p className="font-mono text-[11px] text-ink-3">
                any file name works: each file is recognized by its header · up to{' '}
                {fmt(data.maxFileBytes / 1024 / 1024, 0)} MB per file · {fmt(data.maxTotalBytes / 1024 / 1024, 0)}{' '}
                MB in total
              </p>
            </div>

            <label className="flex flex-col gap-1.5">
              <Label>Upload label</Label>
              <input
                type="text"
                value={label}
                maxLength={60}
                placeholder="Upload 0809"
                onChange={(event) => setLabel(event.target.value)}
                className="rounded-sm border border-rule bg-surface px-3 py-2 text-[13px] text-ink"
              />
            </label>

            {selected.length > 0 && (
              <ul className="flex flex-col gap-1 rounded-sm border border-rule bg-surface-2 px-3 py-2.5">
                {selected.map((file) => {
                  const isOversized = file.size > data.maxFileBytes;

                  return (
                    <li key={file.name} className="flex items-baseline justify-between gap-3 text-[12.5px]">
                      <span className={isOversized ? 'text-ink-3 line-through' : 'text-ink'}>
                        {file.name}
                        {assigned[file.name] && (
                          <span className="ml-2 font-mono text-[11px] text-accent">→ {assigned[file.name]}</span>
                        )}
                      </span>
                      <span
                        className="tnum font-mono text-[11.5px]"
                        style={{ color: isOversized ? 'var(--app-critical-text)' : 'var(--app-ink-3)' }}
                      >
                        {fmt(file.size / 1024, 0)} KB
                      </span>
                    </li>
                  );
                })}
              </ul>
            )}

            {(tooLarge || oversized.length > 0) && (
              <p className="text-[13px]" style={{ color: 'var(--app-critical-text)' }}>
                {tooLarge
                  ? `The upload weighs ${fmt(totalBytes / 1024 / 1024, 1)} MB and the maximum is ${fmt(data.maxTotalBytes / 1024 / 1024, 0)} MB.`
                  : `${oversized.length} ${oversized.length === 1 ? 'file exceeds' : 'files exceed'} the per-file maximum.`}
              </p>
            )}

            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={() => submit(Object.keys(assigned).length > 0)}
                disabled={!canSubmit || upload.isPending}
                className="rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {upload.isPending ? 'Validating and recalculating…' : 'Upload and recalculate'}
              </button>

              {selected.length > 0 && !upload.isPending && (
                <button
                  type="button"
                  onClick={reset}
                  className="text-[13px] text-ink-3 underline-offset-2 hover:underline"
                >
                  Clear selection
                </button>
              )}
            </div>

            {upload.error && (
              <p className="text-[13px]" style={{ color: 'var(--app-critical-text)' }}>
                {upload.error instanceof ApiError ? upload.error.message : 'The upload could not be completed.'}
              </p>
            )}
          </div>
        )}
      </Card>

      {upload.data && <UploadResult result={upload.data} />}

      {unidentified.length > 0 && (
        <AssignSources
          files={unidentified}
          assigned={assigned}
          onAssign={(fileName, source) => setAssigned((current) => ({ ...current, [fileName]: source }))}
          onRetry={() => submit(true)}
          pending={upload.isPending}
        />
      )}

      <Card
        title="Expected files"
        subtitle="Each file is recognized by the columns in its header, so its name can be anything."
      >
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>Source</Th>
                <Th>Canonical name</Th>
                <Th>Key column</Th>
                <Th>Required columns</Th>
              </tr>
            </thead>
            <tbody>
              {data.expectedFiles.map((file) => (
                <ExpectedRow key={file.fileName} file={file} />
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {data.versions.length > 0 && (
        <Card title="Upload history">
          <ul className="divide-y divide-rule">
            {data.versions.map((version) => (
              <li
                key={version.id}
                className="grid gap-x-4 gap-y-1 px-4 py-3 sm:grid-cols-[1fr_auto_auto] sm:items-baseline"
                style={version.id === selectedVersion ? { background: 'var(--app-accent-wash)' } : undefined}
              >
                <span className="flex items-center gap-2 text-[13.5px] text-ink">
                  {version.label}
                  {version.isCurrent && (
                    <span
                      className="rounded-sm px-1.5 py-0.5 font-mono text-[10px] font-semibold tracking-wide uppercase"
                      style={{ background: 'var(--app-accent-wash)', color: 'var(--app-accent)' }}
                    >
                      current
                    </span>
                  )}
                </span>
                <span className="tnum font-mono text-[11.5px] text-ink-3">
                  {countOf(version.applicationCount, 'application')}
                </span>
                <span className="tnum font-mono text-[11.5px] text-ink-2 sm:text-right">
                  {formatDate(version.capturedAt.slice(0, 10))}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  );
}

/**
 * Asignar a mano la fuente de los archivos que el encabezado no distingue.
 *
 * Se muestra lo que el sistema leyo de verdad, no un "no se pudo": con el
 * encabezado a la vista, quien subio el archivo reconoce en un segundo si mando el
 * export equivocado o si solo hace falta decirle cual es.
 */
function AssignSources({
  files,
  assigned,
  onAssign,
  onRetry,
  pending,
}: {
  files: readonly UploadFileView[];
  assigned: Record<string, string>;
  onAssign: (fileName: string, source: string) => void;
  onRetry: () => void;
  pending: boolean;
}) {
  const complete = files.every((file) => assigned[file.fileName]);

  return (
    <Card
      title="Assign the source of these files"
      subtitle="Their header was not enough to identify them. This is what was read from each one."
    >
      <ul className="divide-y divide-rule">
        {files.map((file) => (
          <li key={file.fileName} className="flex flex-col gap-2 px-4 py-3">
            <div className="flex flex-wrap items-baseline justify-between gap-3">
              <span className="font-mono text-[12.5px] text-ink">{file.fileName}</span>

              <label className="flex items-center gap-2">
                <Label>Source</Label>
                <select
                  value={assigned[file.fileName] ?? ''}
                  onChange={(event) => onAssign(file.fileName, event.target.value)}
                  className="rounded-sm border border-rule bg-surface px-2 py-1 text-[12.5px] text-ink"
                >
                  <option value="">Choose…</option>
                  {file.candidates.map((candidate) => (
                    <option key={candidate} value={candidate}>
                      {candidate}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            {file.detectedColumns.length > 0 ? (
              <p className="font-mono text-[11px] break-words text-ink-3">
                header read: {file.detectedColumns.join(' · ')}
              </p>
            ) : (
              <p className="text-[12px] text-ink-3">{formatNotice(file.error)}</p>
            )}
          </li>
        ))}
      </ul>

      <div className="flex items-center gap-3 border-t border-rule px-4 py-3">
        <button
          type="button"
          onClick={onRetry}
          disabled={!complete || pending}
          className="rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {pending ? 'Validating…' : 'Retry with these sources'}
        </button>
        {!complete && <span className="text-[12.5px] text-ink-3">Some files still need a source.</span>}
      </div>
    </Card>
  );
}

/** El veredicto de la carga, archivo por archivo. */
function UploadResult({ result }: { result: UploadResultView }) {
  const color = result.accepted ? 'var(--app-good-text)' : 'var(--app-critical-text)';
  const wash = result.accepted ? 'var(--app-good-wash)' : 'var(--app-critical-wash)';

  return (
    <div className="flex flex-col gap-3">
      <div
        className="rounded-md border px-4 py-3"
        style={{ borderColor: `color-mix(in srgb, ${color} 32%, transparent)`, background: wash }}
      >
        <p className="text-[13.5px] font-semibold" style={{ color }}>
          {result.accepted ? 'Upload published' : 'Upload rejected'}
        </p>
        <p className="mt-0.5 text-[13px] text-ink-2">{formatNotice(result.summary)}</p>

        {result.errors.length > 0 && (
          <ul className="mt-2 flex flex-col gap-1">
            {result.errors.map((error, index) => (
              <li key={`${error.code}-${index}`} className="text-[12.5px] text-ink-2">
                · {formatNotice(error)}
              </li>
            ))}
          </ul>
        )}
      </div>

      {result.files.length > 0 && (
        <Card title="Per-file detail">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[680px] text-[13px]">
              <thead>
                <tr className="border-b border-rule bg-surface-2">
                  <Th>File</Th>
                  <Th>Recognized source</Th>
                  <Th align="right">Rows</Th>
                  <Th align="right">Rejected</Th>
                  <Th>Result</Th>
                </tr>
              </thead>
              <tbody>
                {result.files.map((file) => (
                  <tr key={file.fileName} className="border-b border-rule last:border-0">
                    <td className="px-4 py-2.5 font-mono text-[12px] text-ink">{file.fileName}</td>
                    <td className="px-4 py-2.5 text-ink-2">{file.source}</td>
                    <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                      {file.rowCount.toLocaleString('en-US')}
                    </td>
                    <td
                      className="tnum px-4 py-2.5 text-right font-mono"
                      style={{ color: file.rejectedCount > 0 ? 'var(--app-serious-text)' : 'var(--app-ink-3)' }}
                    >
                      {file.rejectedCount}
                    </td>
                    <td className="px-4 py-2.5 text-[12.5px]">
                      {file.error ? (
                        <span style={{ color: 'var(--app-critical-text)' }}>{formatNotice(file.error)}</span>
                      ) : (
                        <span style={{ color: 'var(--app-good-text)' }}>accepted</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </div>
  );
}

function ExpectedRow({ file }: { file: ExpectedFileView }) {
  return (
    <tr className="border-b border-rule last:border-0">
      <td className="px-4 py-2.5">
        <span className="font-medium text-ink">{file.source}</span>
        {file.description && (
          <span className="mt-0.5 block max-w-[48ch] text-[12px] text-ink-3">{file.description}</span>
        )}
      </td>
      <td className="px-4 py-2.5 font-mono text-[12px] text-ink-2">{file.fileName}</td>
      <td className="px-4 py-2.5 font-mono text-[12px] text-ink-2">{file.keyColumn}</td>
      <td className="px-4 py-2.5">
        <span className="font-mono text-[11.5px] text-ink-2">{file.requiredColumns.join(', ')}</span>
        <span className="mt-0.5 block font-mono text-[11px] text-ink-3">+ {file.optionalColumnCount} optional</span>
      </td>
    </tr>
  );
}

function Th({
  children,
  align = 'left',
}: {
  children: React.ReactNode;
  align?: 'left' | 'right';
}) {
  return (
    <th
      scope="col"
      className={`px-4 py-2.5 font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase ${
        align === 'right' ? 'text-right' : 'text-left'
      }`}
    >
      {children}
    </th>
  );
}
