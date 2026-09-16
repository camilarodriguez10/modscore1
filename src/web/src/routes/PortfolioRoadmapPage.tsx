import { Link, useSearchParams } from 'react-router';
import { exportUrls } from '../api/client';
import { usePortfolioRoadmap } from '../api/hooks';
import type { DispositionGroupView, RoadmapGroupView } from '../api/types';
import { toApiFilters, useFilters, type FilterKey } from '../filters';
import { countOf, groupLabel } from '../i18n/notices';
import { BandChip, Card, QueryError, Label, Spinner, fmt } from '../components/primitives';
import { severityStyle } from './RoadmapPage';

/**
 * Pantalla 06 — el portafolio visto por su plan futuro.
 *
 * La agrupacion es una pregunta distinta cada vez: por disposition se ve la
 * estrategia, por pais se ve donde esta concentrado el trabajo, por CIO se ve quien
 * responde por el. El calculo no cambia —cada proyeccion sale del mismo motor de
 * what-if—, solo cambia por que criterio se promedia.
 */
const GROUPINGS = [
  { key: 'disposition', label: 'Disposition' },
  { key: 'roadmap', label: 'Roadmap' },
  { key: 'country', label: 'Country' },
  { key: 'cio', label: 'CIO' },
  { key: 'lob', label: 'LOB' },
  { key: 'application', label: 'Application' },
  { key: 'portfolio', label: 'Whole portfolio' },
] as const;

/** Que filtro aplica al abrir un grupo. La agrupacion por aplicacion abre su plan. */
const FILTER_OF: Partial<Record<string, FilterKey>> = {
  country: 'country',
  cio: 'cio',
  lob: 'lob',
  disposition: 'disposition',
  roadmap: 'roadmap',
};

