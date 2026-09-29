import { useMappingCatalog } from '../api/hooks';
import type { MappingCatalogFieldView, MappingSourceView } from '../api/types';
import { Card, QueryError, Spinner } from '../components/primitives';
import { typeLabel } from '../mapping';

/**
 * Pestaña Mapping: de dónde sale cada campo de la base, antes de cargar.
 *
 * El linaje dice qué entró en una carga ya publicada. Esta pestaña es el paso previo: para
 * cada fuente, qué columna del archivo carga cada campo, con qué nombres se reconoce solo y
 * qué métricas lo usan. Se cambia al cargar —con "Review column mapping"— y lo confirmado
 * queda con la carga y se propone la próxima vez.
 */
export function MappingPanel({ onReview }: { onReview: () => void }) {
  const mapping = useMappingCatalog();

  if (mapping.isPending) return <Spinner label="Loading the column mapping…" />;
  if (mapping.error) return <QueryError error={mapping.error} title="The column mapping could not be loaded" />;

  const { recorded, sources } = mapping.data;
  const chosen = sources.flatMap((source) => source.fields).filter((field) => field.currentManual).length;

  return (
    <div className="flex flex-col gap-4">
      <Card title="Where each value of the database comes from">
        <div className="flex flex-col gap-3 px-4 py-4 text-[13px] text-ink-2">
          <p className="max-w-[80ch]">
            For each source, the column of the file that loads each field of the database, the names it is recognized by
            on its own and the metrics that use it. It is decided before uploading: choose your files in Uploads and press{' '}
            <strong className="font-medium text-ink">Review column mapping</strong>. What you confirm is saved with the
            upload and proposed again next time.
          </p>
          <p className="max-w-[80ch] text-[12.5px] text-ink-3">
            {recorded
              ? `The column shown is the one each field was loaded from in the current upload${
                  chosen > 0 ? `; ${chosen} ${chosen === 1 ? 'was' : 'were'} chosen by hand` : ''
                }.`
              : 'The current upload was loaded before the column record started, so each field shows the names it accepts. From the next upload on, the column that loaded each field is recorded here.'}
          </p>
          <div>
            <button
              type="button"
              onClick={onReview}
              className="rounded-sm border border-accent px-3 py-1.5 text-[13px] font-medium text-accent hover:bg-accent-wash"
            >
              Go to Uploads to review it with your files
            </button>
          </div>
        </div>
      </Card>

      {sources.map((source, index) => (
        <SourceMapping key={source.fileName} source={source} open={index === 0} recorded={recorded} />
      ))}
    </div>
  );
}

function SourceMapping({ source, open, recorded }: { source: MappingSourceView; open: boolean; recorded: boolean }) {
  const byHand = source.fields.filter((field) => field.currentManual).length;

  return (
    <details open={open} className="group rounded-md border border-rule bg-surface">
      <summary className="flex cursor-pointer flex-wrap items-baseline justify-between gap-x-4 gap-y-1 px-4 py-3">
        <span className="flex flex-wrap items-baseline gap-x-2">
          <span className="text-[14px] font-semibold text-ink">{source.source}</span>
          <span className="font-mono text-[12px] text-ink-3">{source.fileName}</span>
        </span>
        <span className="tnum font-mono text-[11.5px] text-ink-3">
          {source.fields.length} fields{byHand > 0 ? ` · ${byHand} chosen by hand` : ''}
        </span>
      </summary>

      {source.description && <p className="max-w-[80ch] px-4 pb-2 text-[12.5px] text-ink-3">{source.description}</p>}

      <div className="overflow-x-auto border-t border-rule">
        <table className="w-full min-w-[820px] text-[13px]">
          <caption className="sr-only">Column mapping of {source.source}</caption>
          <thead>
            <tr className="border-b border-rule bg-surface-2">
              <Th>Field in the database</Th>
              <Th>Column in the current upload</Th>
              <Th>Also recognized as</Th>
              <Th>Used by</Th>
            </tr>
          </thead>
          <tbody>
            {source.fields.map((field) => (
              <FieldRow key={field.column} field={field} recorded={recorded} />
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
}

function FieldRow({ field, recorded }: { field: MappingCatalogFieldView; recorded: boolean }) {
  const aliases = field.acceptedNames.filter((name) => name !== field.column);

  return (
    <tr className="border-b border-rule align-top last:border-0">
      <td className="px-4 py-2.5">
        <span className="flex flex-wrap items-center gap-1.5">
          <span className="font-mono text-[12.5px] text-ink">{field.column}</span>
          {field.isKey && <Badge>key</Badge>}
          {field.required && !field.isKey && <Badge>required</Badge>}
        </span>
        <span className="mt-0.5 block text-[11.5px] text-ink-3">{typeLabel(field.type)}</span>
      </td>
      <td className="px-4 py-2.5">
        <CurrentColumn field={field} recorded={recorded} />
      </td>
      <td className="px-4 py-2.5 font-mono text-[11.5px] break-words text-ink-3">
        {aliases.length > 0 ? aliases.join(' · ') : '—'}
      </td>
      <td className="px-4 py-2.5 text-[12.5px] text-ink-2">{field.feeds.length > 0 ? field.feeds.join(', ') : '—'}</td>
    </tr>
  );
}

function CurrentColumn({ field, recorded }: { field: MappingCatalogFieldView; recorded: boolean }) {
  if (!recorded || field.currentManual === null) {
    return <span className="text-[12px] text-ink-3">not recorded</span>;
  }

  if (field.currentHeader === null) {
    return <span className="text-[12px] text-ink-3">not in the file</span>;
  }

  return (
    <span className="flex flex-wrap items-center gap-1.5">
      <span className="font-mono text-[12px] text-ink">{field.currentHeader}</span>
      <span
        className="rounded-sm px-1.5 py-0.5 text-[11px] font-medium whitespace-nowrap"
        style={
          field.currentManual
            ? { color: 'var(--app-accent)', background: 'var(--app-accent-wash)' }
            : { color: 'var(--app-good-text)', background: 'var(--app-good-wash)' }
        }
      >
        {field.currentManual ? 'chosen by hand' : 'by name or alias'}
      </span>
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
