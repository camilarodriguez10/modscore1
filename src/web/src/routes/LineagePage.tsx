import { useLineage } from '../api/hooks';
import { Card, QueryError, Spinner, fmt } from '../components/primitives';
import { countOf, formatNotice } from '../i18n/notices';

/**
 * Linaje de datos, dentro de la pantalla de Datos.
 *
 * Explica por que muchos scores tienen el denominador reducido: una fuente que
 * cubre el 75% del portafolio deja un cuarto de las aplicaciones sin esa metrica.
 * Es por carga, no del sistema: comparar dos cargas es la unica forma de ver si una
 * fuente mejoro o se degrado.
 */
export function LineagePanel({ version }: { version?: string | undefined }) {
  const { data, isPending, error } = useLineage(version);

  if (isPending) return <Spinner label="Loading source status…" />;

  if (error) {
    return <QueryError error={error} title="The lineage could not be loaded" />;
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-[13px] text-ink-2">
        {countOf(data.sources.length, 'source')} in <span className="font-medium text-ink">{data.versionLabel}</span>,{' '}
        {data.provider} adapter. Coverage is the share of the {countOf(data.totalApplications, 'application')} that
        received data from each source.
      </p>

      <div className="grid gap-3 md:grid-cols-2">
        {data.sources.map((source) => (
          <Card key={source.sourceName} title={source.sourceName}>
            <div className="flex flex-col gap-3 px-4 py-4">
              <div>
                <div className="h-2 rounded-full bg-grid">
                  <div className="h-full rounded-full bg-accent" style={{ width: `${source.coveragePercent}%` }} />
                </div>
                <div className="tnum mt-1.5 flex justify-between font-mono text-[11.5px] text-ink-3">
                  <span>
                    {source.matchedApplications} of {countOf(data.totalApplications, 'application')}
                  </span>
                  <span className="text-ink-2">{fmt(source.coveragePercent, 1)}%</span>
                </div>
              </div>

              <dl className="tnum grid grid-cols-2 gap-x-4 gap-y-1 border-t border-rule pt-3 font-mono text-[11.5px]">
                <dt className="text-ink-3">Rows read</dt>
                <dd className="text-right text-ink">{source.recordCount}</dd>
                <dt className="text-ink-3">Rejected</dt>
                <dd
                  className="text-right"
                  style={{ color: source.rejectedCount > 0 ? 'var(--app-serious)' : 'var(--app-ink)' }}
                >
                  {source.rejectedCount}
                </dd>
                <dt className="text-ink-3">Origin</dt>
                <dd className="truncate text-right text-ink-2">{source.origin}</dd>
              </dl>

              {source.rejections.length > 0 && (
                <ul className="flex flex-col gap-1 border-t border-rule pt-3">
                  {source.rejections.slice(0, 3).map((rejection) => (
                    <li key={`${rejection.rowNumber}-${rejection.key}`} className="text-[12px] text-ink-2">
                      <span className="tnum font-mono text-ink-3">row {rejection.rowNumber}</span>{' '}
                      {formatNotice(rejection.reason)}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
