import { useRef, useState } from 'react';
import { useMappingReview, usePull, useRulesTimeline, useTestConnections, useUpload } from '../api/hooks';
import type {
  ConnectorsView,
  ExpectedFileView,
  IngestionView,
  UploadFileView,
  UploadRejectionView,
  UploadResultView,
} from '../api/types';
import { Card, Label, fmt } from '../components/primitives';
import { ScoreTrend } from '../components/ScoreTrend';
import { countOf, formatNotice, rejectionHint, rejectionReasonText, sampleRowsText } from '../i18n/notices';
import { formatDate } from './RoadmapPage';
import { ApiError } from '../api/client';
import { MappingReview } from './MappingReview';
import { columnMapToSend, initialDecisions, readHeaderLine, type MappingDecisions } from '../mapping';
import { planLeaveOut, type LeaveOutPlan, type UploadPayload } from '../leaveOut';
import { partLabels } from '../uploadParts';

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
 *
 * Desde el 2026-10-07 (ADR 0034) acepta libros de Excel, y varios archivos de una misma
 * fuente —uno por región, por ejemplo— se cargan juntos.
 */
export function IngestionPanel({
  data,
  selectedVersion,
}: {
  data: IngestionView;
  selectedVersion: string;
}) {
  const upload = useUpload();
  const review = useMappingReview();
  const timeline = useRulesTimeline();
  const [decisions, setDecisions] = useState<MappingDecisions>({});
  const [headerError, setHeaderError] = useState<string | null>(null);

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
  // Lo último que se mandó, para poder repetirlo sin los archivos que fallaron.
  const [lastSent, setLastSent] = useState<UploadPayload | null>(null);
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

  // Si la carga se rechazó solo por algunos archivos, lo que queda sin ellos.
  const leaveOut = upload.data && lastSent ? planLeaveOut(upload.data, lastSent) : null;

  const submit = (withAssignments: boolean) => {
    if (!canSubmit) return;

    const assignments = withAssignments ? selected.map((file) => assigned[file.name] ?? '-') : undefined;
    const payload: UploadPayload = { files: selected, label, assignments };
    setLastSent(payload);

    upload.mutate(payload, {
      onSuccess: (result) => {
        if (result.accepted) {
          setSelected([]);
          setLabel('');
          setAssigned({});
          if (inputRef.current) inputRef.current.value = '';
        }
      },
    });
  };

  const reset = () => {
    setSelected([]);
    setAssigned({});
    setLastSent(null);
    upload.reset();
    closeReview();
    if (inputRef.current) inputRef.current.value = '';
  };

  const closeReview = () => {
    review.reset();
    setDecisions({});
    setHeaderError(null);
  };

  /**
   * Revisa el mapping: lee en el navegador solo la primera línea de cada archivo y pide a
   * la capa System qué columna cargaría cada campo. Con `current`, vuelve a revisar con lo
   * que la persona ya decidió.
   */
  const startReview = async (current?: MappingDecisions) => {
    if (!canSubmit) return;

    const headers: string[] = [];

    for (const file of selected) {
      try {
        headers.push(await readHeaderLine(file));
      } catch {
        setHeaderError(
          `The header of ${file.name} could not be read in this browser. You can still upload the files: each column is then recognized by its name or a known alias.`,
        );
        return;
      }
    }

    setHeaderError(null);
    upload.reset();

    review.mutate(
      selected.map((file, index) => {
        const previous = review.data?.files.find((candidate) => candidate.fileName === file.name);
        const decision = current?.[file.name];
        const source = decision?.source ?? assigned[file.name];

        return {
          fileName: file.name,
          header: headers[index] ?? '',
          assignedFileName: source || undefined,
          columns: previous && decision ? columnMapToSend(previous, decision) : undefined,
        };
      }),
      { onSuccess: (result) => setDecisions(initialDecisions(result)) },
    );
  };

  /** Carga con el mapping revisado: la fuente y las decisiones de cada archivo. */
  const uploadWithMapping = () => {
    if (!canSubmit || !review.data) return;

    const files = review.data.files;
    const payload: UploadPayload = {
      files: selected,
      label,
      assignments: selected.map((file) => decisions[file.name]?.source ?? '-'),
      columnMaps: selected.map((file) => {
        const reviewed = files.find((candidate) => candidate.fileName === file.name);
        return reviewed ? columnMapToSend(reviewed, decisions[file.name]) : null;
      }),
    };
    setLastSent(payload);

    upload.mutate(payload, {
      onSuccess: (result) => {
        if (result.accepted) {
          setSelected([]);
          setLabel('');
          setAssigned({});
          closeReview();
          if (inputRef.current) inputRef.current.value = '';
        }
      },
    });
  };

  /**
   * Repite la carga sin los archivos que fallaron, cuando la persona lo confirma. Lo que se
   * publica es una carga normal: la System la valida entera otra vez.
   */
  const uploadWithoutProblems = (plan: LeaveOutPlan) => {
    setLastSent(plan.payload);

    upload.mutate(plan.payload, {
      onSuccess: (result) => {
        if (result.accepted) {
          setSelected([]);
          setLabel('');
          setAssigned({});
          closeReview();
          if (inputRef.current) inputRef.current.value = '';
        }
      },
    });
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
              <label htmlFor="upload-files">
                <Label>CSV or Excel files</Label>
              </label>
              <input
                ref={inputRef}
                id="upload-files"
                type="file"
                multiple
                aria-describedby="upload-files-hint"
                accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                onChange={(event) => {
                  setSelected(Array.from(event.target.files ?? []));
                  setAssigned({});
                  setLastSent(null);
                  upload.reset();
                  closeReview();
                }}
                className="rounded-sm border border-rule bg-surface px-3 py-2 text-[13px] text-ink-2 file:mr-3 file:rounded-sm file:border-0 file:bg-accent file:px-3 file:py-1.5 file:text-[12.5px] file:font-medium file:text-on-accent"
              />
              <p id="upload-files-hint" className="font-mono text-[11px] text-ink-3">
                any file name works: each file is recognized by its header · CSV or Excel (.xlsx, first visible sheet) ·
                files of the same source, such as one per region, are loaded together · up to{' '}
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
                {selected.map((file, index) => {
                  const isOversized = file.size > data.maxFileBytes;

                  return (
                    <li key={`${file.name}-${index}`} className="flex items-baseline justify-between gap-3 text-[12.5px]">
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

            <div className="flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={() => void startReview()}
                disabled={!canSubmit || review.isPending || upload.isPending}
                className="rounded-sm border border-accent px-4 py-2 text-[13.5px] font-medium text-accent hover:bg-accent-wash disabled:cursor-not-allowed disabled:opacity-50"
              >
                {review.isPending && !review.data ? 'Reading headers…' : 'Review column mapping'}
              </button>

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

            {selected.length > 0 && !review.data && (
              <p className="max-w-[80ch] text-[12.5px] text-ink-3">
                Review column mapping reads only the header row of each file in this browser and shows which column will
                load each field of the database, before anything is uploaded.
              </p>
            )}

            {upload.error && (
              <p className="text-[13px]" style={{ color: 'var(--app-critical-text)' }}>
                {upload.error instanceof ApiError ? upload.error.message : 'The upload could not be completed.'}
              </p>
            )}

            {(headerError || review.error) && (
              <p className="text-[13px]" style={{ color: 'var(--app-critical-text)' }}>
                {headerError ??
                  (review.error instanceof ApiError ? review.error.message : 'The column mapping could not be reviewed.')}
              </p>
            )}
          </div>
        )}
      </Card>

      {review.data && !upload.data?.accepted && (
        <MappingReview
          review={review.data}
          decisions={decisions}
          onChange={(fileName, decision) => setDecisions((current) => ({ ...current, [fileName]: decision }))}
          onRecheck={() => void startReview(decisions)}
          onUpload={uploadWithMapping}
          onCancel={closeReview}
          checking={review.isPending}
          uploading={upload.isPending}
        />
      )}

      {upload.data && <UploadResult result={upload.data} />}

      {leaveOut && (
        <LeaveOutFiles plan={leaveOut} onConfirm={() => uploadWithoutProblems(leaveOut)} pending={upload.isPending} />
      )}

      {unidentified.length > 0 && (
        <AssignSources
          files={unidentified}
          assigned={assigned}
          onAssign={(fileName, source) => setAssigned((current) => ({ ...current, [fileName]: source }))}
          onRetry={() => submit(true)}
          pending={upload.isPending}
        />
      )}

      {data.connectors && <DatabaseSources connectors={data.connectors} />}

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

/**
 * Recalcular con los archivos que pasaron. La carga es todo o nada, así que dejar fuera un
 * archivo lo decide quien carga, sabiendo qué fuente queda sin dato en esta carga.
 */
function LeaveOutFiles({
  plan,
  onConfirm,
  pending,
}: {
  plan: LeaveOutPlan;
  onConfirm: () => void;
  pending: boolean;
}) {
  const count = countOf(plan.leftOut.length, 'file');

  return (
    <Card
      title="Recalculate with the files that passed"
      subtitle={`Leave out ${count} with problems and publish the other ${countOf(plan.payload.files.length, 'file')}. Nothing is published until you confirm.`}
    >
      <ul className="divide-y divide-rule">
        {plan.leftOut.map((file, index) => (
          <li key={`${file.fileName}-${index}`} className="flex flex-col gap-0.5 px-4 py-3">
            <span className="font-mono text-[12.5px] text-ink">{file.fileName}</span>
            <span className="text-[12.5px] text-ink-2">
              {file.copyOf
                ? `It is a copy of ${file.copyOf}, which is loaded: leaving it out changes nothing.`
                : file.source && file.partial
                ? `${file.source} is still loaded from its other files; only the rows of this one are left out.`
                : file.source
                  ? `${file.source} will have no data in this upload: its metrics are left out of the score, as with any missing source.`
                  : 'It is not one of the sources of the model, so leaving it out does not change the score.'}
            </span>
          </li>
        ))}
      </ul>

      <div className="flex flex-wrap items-center gap-3 border-t border-rule px-4 py-3">
        <button
          type="button"
          onClick={onConfirm}
          disabled={pending}
          className="rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {pending ? 'Validating and recalculating…' : `Leave out ${count} and recalculate`}
        </button>
        <span className="text-[12.5px] text-ink-3">
          The files that passed are validated again. Earlier uploads do not change.
        </span>
      </div>
    </Card>
  );
}

/** El veredicto de la carga, archivo por archivo. */
/**
 * Fuentes leídas directamente de bases de datos (ADR 0037). Las conexiones y las consultas
 * son configuración del despliegue: aquí se ve de dónde sale cada fuente, se prueban las
 * conexiones y se publica una carga leída de las bases, por la misma validación que los
 * archivos.
 */
function DatabaseSources({ connectors }: { connectors: ConnectorsView }) {
  const pull = usePull();
  const test = useTestConnections();
  const [label, setLabel] = useState('');
  const busy = pull.isPending || test.isPending;

  const engineOf = (connection: string) =>
    connectors.connections.find((candidate) => candidate.name === connection)?.provider ?? '—';

  const schedule = connectors.pullIntervalMinutes
    ? ` They are also read on their own every ${countOf(connectors.pullIntervalMinutes, 'minute')}.`
    : '';

  return (
    <>
      <Card
        title="Database sources"
        subtitle={`These sources are read straight from the organization's databases instead of files.${schedule} A pull publishes a complete upload through the same validation as files; if one source cannot be read, nothing is published.`}
      >
        <div className="overflow-x-auto">
          <table className="w-full min-w-[620px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>Source</Th>
                <Th>Connection</Th>
                <Th>Engine</Th>
                <Th>Read from</Th>
              </tr>
            </thead>
            <tbody>
              {connectors.sources.map((source) => (
                <tr key={source.fileName} className="border-b border-rule last:border-0">
                  <td className="px-4 py-2.5 text-ink">{source.source}</td>
                  <td className="px-4 py-2.5 font-mono text-[12px] text-ink-2">{source.connection}</td>
                  <td className="px-4 py-2.5 font-mono text-[12px] text-ink-2">{engineOf(source.connection)}</td>
                  <td className="px-4 py-2.5 text-ink-2">{source.kind === 'table' ? 'View or table' : 'Query'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex flex-col gap-4 border-t border-rule px-4 py-5">
          {!connectors.pullEnabled ? (
            <p className="text-[13px] text-ink-2">{formatNotice(connectors.disabledReason)}</p>
          ) : (
            <>
              <label className="flex max-w-[420px] flex-col gap-1.5">
                <Label>Upload label</Label>
                <input
                  type="text"
                  value={label}
                  maxLength={60}
                  placeholder="Database pull 0809"
                  onChange={(event) => setLabel(event.target.value)}
                  className="rounded-sm border border-rule bg-surface px-3 py-2 text-[13px] text-ink"
                />
              </label>

              <div className="flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  onClick={() => test.mutate()}
                  disabled={busy}
                  className="rounded-sm border border-accent px-4 py-2 text-[13.5px] font-medium text-accent hover:bg-accent-wash disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {test.isPending ? 'Testing connections…' : 'Test connections'}
                </button>

                <button
                  type="button"
                  onClick={() =>
                    pull.mutate(label, {
                      onSuccess: (result) => {
                        if (result.upload.accepted) setLabel('');
                      },
                    })
                  }
                  disabled={busy}
                  className="rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {pull.isPending ? 'Reading databases and recalculating…' : 'Pull from databases and recalculate'}
                </button>
              </div>
            </>
          )}

          {test.data && (
            <ul className="flex flex-col gap-1.5" aria-label="Connection test results">
              {test.data.map((check) => (
                <li key={check.name} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-[12.5px]">
                  <span className="font-mono text-ink">{check.name}</span>
                  <span className="font-mono text-[11.5px] text-ink-3">{check.provider}</span>
                  {check.ok ? (
                    <span style={{ color: 'var(--app-good-text)' }}>
                      connected in {fmt(check.elapsedMilliseconds / 1000, 1)} s
                    </span>
                  ) : (
                    <span style={{ color: 'var(--app-critical-text)' }}>{formatNotice(check.error)}</span>
                  )}
                </li>
              ))}
            </ul>
          )}

          {(pull.error || test.error) && (
            <p className="text-[13px]" style={{ color: 'var(--app-critical-text)' }}>
              {(pull.error ?? test.error) instanceof ApiError
                ? (pull.error ?? test.error)?.message
                : 'The databases could not be reached from the System layer.'}
            </p>
          )}
        </div>
      </Card>

      {pull.data && pull.data.sources.length > 0 && (
        <Card title="Per-source detail">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[620px] text-[13px]">
              <thead>
                <tr className="border-b border-rule bg-surface-2">
                  <Th>Source</Th>
                  <Th>Connection</Th>
                  <Th align="right">Rows read</Th>
                  <Th align="right">Time</Th>
                  <Th>Result</Th>
                </tr>
              </thead>
              <tbody>
                {pull.data.sources.map((source) => (
                  <tr key={source.fileName} className="border-b border-rule last:border-0">
                    <td className="px-4 py-2.5 text-ink">{source.source}</td>
                    <td className="px-4 py-2.5 font-mono text-[12px] text-ink-2">{source.connection}</td>
                    <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                      {source.rows.toLocaleString('en-US')}
                    </td>
                    <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">
                      {fmt(source.elapsedMilliseconds / 1000, 1)} s
                    </td>
                    <td className="px-4 py-2.5 text-[12.5px]">
                      {source.error ? (
                        <span style={{ color: 'var(--app-critical-text)' }}>{formatNotice(source.error)}</span>
                      ) : (
                        <span style={{ color: 'var(--app-good-text)' }}>read</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {pull.data && <UploadResult result={pull.data.upload} />}
    </>
  );
}

function UploadResult({ result }: { result: UploadResultView }) {
  const parts = partLabels(result.files);
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
                {result.files.map((file, index) => (
                  <tr key={`${file.fileName}-${index}`} className="border-b border-rule last:border-0">
                    <td className="px-4 py-2.5 font-mono text-[12px] text-ink">{file.fileName}</td>
                    <td className="px-4 py-2.5 text-ink-2">
                      {file.source}
                      {parts[index] && (
                        <span className="ml-1.5 font-mono text-[11px] text-ink-3">· {parts[index]}</span>
                      )}
                    </td>
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
                      ) : file.rowCount === 0 && file.rejectedCount > 0 ? (
                        // El archivo se leyó, pero ninguna fila sirvió: decir "accepted" aquí
                        // hacía pensar que el problema estaba en otro lado.
                        <span style={{ color: 'var(--app-critical-text)' }}>read, but every row was rejected</span>
                      ) : file.rejectedCount > 0 ? (
                        <span style={{ color: 'var(--app-serious-text)' }}>accepted, some rows rejected</span>
                      ) : (file.ignoredValues ?? []).length > 0 ? (
                        <span style={{ color: 'var(--app-serious-text)' }}>accepted, some values loaded as empty</span>
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

      <RejectionReasons files={result.files} accepted={result.accepted} />
    </div>
  );
}

/**
 * Por qué quedaron filas fuera, y qué valores quedaron sin dato, archivo por archivo y
 * agrupado por problema. Quien no puede compartir el archivo necesita saber qué columna
 * corregir sin mandárselo a nadie.
 */
function RejectionReasons({ files, accepted }: { files: readonly UploadFileView[]; accepted: boolean }) {
  return (
    <>
      <ReasonsCard
        title="Why rows were rejected"
        intro={`Each problem shows the column, what it expected and the value of the first row that had it. A row can have more than one problem, so the counts can add up to more than the rejected rows. ${
          accepted
            ? 'Fix the file and upload the set again to include those rows.'
            : 'Nothing from this upload was kept: fix the file and upload the set again.'
        }`}
        files={files}
        reasonsOf={(file) => file.rejectionReasons ?? []}
        regionLabel={(file) => `Rejected rows in ${file.source}`}
        summary={(file) =>
          `${countOf(file.rejectedCount, 'row')} of ${(file.rowCount + file.rejectedCount).toLocaleString('en-US')} rejected`
        }
        tone="var(--app-serious-text)"
      />
      <ReasonsCard
        title="Values loaded as empty"
        intro="These cells could not be read, so the field was loaded with no data, exactly as if the cell were empty; the rest of each row was loaded. A field without data is left out of the calculation, as the BRD does with a missing value. Fix the file and upload it again to include them."
        files={files}
        reasonsOf={(file) => file.ignoredValues ?? []}
        regionLabel={(file) => `Values loaded as empty in ${file.source}`}
        summary={(file) => `${countOf(file.rowCount, 'row')} loaded`}
        tone="var(--app-ink-2)"
      />
    </>
  );
}

function ReasonsCard({
  title,
  intro,
  files,
  reasonsOf,
  regionLabel,
  summary,
  tone,
}: {
  title: string;
  intro: string;
  files: readonly UploadFileView[];
  reasonsOf: (file: UploadFileView) => readonly UploadRejectionView[];
  regionLabel: (file: UploadFileView) => string;
  summary: (file: UploadFileView) => string;
  tone: string;
}) {
  const withReasons = files.filter((file) => reasonsOf(file).length > 0);

  if (withReasons.length === 0) return null;

  return (
    <Card title={title}>
      <div className="flex flex-col gap-5 px-4 py-3">
        <p className="max-w-[72ch] text-[12.5px] text-ink-3">{intro}</p>

        {withReasons.map((file, index) => (
          <section key={`${file.fileName}-${index}`} aria-label={regionLabel(file)} className="flex flex-col gap-2">
            <h3 className="flex flex-wrap items-baseline gap-x-2 text-[13px]">
              <span className="font-semibold text-ink">{file.source}</span>
              <span className="font-mono text-[12px] text-ink-3">{file.fileName}</span>
              <span className="tnum text-[12.5px]" style={{ color: tone }}>
                {summary(file)}
              </span>
            </h3>

            <ul className="flex flex-col divide-y divide-rule rounded-md border border-rule">
              {reasonsOf(file).map((item, index) => {
                const hint = rejectionHint(item.reason);

                return (
                  <li key={`${item.reason.code}-${index}`} className="flex flex-col gap-1 px-3 py-2.5">
                    <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                      <span className="text-[13px] text-ink">{rejectionReasonText(item.reason, item.sampleRows[0])}</span>
                      <span className="tnum shrink-0 font-mono text-[12px]" style={{ color: tone }}>
                        {countOf(item.rows, 'row')}
                      </span>
                    </div>
                    {item.sampleRows.length > 0 && (
                      <span className="tnum font-mono text-[11.5px] text-ink-3">
                        {sampleRowsText(item.sampleRows, item.rows)}
                      </span>
                    )}
                    {hint && <span className="max-w-[80ch] text-[12.5px] text-ink-2">{hint}</span>}
                  </li>
                );
              })}
            </ul>
          </section>
        ))}
      </div>
    </Card>
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
