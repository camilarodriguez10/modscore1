import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useCode, useCodeApplication } from '../api/hooks';
import type {
  CodeApplication,
  CodeApplicationView,
  CodeKpis,
  CodeLibraryRow,
  CodeLifecycleRow,
  CodeRepository,
  CodeSource,
  CodeStatusShare,
  CodeView,
} from '../api/types';
import { toApiFilters, useFilters } from '../filters';
import { countOf } from '../i18n/notices';
import { codeExplanation, formatParameter } from '../i18n/analytics';
import {
  STATUS_KEYS,
  factorLabel,
  formatDay,
  kindLabel,
  mismatchHint,
  mismatchLabel,
  monthsFromNow,
  statusHint,
  statusLabel,
  statusMark,
  statusText,
  statusWash,
} from '../i18n/code';
import { Card, Label, QueryError, Spinner, fmt } from '../components/primitives';
import { LevelBar, LevelChip } from '../components/dashboard/charts';
import { AreaStatus, DefinitionsContext, ExplanationBlock } from '../components/Explanation';

/**
 * Pantalla Code — obsolescencia y riesgo segun lo que declaran los repositorios de cada
 * aplicacion, cruzado con las fechas de fin de vida de endoflife.date (ADR 0026).
 *
 * Como el dashboard, no calcula: pinta lo que llega de la capa System con el filtro de la
 * barra. Hasta que exista la integracion con GitHub Enterprise, los repositorios son de
 * demostracion y la pantalla lo dice arriba, antes que cualquier numero.
 */
export function CodePage() {
  const [params, setParams] = useSearchParams();
  const { filters, search } = useFilters();
  const { data, isPending, error } = useCode(toApiFilters(filters));
  const [focus, setFocus] = useState<Focus>('all');
  const table = useRef<HTMLDivElement>(null);

  const app = params.get('app');
  const openApplication = (eaiCode: string | null) => {
    const updated = new URLSearchParams(params);
    if (eaiCode) updated.set('app', eaiCode);
    else updated.delete('app');
    setParams(updated);
  };

  const focusOn = (next: Focus) => {
    setFocus(next);
    table.current?.scrollIntoView({ block: 'start', behavior: 'smooth' });
  };

  if (isPending) return <Spinner label="Reading the repositories…" />;
  if (error) return <QueryError error={error} title="The code analysis could not be loaded" />;

  const { analysis, source } = data;
  const hasRepositories = analysis.kpis.repositories > 0;

  return (
    <DefinitionsContext.Provider value={data.definitions}>
      <div className="flex flex-col gap-4">
        <Header view={data} />
        <SourceNotice source={source} />

        {analysis.kpis.applications === 0 ? (
          <Card title="Code">
            <p className="px-4 py-6 text-[13px] text-ink-2">
              No application matches the filter. Remove a filter above to see the portfolio again.
            </p>
          </Card>
        ) : !hasRepositories ? (
          <NotConnected source={source} />
        ) : (
          <>
            <Indicators kpis={analysis.kpis} definitions={data.definitions} onFocus={focusOn} />
            <div className="rounded-md border border-rule bg-surface [&>details]:border-t-0">
              <ExplanationBlock
                explanation={codeExplanation(0, data.definitions)}
                definitions={data.definitions}
                title="How the code risk index is calculated"
              />
            </div>

            <LifecyclePanel statuses={analysis.statuses} rows={analysis.lifecycle} view={data} />

            <div className="grid gap-4 xl:grid-cols-2">
              <LibrariesPanel libraries={analysis.libraries} view={data} />
              <MismatchPanel applications={analysis.applications} view={data} onOpen={openApplication} />
            </div>

            <div ref={table} className="scroll-mt-4">
              <ApplicationsPanel view={data} focus={focus} onFocus={setFocus} onOpen={openApplication} />
            </div>
          </>
        )}

        {app && <ApplicationDrawer eaiCode={app} search={search} onClose={() => openApplication(null)} />}
      </div>
    </DefinitionsContext.Provider>
  );
}

// ------------------------------------------------------------ encabezado

function Header({ view }: { view: CodeView }) {
  return (
    <header className="flex flex-wrap items-end justify-between gap-x-6 gap-y-2">
      <div>
        <p className="font-mono text-[10.5px] font-semibold tracking-[0.12em] text-accent uppercase">
          Code intelligence · GitHub Enterprise
        </p>
        <h1 className="mt-1 text-[22px] leading-tight font-semibold">Component obsolescence &amp; risk</h1>
        <p className="mt-1 max-w-[80ch] text-[13px] text-ink-2">
          What each application’s repositories declare — runtimes, frameworks, OS images, databases and libraries —
          checked against published end-of-life dates and open security advisories.
        </p>
      </div>
      <div className="flex flex-col items-end gap-1">
        <p className="font-mono text-[11px] text-ink-3">
          Rules {view.rulesetVersion} · assessed on {formatDay(view.analysis.asOf)}
        </p>
        <AreaStatus area="code" definitions={view.definitions} />
      </div>
    </header>
  );
}