export function PortfolioRoadmapPage() {
  const [params, setParams] = useSearchParams();
  const { filters, toggle, search } = useFilters();
  const groupBy = GROUPINGS.some((g) => g.key === params.get('groupBy'))
    ? params.get('groupBy')!
    : 'disposition';

  const { data, isPending, error } = usePortfolioRoadmap(toApiFilters(filters), groupBy);

  const setGrouping = (key: string) => {
    const next = new URLSearchParams(params);
    if (key === 'disposition') next.delete('groupBy');
    else next.set('groupBy', key);
    setParams(next);
  };

  if (isPending) return <Spinner label="Aggregating the portfolio's plans…" />;

  if (error) {
    return <QueryError error={error} title="The portfolio roadmap could not be loaded" />;
  }

  const filterKey = FILTER_OF[groupBy];
  const grouping = GROUPINGS.find((g) => g.key === groupBy);

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <Label>Portfolio roadmap</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">
            {countOf(data.totalApplications, 'application')} with a plan
          </h1>
        </div>

        <div className="flex flex-wrap items-end gap-6">
          <dl className="flex gap-6">
            <Stat label="Leaving the portfolio" value={String(data.leavingPortfolio)} />
            <Stat label="Without disposition" value={String(data.withoutDisposition)} />
          </dl>
          <a
            href={exportUrls.roadmap(toApiFilters(filters), groupBy)}
            download
            className="rounded-sm border border-rule bg-surface px-3 py-1.5 text-[13px] hover:bg-surface-2"
            title="One row per group, with the filters and grouping you are looking at"
          >
            Download CSV
          </a>
        </div>
      </header>

      <div className="flex flex-wrap items-center gap-2">
        <Label>Group by</Label>
        <div className="flex flex-wrap gap-1">
          {GROUPINGS.map((option) => (
            <button
              key={option.key}
              type="button"
              onClick={() => setGrouping(option.key)}
              aria-pressed={groupBy === option.key}
              className={`rounded-sm px-2.5 py-1 text-[12.5px] ${
                groupBy === option.key
                  ? 'bg-accent-wash font-medium text-accent'
                  : 'border border-rule text-ink-2 hover:bg-surface-2'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <Card
        title={groupBy === 'portfolio' ? 'Plan for the whole portfolio' : `Plan by ${grouping?.label.toLowerCase()}`}
        subtitle={
          filterKey
            ? 'Opening a group keeps it as a filter on every screen.'
            : 'The projected average only counts applications that project: those leaving the portfolio do not improve their score, they stop existing.'
        }
      >
        <div className="overflow-x-auto">
          <table className="w-full min-w-[880px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>Group</Th>
                <Th align="right">Apps</Th>
                <Th align="right">Score today</Th>
                <Th align="right">When finished</Th>
                <Th align="right">Δ</Th>
                <Th align="right">Leaving</Th>
                <Th align="right">Weeks</Th>
                <Th>Roadmap mix</Th>
              </tr>
            </thead>
            <tbody>
              {data.groups.map((group) => (
                <GroupRow
                  key={group.key}
                  group={group}
                  groupBy={groupBy}
                  search={search}
                  {...(filterKey && group.key !== ''
                    ? {
                        active: filters[filterKey] === group.key,
                        onSelect: () => toggle(filterKey, group.key),
                      }
                    : {})}
                />
              ))}
            </tbody>
          </table>
        </div>

        {data.groups.length === 0 && (
          <p className="px-4 py-6 text-[13px] text-ink-2">
            No application matches the filter. Remove it above to see the portfolio again.
          </p>
        )}
      </Card>

      {/* El reparto por disposition es el mapa de MAPS y solo tiene sentido en su
          propia agrupacion: mostrarlo bajo "por pais" seria repetir otra pregunta. */}
      {groupBy === 'disposition' &&
        data.dispositions.map((group) => <DispositionCard key={group.disposition} group={group} />)}

      {data.alerts.length > 0 && (
        <Card
          title="Contradictions between the plan and the data"
          subtitle="Each alert points to a concrete conflict, not an opinion about the strategy."
        >
          <ul className="divide-y divide-rule">
            {data.alerts.map((alert) => {
              const { color, icon } = severityStyle(alert.severity);

              return (
                <li
                  key={alert.ruleKey}
                  className="grid gap-x-4 gap-y-1 px-4 py-3 sm:grid-cols-[1.5rem_1fr_auto] sm:items-baseline"
                >
                  <span aria-hidden="true" className="text-[13px] font-bold" style={{ color }}>
                    {icon}
                  </span>

                  <div>
                    <p className="text-[13.5px] text-ink">{alert.title}</p>
                    <p className="mt-1 flex flex-wrap items-center gap-2 font-mono text-[11.5px] text-ink-3">
                      examples:
                      {alert.sampleEaiCodes.map((eai) => (
                        <Link
                          key={eai}
                          to={`/applications/${eai}/roadmap${search}`}
                          className="text-accent underline-offset-2 hover:underline"
                        >
                          {eai}
                        </Link>
                      ))}
                    </p>
                  </div>

                  <span className="tnum font-mono text-[13px] font-semibold sm:text-right" style={{ color }}>
                    {alert.count}
                  </span>
                </li>
              );
            })}
          </ul>
        </Card>
      )}
    </div>
  );
}

function GroupRow({
  group,
  groupBy,
  search,
  active = false,
  onSelect,
}: {
  group: RoadmapGroupView;
  groupBy: string;
  search: string;
  active?: boolean;
  onSelect?: (() => void) | undefined;
}) {
  // El backend no nombra la fila unica ni el grupo sin asignar: los nombra la interfaz.
  const name = groupBy === 'portfolio' ? 'Whole portfolio' : groupLabel(group.name);

  return (
    <tr
      className="border-b border-rule last:border-0"
      style={active ? { background: 'var(--app-accent-wash)' } : undefined}
    >
      <td className="px-4 py-2.5">
        {groupBy === 'application' ? (
          <Link
            to={`/applications/${group.key}/roadmap${search}`}
            className="font-medium text-accent underline-offset-2 hover:underline"
          >
            {name}
          </Link>
        ) : onSelect ? (
          <button
            type="button"
            onClick={onSelect}
            aria-pressed={active}
            title={active ? `Remove the ${name} filter` : `Filter by ${name}`}
            className="font-medium text-accent underline-offset-2 hover:underline"
          >
            {name}
          </button>
        ) : (
          <span className="font-medium text-ink">{name}</span>
        )}
        <span className="tnum ml-2 font-mono text-[11px] text-ink-3">{fmt(group.sharePercent, 0)}%</span>
        {group.alerts > 0 && (
          <span className="tnum ml-2 font-mono text-[11px]" style={{ color: 'var(--app-serious-text)' }}>
            {group.alerts} {group.alerts === 1 ? 'alert' : 'alerts'}
          </span>
        )}
      </td>

      <td className="tnum px-4 py-2.5 text-right font-mono text-ink">{group.count}</td>

      <td className="px-4 py-2.5 text-right">
        <BandChip band={group.band} score={group.averageScore} />
      </td>

      <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
        {group.projectedScore === null ? '—' : fmt(group.projectedScore)}
      </td>

      <td className="tnum px-4 py-2.5 text-right font-mono">
        {group.delta === null ? (
          <span className="text-ink-3">leaves</span>
        ) : (
          <span style={{ color: 'var(--app-good-text)' }}>+{fmt(group.delta)}</span>
        )}
      </td>

      <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">
        {group.leavingPortfolio === 0 ? '—' : group.leavingPortfolio}
      </td>

      <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">{group.averageWeeks}</td>

      <td className="px-4 py-2.5">
        <ul className="flex flex-wrap gap-x-3 gap-y-1">
          {group.mix.slice(0, 3).map((entry) => (
            <li key={entry.roadmap} className="text-[12px] text-ink-2">
              {entry.name}
              <span className="tnum ml-1 font-mono text-[11px] text-ink-3">{entry.count}</span>
            </li>
          ))}
          {group.mix.length > 3 && <li className="font-mono text-[11px] text-ink-3">+{group.mix.length - 3}</li>}
        </ul>
      </td>
    </tr>
  );
}

function DispositionCard({ group }: { group: DispositionGroupView }) {
  return (
    <Card title={group.name} subtitle={group.definition}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] text-[13px]">
          <thead>
            <tr className="border-b border-rule bg-surface-2">
              <Th>Roadmap</Th>
              <Th align="right">Apps</Th>
              <Th align="right">Score today</Th>
              <Th align="right">When finished</Th>
              <Th align="right">Δ</Th>
              <Th align="right">Duration</Th>
            </tr>
          </thead>
          <tbody>
            {group.roadmaps.map((roadmap) => (
              <tr key={roadmap.roadmap} className="border-b border-rule last:border-0">
                <td className="px-4 py-2.5">
                  <span className="font-medium text-ink">{roadmap.name}</span>
                  <span className="mt-0.5 block max-w-[52ch] text-[12px] text-ink-3">{roadmap.definition}</span>
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono text-ink">{roadmap.count}</td>
                <td className="tnum px-4 py-2.5 text-right font-mono text-ink-2">
                  {roadmap.count === 0 ? '—' : fmt(roadmap.averageScore)}
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                  {roadmap.averageProjectedScore === null ? '—' : fmt(roadmap.averageProjectedScore)}
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono">
                  {roadmap.averageDelta === null ? (
                    <span className="text-ink-3">leaves</span>
                  ) : (
                    <span style={{ color: 'var(--app-good-text)' }}>+{fmt(roadmap.averageDelta)}</span>
                  )}
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">{roadmap.totalWeeks} wk</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
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
