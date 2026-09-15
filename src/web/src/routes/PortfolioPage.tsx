import { Link } from 'react-router';
import { usePortfolio } from '../api/hooks';
import { BandDistribution, GroupBars } from '../components/charts';
import { toApiFilters, useFilters, usePage } from '../filters';
import { BandChip, Card, QueryError, Label, Spinner, fmt } from '../components/primitives';
import { countOf } from '../i18n/notices';

const PAGE_SIZE = 25;

/**
 * Pantalla 01 — portafolio.
 *
 * Los agregados no son ilustraciones: cada tramo, cada barra y cada celda de LOB o
 * pais es un control. Activarlo agrega ese filtro a la URL, y desde ahi acota la
 * tabla, los propios agregados y el resto de las pantallas.
 *
 * El filtro solo acota. El alcance del usuario recorta primero en el servidor y el
 * filtro se aplica sobre lo que quedo (ADR 0012).
 */
export function PortfolioPage() {
  const { filters, toggle } = useFilters();
  const [page, setPage] = usePage();
  const { data, isPending, isFetching, error } = usePortfolio(
    toApiFilters(filters, page, PAGE_SIZE),
  );

  if (isPending) return <Spinner label="Loading the portfolio…" />;

  if (error) {
    return <QueryError error={error} title="The portfolio could not be loaded" />;
  }

  const totalPages = Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE));
  const search = new URLSearchParams(window.location.search).toString();
  const detailSearch = search ? `?${search}` : '';

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <Label>Portfolio</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">
            {countOf(data.totalApplications, 'application')}
          </h1>
        </div>

        <dl className="flex gap-6">
          <Stat label="Average" value={fmt(data.averageScore)} />
          <Stat label="Median" value={fmt(data.medianScore)} />
        </dl>
      </header>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card title="Distribution by score band" subtitle="Over the filtered set">
          <BandDistribution
            bands={data.distribution}
            selected={filters.band}
            onSelect={(band) => toggle('band', band)}
          />
        </Card>
        <Card title="Average score by LOB">
          <GroupBars groups={data.byLob} selected={filters.lob} onSelect={(key) => toggle('lob', key)} />
        </Card>
        <Card title="Average score by country">
          <GroupBars
            groups={data.byCountry}
            selected={filters.country}
            onSelect={(key) => toggle('country', key)}
          />
        </Card>
        <Card title="Average score by CIO">
          <GroupBars groups={data.byCio} selected={filters.cio} onSelect={(key) => toggle('cio', key)} />
        </Card>
        <Card title="Average score by disposition">
          <GroupBars
            groups={data.byDisposition}
            selected={filters.disposition}
            onSelect={(key) => toggle('disposition', key)}
          />
        </Card>
        <Card title="Average score by roadmap">
          <GroupBars
            groups={data.byRoadmap}
            selected={filters.roadmap}
            onSelect={(key) => toggle('roadmap', key)}
          />
        </Card>
      </div>

      <Card
        title="Applications"
        subtitle={`${data.totalCount} ${data.totalCount === 1 ? 'result' : 'results'}`}
        right={
          isFetching ? <span className="font-mono text-[11px] text-ink-3">updating…</span> : undefined
        }
      >
        <div className="overflow-x-auto">
          <table className="w-full min-w-[820px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>EAI</Th>
                <Th>Application</Th>
                <Th>LOB</Th>
                <Th>Country</Th>
                <Th>Disposition / Roadmap</Th>
                <Th align="right">Weight</Th>
                <Th align="right">Score</Th>
              </tr>
            </thead>
            <tbody>
              {data.applications.map((row) => (
                <tr key={row.eaiCode} className="border-b border-rule last:border-0 hover:bg-surface-2">
                  <td className="tnum px-4 py-2.5 font-mono text-ink-2">
                    <Link
                      to={`/applications/${row.eaiCode}${detailSearch}`}
                      className="text-accent underline-offset-2 hover:underline"
                    >
                      {row.eaiCode}
                    </Link>
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="font-medium text-ink">{row.applicationName}</span>
                    {row.criticalApplication && (
                      <span className="ml-2 font-mono text-[10.5px] tracking-wide text-ink-3 uppercase">
                        critical
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-ink-2">
                    <CellFilter
                      value={row.lob}
                      active={filters.lob === row.lob}
                      onSelect={() => row.lob && toggle('lob', row.lob)}
                    />
                  </td>
                  <td className="px-4 py-2.5 text-ink-2">
                    <CellFilter
                      value={row.country}
                      active={filters.country === row.country}
                      onSelect={() => row.country && toggle('country', row.country)}
                    />
                  </td>
                  <td className="px-4 py-2.5 text-ink-2">
                    <CellFilter
                      value={row.disposition}
                      active={filters.disposition === row.disposition}
                      onSelect={() => row.disposition && toggle('disposition', row.disposition)}
                    />
                    {row.roadmap && <span className="text-ink-3"> · {row.roadmap}</span>}
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">
                    {fmt(row.availableWeight, 0)}
                  </td>
                  <td className="px-4 py-2.5 text-right">
                    <BandChip band={row.band} score={row.score} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {data.applications.length === 0 && (
          <p className="px-4 py-6 text-[13px] text-ink-2">
            No application matches the filter. Remove it above to see the portfolio again.
          </p>
        )}

        <nav className="flex items-center justify-between border-t border-rule px-4 py-2.5">
          <span className="tnum font-mono text-[12px] text-ink-3">
            page {data.page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <PageButton disabled={data.page <= 1} onClick={() => setPage(data.page - 1)}>
              Previous
            </PageButton>
            <PageButton disabled={data.page >= totalPages} onClick={() => setPage(data.page + 1)}>
              Next
            </PageButton>
          </div>
        </nav>
      </Card>
    </div>
  );
}

/**
 * Una celda que tambien filtra. El valor vacio no es un control: no hay nada por
 * lo que filtrar y un boton ahi solo prometeria.
 */
function CellFilter({
  value,
  active,
  onSelect,
}: {
  value: string | null;
  active: boolean;
  onSelect: () => void;
}) {
  if (!value) {
    return <span className="text-ink-3">—</span>;
  }

  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={active}
      title={active ? `Remove the ${value} filter` : `Filter by ${value}`}
      className="rounded-sm px-1 py-0.5 underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[var(--app-accent)]"
      style={active ? { background: 'var(--app-accent-wash)', color: 'var(--app-accent)' } : undefined}
    >
      {value}
    </button>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>
        <Label>{label}</Label>
      </dt>
      <dd className="tnum mt-0.5 text-[20px] leading-none font-semibold text-ink">{value}</dd>
    </div>
  );
}

function PageButton({
  disabled,
  onClick,
  children,
}: {
  disabled: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="rounded-sm border border-rule px-3 py-1 text-[12.5px] text-ink-2 hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-40"
    >
      {children}
    </button>
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