/**
 * De donde salen los datos, antes que cualquier numero. Con datos de demostracion lo dice
 * en ambar; con la integracion conectada, en una linea neutra.
 */
function SourceNotice({ source }: { source: CodeSource }) {
  const catalog = (
    <>
      End-of-life dates come from{' '}
      <a
        href={source.catalogUrl}
        target="_blank"
        rel="noopener noreferrer"
        className="font-medium underline underline-offset-2"
      >
        {source.catalog}
      </a>
      , retrieved {formatDay(source.catalogRetrievedOn)} ({source.catalogProducts} products, {source.catalogCycles}{' '}
      release cycles).
    </>
  );

  if (source.connected) {
    return (
      <p className="rounded-md border border-rule bg-surface px-4 py-2.5 text-[12.5px] text-ink-2">
        Repositories scanned {source.scannedAt ? formatDay(source.scannedAt.slice(0, 10)) : '—'}. {catalog}
      </p>
    );
  }

  const sample = source.provider === 'Sample';

  return (
    <section
      aria-label="Data source"
      className="flex flex-col gap-1 rounded-md border px-4 py-3 text-[12.5px] leading-relaxed"
      style={{
        background: 'var(--app-warning-wash)',
        borderColor: 'color-mix(in srgb, var(--app-warning) 45%, transparent)',
        color: 'var(--app-ink)',
      }}
    >
      <p className="font-semibold" style={{ color: 'var(--app-warning-text)' }}>
        {sample ? 'Sample repositories — GitHub Enterprise is not connected yet' : 'GitHub Enterprise is not connected'}
      </p>
      <p>
        {sample
          ? 'The repositories, versions and advisories below are generated from the portfolio for demonstration: each application’s coding language picks its stack, and its Currency Score tilts the versions. '
          : 'This screen reads each application’s repositories once the integration is in place. '}
        {catalog}
      </p>
    </section>
  );
}

function NotConnected({ source }: { source: CodeSource }) {
  return (
    <Card title="No repositories">
      <div className="flex flex-col gap-2 px-4 py-5 text-[13px] text-ink-2">
        <p>
          {source.provider === 'None'
            ? 'There is no repository inventory yet. When the GitHub Enterprise integration is connected, each repository is matched to its application by the property that names its EAI code, and this screen fills in.'
            : 'None of the applications in the filter has a repository mapped to it.'}
        </p>
        <p>
          The rules that will apply are already defined:{' '}
          <Link to="/definitions#code" className="text-accent underline underline-offset-2">
            see the Code definitions
          </Link>
          .
        </p>
      </div>
    </Card>
  );
}

// ------------------------------------------------------------ indicadores

type Focus = 'all' | 'eol' | 'approaching' | 'vulnerable' | 'high' | 'mismatch' | 'none';

const FOCUS: Record<Focus, { label: string; predicate: (a: CodeApplication) => boolean }> = {
  all: { label: 'All applications', predicate: () => true },
  eol: { label: 'Past end of life', predicate: (a) => a.eolComponents > 0 },
  approaching: { label: 'Ending soon', predicate: (a) => a.approachingComponents > 0 },
  vulnerable: { label: 'Critical or high advisories', predicate: (a) => a.criticalAlerts + a.highAlerts > 0 },
  high: { label: 'High or critical code risk', predicate: (a) => a.riskLevel === 'high' || a.riskLevel === 'critical' },
  mismatch: { label: 'Code and inventory disagree', predicate: (a) => a.mismatch !== null },
  none: { label: 'Without repositories', predicate: (a) => a.repositories === 0 },
};

const share = (part: number, whole: number) => (whole === 0 ? '0' : fmt((part / whole) * 100, 0));

function Indicators({
  kpis,
  definitions,
  onFocus,
}: {
  kpis: CodeKpis;
  definitions: CodeView['definitions'];
  onFocus: (focus: Focus) => void;
}) {
  const horizon = definitions.parameters.find((p) => p.key === 'code.approachingMonths');
  const analyzed = kpis.applicationsWithRepositories;

  const tiles: { key: string; title: string; value: string; sub: string; focus: Focus; status?: string }[] = [
    {
      key: 'repositories',
      title: 'Repositories analyzed',
      value: fmt(kpis.repositories, 0),
      sub: `${analyzed} of ${kpis.applications} applications have repositories`,
      focus: 'none',
    },
    {
      key: 'eol',
      title: 'Past end of life',
      value: `${share(kpis.eolApplications, analyzed)}%`,
      sub: `${countOf(kpis.eolApplications, 'application')} · ${countOf(kpis.eolComponents, 'component')}`,
      focus: 'eol',
      status: 'eol',
    },
    {
      key: 'approaching',
      title: `Ending within ${horizon ? formatParameter(horizon) : '12 months'}`,
      value: `${share(kpis.approachingApplications, analyzed)}%`,
      sub: `${countOf(kpis.approachingApplications, 'application')} · ${countOf(kpis.approachingComponents, 'component')}`,
      focus: 'approaching',
      status: 'approaching',
    },
    {
      key: 'vulnerable',
      title: 'Critical & high advisories',
      value: fmt(kpis.criticalAlerts + kpis.highAlerts, 0),
      sub: `${kpis.criticalAlerts} critical · ${kpis.highAlerts} high · ${countOf(kpis.vulnerableApplications, 'application')}`,
      focus: 'vulnerable',
    },
    {
      key: 'risk',
      title: 'High code risk',
      value: `${share(kpis.highRiskApplications, analyzed)}%`,
      sub: `${kpis.highRiskApplications} of ${analyzed} analyzed · high or critical`,
      focus: 'high',
    },
  ];

  return (
    <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5" aria-label="Code indicators">
      {tiles.map((tile) => (
        <li key={tile.key}>
          <button
            type="button"
            onClick={() => onFocus(tile.focus)}
            className="flex h-full w-full flex-col gap-1.5 rounded-md border border-rule bg-surface px-4 py-3 text-left hover:border-[var(--app-ink-3)]"
          >
            <span className="flex items-center gap-1.5 font-mono text-[10.5px] font-semibold tracking-[0.08em] text-ink-3 uppercase">
              {tile.status && (
                <span aria-hidden="true" className="size-2 rounded-full" style={{ background: statusMark(tile.status) }} />
              )}
              {tile.title}
            </span>
            <span className="tnum text-[26px] leading-none font-semibold text-ink">{tile.value}</span>
            <span className="text-[11.5px] text-ink-2">{tile.sub}</span>
          </button>
        </li>
      ))}
    </ul>
  );
}

// ------------------------------------------------------------ ciclo de vida

export function StatusChip({ status }: { status: string }) {
  return (
    <span
      title={statusHint(status)}
      className={`inline-flex items-center gap-1.5 rounded-sm px-1.5 py-0.5 text-[11px] font-semibold whitespace-nowrap ${
        status === 'unknown' ? 'border border-dashed border-rule' : ''
      }`}
      style={{ color: statusText(status), background: statusWash(status) }}
    >
      <span aria-hidden="true" className="size-1.5 rounded-full" style={{ background: statusMark(status) }} />
      {statusLabel(status)}
    </span>
  );
}

/** Los componentes por estado, en una barra apilada. Cada segmento filtra la tabla. */
function StatusBar({
  statuses,
  selected,
  onSelect,
}: {
  statuses: readonly CodeStatusShare[];
  selected: string;
  onSelect: (status: string) => void;
}) {
  const total = statuses.reduce((sum, s) => sum + s.components, 0);
  const shown = STATUS_KEYS.map((key) => statuses.find((s) => s.status === key)).filter(
    (s): s is CodeStatusShare => s !== undefined && s.components > 0,
  );

  return (
    <div className="flex flex-col gap-2">
      <div className="flex h-8 w-full gap-0.5 overflow-hidden rounded-sm" role="group" aria-label="Components by lifecycle status">
        {shown.map((s) => {
          const width = (s.components / total) * 100;
          return (
            <button
              key={s.status}
              type="button"
              aria-pressed={selected === s.status}
              onClick={() => onSelect(s.status)}
              aria-label={`${statusLabel(s.status)}: ${countOf(s.components, 'component')} in ${countOf(s.applications, 'application')}`}
              className="tnum flex min-w-[6px] items-center justify-center font-mono text-[10.5px] font-semibold first:rounded-l-sm last:rounded-r-sm"
              style={{
                width: `${width}%`,
                background: statusWash(s.status),
                color: statusText(s.status),
                boxShadow: `inset 0 -3px 0 ${statusMark(s.status)}${selected === s.status ? `, inset 0 0 0 2px ${statusMark(s.status)}` : ''}`,
              }}
            >
              {width >= 8 ? `${fmt(width, 0)}%` : ''}
            </button>
          );
        })}
      </div>
      <ul className="flex flex-wrap gap-x-4 gap-y-1 text-[11.5px] text-ink-2" aria-label="Legend">
        {shown.map((s) => (
          <li key={s.status} className="flex items-center gap-1.5">
            <span aria-hidden="true" className="size-2 rounded-full" style={{ background: statusMark(s.status) }} />
            <span className="font-medium text-ink">{statusLabel(s.status)}</span>
            <span className="tnum">
              {countOf(s.components, 'component')} · {countOf(s.applications, 'app')}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

type LifecycleFilter = 'action' | 'all' | (typeof STATUS_KEYS)[number];

const CYCLES = 12;

function LifecyclePanel({
  statuses,
  rows,
  view,
}: {
  statuses: readonly CodeStatusShare[];
  rows: readonly CodeLifecycleRow[];
  view: CodeView;
}) {
  const [filter, setFilter] = useState<LifecycleFilter>('action');
  const [expanded, setExpanded] = useState(false);
  const matching = rows.filter((row) =>
    filter === 'all' ? true : filter === 'action' ? row.status === 'eol' || row.status === 'approaching' : row.status === filter,
  );
  const visible = expanded ? matching : matching.slice(0, CYCLES);
  const choose = (next: LifecycleFilter) => {
    setFilter(next);
    setExpanded(false);
  };

  const options: { key: LifecycleFilter; label: string }[] = [
    { key: 'action', label: 'Needs action' },
    ...STATUS_KEYS.map((key) => ({ key, label: statusLabel(key) })),
    { key: 'all', label: 'All' },
  ];

  return (
    <Card
      title="Where is the stack reaching its end of life?"
      subtitle="Every runtime, framework, OS image and database the repositories declare, by release cycle."
    >
      <div className="flex flex-col gap-4 px-4 py-4">
        <StatusBar statuses={statuses} selected={filter} onSelect={(status) => choose(status as LifecycleFilter)} />

        <div className="flex flex-wrap gap-1.5" role="group" aria-label="Show cycles">
          {options.map((option) => (
            <button
              key={option.key}
              type="button"
              aria-pressed={filter === option.key}
              onClick={() => choose(option.key)}
              className={`rounded-sm border px-2.5 py-1 text-[12px] ${
                filter === option.key ? 'border-accent bg-accent-wash font-medium text-accent' : 'border-rule text-ink-2 hover:bg-surface-2'
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[820px] text-[12.5px]">
            <thead>
              <tr className="border-b border-rule text-left">
                {['Product', 'Cycle', 'End of life', 'Status', 'Repositories', 'Applications', 'Source'].map((heading) => (
                  <Th key={heading} right={heading === 'Repositories' || heading === 'Applications'}>
                    {heading}
                  </Th>
                ))}
              </tr>
            </thead>
            <tbody>
              {visible.map((row) => (
                <tr key={`${row.product}-${row.cycle}`} className="border-b border-rule last:border-0">
                  <td className="px-3 py-2">
                    <span className="font-medium text-ink">{row.productName}</span>
                    <span className="block text-[11px] text-ink-3">{kindLabel(row.kind)}</span>
                  </td>
                  <td className="px-3 py-2 font-mono text-[12px]">
                    {row.cycleLabel ?? row.cycle}
                    {row.cycleLabel && <span className="block text-[10.5px] text-ink-3">cycle {row.cycle}</span>}
                  </td>
                  <td className="px-3 py-2">
                    <EndOfLife
                      eol={row.eol}
                      months={row.monthsToEol}
                      discontinued={row.discontinued}
                      extended={row.extendedSupportEnds}
                      unknown={row.status === 'unknown'}
                      asOf={view.analysis.asOf}
                    />
                  </td>
                  <td className="px-3 py-2">
                    <StatusChip status={row.status} />
                  </td>
                  <td className="tnum px-3 py-2 text-right font-mono">{row.repositories}</td>
                  <td className="tnum px-3 py-2 text-right font-mono">{row.applications}</td>
                  <td className="px-3 py-2">
                    {row.sourceUrl ? (
                      <a
                        href={row.sourceUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-[12px] text-accent underline underline-offset-2"
                        aria-label={`${row.productName} on endoflife.date (opens in a new tab)`}
                      >
                        endoflife.date
                      </a>
                    ) : (
                      <span className="text-[12px] text-ink-3">Not in catalog</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {visible.length === 0 && <p className="px-3 py-4 text-[13px] text-ink-2">No release cycle in this status.</p>}
        </div>

        {matching.length > CYCLES && (
          <button
            type="button"
            onClick={() => setExpanded(!expanded)}
            className="self-start rounded-sm border border-rule px-3 py-1.5 text-[12.5px] text-ink-2 hover:bg-surface-2"
          >
            {expanded ? `Show the first ${CYCLES}` : `Show all ${countOf(matching.length, 'release cycle')}`}
          </button>
        )}
      </div>
      <ExplanationBlock explanation={codeExplanation(1, view.definitions)} definitions={view.definitions} />
    </Card>
  );
}

function EndOfLife({
  eol,
  months,
  discontinued,
  extended,
  unknown,
  asOf,
}: {
  eol: string | null;
  months: number | null;
  discontinued: boolean;
  extended: string | null;
  unknown: boolean;
  asOf: string;
}) {
  if (unknown) return <span className="text-ink-3">—</span>;
  if (discontinued) return <span className="text-ink-2">Discontinued, no date</span>;
  if (!eol) return <span className="text-ink-2">No date announced</span>;

  return (
    <span className="flex flex-col">
      <span className="tnum">{formatDay(eol)}</span>
      <span className="text-[11px] text-ink-3">
        {monthsFromNow(months)}
        {extended ? ` · paid support ${extended <= asOf ? 'ended' : 'to'} ${formatDay(extended)}` : ''}
      </span>
    </span>
  );
}

// ------------------------------------------------------------ bibliotecas

function LibrariesPanel({ libraries, view }: { libraries: readonly CodeLibraryRow[]; view: CodeView }) {
  return (
    <Card
      title="Which libraries need attention?"
      subtitle={`${countOf(view.analysis.kpis.flaggedLibraries, 'library', 'libraries')} with findings; the most urgent first.`}
    >
      <div className="overflow-x-auto px-1 py-2">
        <table className="w-full min-w-[560px] text-[12.5px]">
          <thead>
            <tr className="border-b border-rule text-left">
              {['Library', 'In use', 'Latest', 'Findings', 'Apps'].map((heading) => (
                <Th key={heading} right={heading === 'Apps'}>
                  {heading}
                </Th>
              ))}
            </tr>
          </thead>
          <tbody>
            {libraries.map((library) => (
              <tr key={`${library.ecosystem}-${library.name}`} className="border-b border-rule align-top last:border-0">
                <td className="px-3 py-2">
                  <span className="font-mono text-[12px] break-all text-ink">{library.name}</span>
                  <span className="block text-[11px] text-ink-3">{library.ecosystem}</span>
                </td>
                <td className="px-3 py-2 font-mono text-[11.5px] text-ink-2">{library.versions.join(', ')}</td>
                <td className="px-3 py-2 font-mono text-[11.5px] text-ink-2">{library.latest ?? '—'}</td>
                <td className="px-3 py-2">
                  <Findings
                    critical={library.critical}
                    high={library.high}
                    deprecated={library.deprecated}
                    outdated={library.outdated}
                    behind={library.majorsBehind}
                  />
                </td>
                <td className="tnum px-3 py-2 text-right font-mono">{library.applications}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {libraries.length === 0 && <p className="px-3 py-4 text-[13px] text-ink-2">No library with findings.</p>}
      </div>
      <ExplanationBlock explanation={codeExplanation(2, view.definitions)} definitions={view.definitions} />
    </Card>
  );
}

function Findings({
  critical,
  high,
  deprecated,
  outdated,
  behind,
}: {
  critical: number;
  high: number;
  deprecated: boolean;
  outdated: boolean;
  behind: number | null;
}) {
  const items: ReactNode[] = [];

  if (critical > 0) {
    items.push(
      <span key="critical" style={{ color: 'var(--app-critical-text)' }} className="font-semibold">
        {critical} critical
      </span>,
    );
  }
  if (high > 0) {
    items.push(
      <span key="high" style={{ color: 'var(--app-serious-text)' }} className="font-semibold">
        {high} high
      </span>,
    );
  }
  if (deprecated) items.push(<span key="deprecated">Deprecated</span>);
  if (outdated && behind !== null) items.push(<span key="outdated">{countOf(behind, 'major')} behind</span>);

  if (items.length === 0) return <span className="text-ink-3">—</span>;

  return <span className="flex flex-wrap gap-x-2 gap-y-0.5 text-[12px] text-ink-2">{items}</span>;
}

// ------------------------------------------------------------ discrepancias

function MismatchPanel({
  applications,
  view,
  onOpen,
}: {
  applications: readonly CodeApplication[];
  view: CodeView;
  onOpen: (eaiCode: string) => void;
}) {
  const groups = (['hiddenObsolescence', 'inventoryBehind'] as const).map((key) => ({
    key,
    rows: applications.filter((a) => a.mismatch === key),
  }));

  return (
    <Card
      title="Where do the code and the inventory disagree?"
      subtitle="The Currency Score from CMT against what the repositories actually declare."
    >
      <div className="flex flex-col divide-y divide-[var(--app-rule)]">
        {groups.map(({ key, rows }) => (
          <section key={key} className="px-4 py-3" aria-label={mismatchLabel(key)}>
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h3 className="text-[13.5px] font-semibold">
                {mismatchLabel(key)} <span className="tnum font-mono text-ink-2">· {rows.length}</span>
              </h3>
              <span className="text-[11.5px] text-ink-2">{mismatchHint(key)}</span>
            </div>
            {rows.length === 0 ? (
              <p className="mt-2 text-[12.5px] text-ink-3">No application in this situation.</p>
            ) : (
              <ul className="mt-2 flex flex-col gap-1">
                {rows.slice(0, 6).map((row) => (
                  <li key={row.eaiCode} className="flex flex-wrap items-baseline justify-between gap-x-3 text-[12.5px]">
                    <button
                      type="button"
                      onClick={() => onOpen(row.eaiCode)}
                      className="text-left font-medium text-accent underline-offset-2 hover:underline"
                    >
                      {row.applicationName}
                    </button>
                    <span className="tnum font-mono text-[11.5px] text-ink-2">
                      Currency {row.currency === null ? '—' : fmt(row.currency, 0)} ·{' '}
                      {row.eolComponents > 0 ? countOf(row.eolComponents, 'component') + ' past end of life' : 'nothing past end of life'}
                    </span>
                  </li>
                ))}
                {rows.length > 6 && (
                  <li className="text-[11.5px] text-ink-3">
                    and {rows.length - 6} more — use “Code and inventory disagree” in the table below.
                  </li>
                )}
              </ul>
            )}
          </section>
        ))}
      </div>
      <ExplanationBlock explanation={codeExplanation(3, view.definitions)} definitions={view.definitions} />
    </Card>
  );
}

// ------------------------------------------------------------ aplicaciones

const PAGE = 15;

function ApplicationsPanel({
  view,
  focus,
  onFocus,
  onOpen,
}: {
  view: CodeView;
  focus: Focus;
  onFocus: (focus: Focus) => void;
  onOpen: (eaiCode: string) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const rows = useMemo(() => view.analysis.applications.filter(FOCUS[focus].predicate), [view, focus]);
  const shown = expanded ? rows : rows.slice(0, PAGE);
  const levels = view.analysis.riskLevels.filter((l) => l.level !== 'none');
  const without = view.analysis.riskLevels.find((l) => l.level === 'none')?.applications ?? 0;

  return (
    <Card
      title="Which applications carry the most code risk?"
      subtitle="Ranked by code risk index. Open an application to see its repositories."
    >
      <div className="flex flex-col gap-3 px-4 py-4">
        <div className="flex flex-col gap-1.5">
          <Label>Code risk by application</Label>
          <LevelBar levels={levels} name="Code risk" />
          <p className="text-[11.5px] text-ink-2">
            {levels.map((l) => `${l.applications} ${l.level}`).join(' · ')} · {without} without repositories, which have no level
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label="Show applications">
          {(Object.keys(FOCUS) as Focus[]).map((key) => (
            <button
              key={key}
              type="button"
              aria-pressed={focus === key}
              onClick={() => {
                onFocus(key);
                setExpanded(false);
              }}
              className={`rounded-sm border px-2.5 py-1 text-[12px] ${
                focus === key ? 'border-accent bg-accent-wash font-medium text-accent' : 'border-rule text-ink-2 hover:bg-surface-2'
              }`}
            >
              {FOCUS[key].label}
            </button>
          ))}
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[980px] text-[12.5px]">
            <thead>
              <tr className="border-b border-rule text-left">
                {['Application', 'Repos', 'Past EOL', 'Ending soon', 'Advisories', 'Libraries', 'Stale', 'Currency', 'Code risk'].map(
                  (heading) => (
                    <Th key={heading} right={!['Application', 'Code risk'].includes(heading)}>
                      {heading}
                    </Th>
                  ),
                )}
              </tr>
            </thead>
            <tbody>
              {shown.map((row) => (
                <tr key={row.eaiCode} className="border-b border-rule last:border-0">
                  <td className="px-3 py-2">
                    <button
                      type="button"
                      onClick={() => onOpen(row.eaiCode)}
                      className="text-left font-medium text-accent underline-offset-2 hover:underline"
                    >
                      {row.applicationName}
                    </button>
                    <span className="block font-mono text-[10.5px] text-ink-3">
                      {row.eaiCode}
                      {row.critical ? ' · critical' : ''}
                      {row.mismatch ? ` · ${mismatchLabel(row.mismatch).toLowerCase()}` : ''}
                    </span>
                  </td>
                  <Num value={row.repositories} />
                  <Num value={row.eolComponents} status="eol" />
                  <Num value={row.approachingComponents} status="approaching" />
                  <td className="tnum px-3 py-2 text-right font-mono text-[12px]">
                    {row.criticalAlerts + row.highAlerts === 0 ? (
                      <span className="text-ink-3">0</span>
                    ) : (
                      <>
                        <span style={{ color: 'var(--app-critical-text)' }}>{row.criticalAlerts}C</span>{' '}
                        <span style={{ color: 'var(--app-serious-text)' }}>{row.highAlerts}H</span>
                      </>
                    )}
                  </td>
                  <td className="tnum px-3 py-2 text-right font-mono text-[12px] text-ink-2">
                    {row.deprecatedLibraries + row.outdatedLibraries === 0
                      ? '0'
                      : `${row.deprecatedLibraries} dep · ${row.outdatedLibraries} old`}
                  </td>
                  <Num value={row.staleRepositories} />
                  <td className="tnum px-3 py-2 text-right font-mono text-ink-2">
                    {row.currency === null ? '—' : fmt(row.currency, 0)}
                    {row.legacy && <span className="ml-1 text-[10px] text-ink-3">legacy</span>}
                  </td>
                  <td className="px-3 py-2">
                    {row.riskLevel ? (
                      <span className="flex items-center gap-2">
                        <span className="tnum w-7 text-right font-mono">{fmt(row.riskIndex ?? 0, 0)}</span>
                        <LevelChip level={row.riskLevel} />
                      </span>
                    ) : (
                      <span className="text-[12px] text-ink-3">No repositories</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {rows.length === 0 && <p className="px-3 py-4 text-[13px] text-ink-2">No application in this selection.</p>}
        </div>

        {rows.length > PAGE && (
          <button
            type="button"
            onClick={() => setExpanded(!expanded)}
            className="self-start rounded-sm border border-rule px-3 py-1.5 text-[12.5px] text-ink-2 hover:bg-surface-2"
          >
            {expanded ? `Show the first ${PAGE}` : `Show all ${countOf(rows.length, 'application')}`}
          </button>
        )}
      </div>
    </Card>
  );
}

function Th({ children, right = false }: { children: ReactNode; right?: boolean }) {
  return (
    <th
      scope="col"
      className={`px-3 py-2 font-mono text-[10px] font-semibold tracking-[0.08em] text-ink-3 uppercase ${right ? 'text-right' : ''}`}
    >
      {children}
    </th>
  );
}

function Num({ value, status }: { value: number; status?: string }) {
  return (
    <td
      className="tnum px-3 py-2 text-right font-mono"
      style={{ color: value > 0 && status ? statusText(status) : value === 0 ? 'var(--app-ink-3)' : undefined }}
    >
      {value}
    </td>
  );
}

// ------------------------------------------------------------ detalle

/**
 * Una aplicacion con sus repositorios. Es un dialogo, como el detalle del dashboard:
 * Escape lo cierra y el foco vuelve a donde estaba. La direccion lo recuerda (?app=),
 * asi que el enlace se puede compartir.
 */
function ApplicationDrawer({ eaiCode, search, onClose }: { eaiCode: string; search: string; onClose: () => void }) {
  const close = useRef<HTMLButtonElement>(null);
  const { data, isPending, error } = useCodeApplication(eaiCode);

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    close.current?.focus();

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };

    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
      previous?.focus();
    };
  }, [onClose]);

  const application = data?.detail.application;

  return (
    <div className="fixed inset-0 z-40 flex justify-end">
      <button
        type="button"
        aria-label="Close the detail"
        tabIndex={-1}
        onClick={onClose}
        className="absolute inset-0 cursor-default bg-[color-mix(in_srgb,var(--app-ink)_28%,transparent)]"
      />
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="code-detail-title"
        className="relative flex h-full w-full max-w-[900px] flex-col border-l border-rule bg-surface shadow-xl"
      >
        <header className="flex items-start justify-between gap-4 border-b border-rule px-5 py-4">
          <div className="min-w-0">
            <p className="font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase">
              Code · EAI {eaiCode}
            </p>
            <h2 id="code-detail-title" className="mt-0.5 text-[17px] leading-snug font-semibold text-ink">
              {application?.applicationName ?? 'Application'}
            </h2>
            {application && (
              <p className="mt-1 flex flex-wrap items-center gap-2 text-[12.5px] text-ink-2">
                {application.riskLevel ? (
                  <>
                    <span className="tnum font-mono">Index {fmt(application.riskIndex ?? 0, 0)}</span>
                    <LevelChip level={application.riskLevel} />
                  </>
                ) : (
                  <span>No repositories</span>
                )}
                <span>· {countOf(application.repositories, 'repository', 'repositories')}</span>
                <span>
                  · Currency {application.currency === null ? '—' : fmt(application.currency, 0)}
                  {application.legacy ? ' (legacy)' : ''}
                </span>
                {application.mismatch && (
                  <span
                    className="rounded-sm px-1.5 py-0.5 text-[11px] font-semibold"
                    style={{ background: 'var(--app-warning-wash)', color: 'var(--app-warning-text)' }}
                    title={mismatchHint(application.mismatch)}
                  >
                    {mismatchLabel(application.mismatch)}
                  </span>
                )}
                <Link
                  to={`/applications/${eaiCode}${search}`}
                  className="text-accent underline underline-offset-2"
                >
                  Open the application
                </Link>
              </p>
            )}
          </div>
          <button
            ref={close}
            type="button"
            onClick={onClose}
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            Close
          </button>
        </header>

        <div className="flex-1 overflow-auto px-5 py-4">
          {isPending ? (
            <Spinner label="Reading the repositories…" />
          ) : error ? (
            <QueryError error={error} title="The repositories could not be loaded" />
          ) : (
            <DrawerBody view={data} />
          )}
        </div>
      </section>
    </div>
  );
}

function DrawerBody({ view }: { view: CodeApplicationView }) {
  const { detail } = view;

  if (detail.repositories.length === 0) {
    return (
      <p className="text-[13px] text-ink-2">
        No repository in GitHub Enterprise is mapped to this application. Its code risk cannot be assessed: that is not
        low risk, it is not visible.
      </p>
    );
  }

  const total = detail.factors.reduce((sum, f) => sum + f.total, 0);

  return (
    <div className="flex flex-col gap-5">
      <section aria-labelledby="factors-title">
        <h3 id="factors-title" className="text-[13.5px] font-semibold">
          How the index adds up
        </h3>
        <table className="mt-2 w-full max-w-[520px] text-[12.5px]">
          <thead className="sr-only">
            <tr>
              <th scope="col">Finding</th>
              <th scope="col">Count</th>
              <th scope="col">Points each</th>
              <th scope="col">Points</th>
            </tr>
          </thead>
          <tbody>
            {detail.factors.map((factor) => (
              <tr key={factor.key} className={`border-b border-rule ${factor.count === 0 ? 'text-ink-3' : ''}`}>
                <th scope="row" className="py-1.5 pr-3 text-left font-normal">
                  {factorLabel(factor.key)}
                </th>
                <td className="tnum py-1.5 text-right font-mono">{factor.count}</td>
                <td className="tnum py-1.5 text-right font-mono text-ink-3">× {fmt(factor.points, 0)}</td>
                <td className="tnum py-1.5 text-right font-mono font-semibold">{fmt(factor.total, 0)}</td>
              </tr>
            ))}
            <tr>
              <th scope="row" className="py-1.5 pr-3 text-left font-semibold">
                Code risk index
              </th>
              <td colSpan={3} className="tnum py-1.5 text-right font-mono font-semibold">
                {fmt(total, 0)}
              </td>
            </tr>
          </tbody>
        </table>
        <p className="mt-1.5 text-[11.5px] text-ink-2">
          Points and levels are the Code definitions in force.{' '}
          <Link to="/definitions#code" className="text-accent underline underline-offset-2">
            See the definitions
          </Link>
        </p>
      </section>

      {detail.repositories.map((repository) => (
        <Repository key={repository.name} repository={repository} asOf={view.asOf} />
      ))}
    </div>
  );
}

function Repository({ repository, asOf }: { repository: CodeRepository; asOf: string }) {
  const flagged = repository.dependencies.filter((d) => d.deprecated || d.outdated || d.critical + d.high > 0);
  const clean = repository.dependencies.length - flagged.length;

  return (
    <section className="rounded-md border border-rule" aria-label={`Repository ${repository.name}`}>
      <header className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-rule bg-surface-2 px-4 py-2.5">
        <h3 className="font-mono text-[13px] font-semibold break-all">{repository.name}</h3>
        <span className="text-[11.5px] text-ink-2">
          {repository.primaryLanguage ?? 'No language'} · {repository.defaultBranch} · last commit{' '}
          {formatDay(repository.lastCommit)}
        </span>
        {repository.archived && <Tag>Archived</Tag>}
        {repository.stale && !repository.archived && <Tag>Stale</Tag>}
      </header>

      <div className="flex flex-col gap-3 px-4 py-3">
        {repository.components.length === 0 && repository.dependencies.length === 0 ? (
          <p className="text-[12.5px] text-ink-2">
            No manifest declares a runtime, framework or dependency: this repository cannot be assessed from its
            manifests.
          </p>
        ) : (
          <>
            {repository.components.length > 0 && (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-[12.5px]">
                  <thead>
                    <tr className="border-b border-rule text-left">
                      {['Component', 'Version', 'End of life', 'Status', 'Found in'].map((heading) => (
                        <Th key={heading}>{heading}</Th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {repository.components.map((component) => (
                      <tr key={`${component.product}-${component.version}`} className="border-b border-rule last:border-0">
                        <td className="px-3 py-1.5">
                          {component.productName}
                          <span className="block text-[11px] text-ink-3">{kindLabel(component.kind)}</span>
                        </td>
                        <td className="px-3 py-1.5 font-mono text-[12px]">
                          {component.version}
                          {component.cycle && component.cycle !== component.version && (
                            <span className="block text-[10.5px] text-ink-3">
                              cycle {component.cycleLabel ?? component.cycle}
                            </span>
                          )}
                        </td>
                        <td className="px-3 py-1.5">
                          <EndOfLife
                            eol={component.eol}
                            months={component.monthsToEol}
                            discontinued={component.status === 'eol' && component.eol === null}
                            extended={component.extendedSupportEnds}
                            unknown={component.status === 'unknown'}
                            asOf={asOf}
                          />
                        </td>
                        <td className="px-3 py-1.5">
                          <StatusChip status={component.status} />
                        </td>
                        <td className="px-3 py-1.5 font-mono text-[11.5px] text-ink-2">{component.detectedIn}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {repository.dependencies.length > 0 && (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-[12.5px]">
                  <thead>
                    <tr className="border-b border-rule text-left">
                      {['Library', 'Version', 'Latest', 'Findings'].map((heading) => (
                        <Th key={heading}>{heading}</Th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {flagged.map((dependency) => (
                      <tr key={dependency.name} className="border-b border-rule last:border-0">
                        <td className="px-3 py-1.5">
                          <span className="font-mono text-[12px] break-all">{dependency.name}</span>
                          <span className="block text-[11px] text-ink-3">
                            {dependency.ecosystem} · {dependency.direct ? 'direct' : 'transitive'}
                          </span>
                        </td>
                        <td className="px-3 py-1.5 font-mono text-[12px]">{dependency.version}</td>
                        <td className="px-3 py-1.5 font-mono text-[12px] text-ink-2">{dependency.latest ?? '—'}</td>
                        <td className="px-3 py-1.5">
                          <Findings
                            critical={dependency.critical}
                            high={dependency.high}
                            deprecated={dependency.deprecated}
                            outdated={dependency.outdated}
                            behind={dependency.majorsBehind}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                <p className="mt-1.5 text-[11.5px] text-ink-2">
                  {flagged.length === 0
                    ? `${countOf(repository.dependencies.length, 'library', 'libraries')}, none with findings.`
                    : clean > 0
                      ? `And ${countOf(clean, 'more library', 'more libraries')} without findings.`
                      : ''}
                </p>
              </div>
            )}
          </>
        )}
      </div>
    </section>
  );
}

function Tag({ children }: { children: ReactNode }) {
  return (
    <span
      className="rounded-sm px-1.5 py-0.5 text-[10.5px] font-semibold uppercase"
      style={{ background: 'var(--app-warning-wash)', color: 'var(--app-warning-text)' }}
    >
      {children}
    </span>
  );
}
