import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useDashboard } from '../api/hooks';
import type { DashboardApplication, DashboardKpi, DashboardTrendPoint, DashboardView } from '../api/types';
import { toApiFilters, useFilters } from '../filters';
import { bandLabel, countOf } from '../i18n/notices';
import { AXIS_KEYS, kpiSub, kpiTitle, levelLabel, scaleLabel, sourceName } from '../i18n/dashboard';
import { BandChip, Card, QueryError, Spinner, fmt } from '../components/primitives';
import { LevelChip, TrendLine, levelText } from '../components/dashboard/charts';
import { DefinitionsContext, ExplanationBlock } from '../components/Explanation';
import { dashboardExplanation } from '../i18n/analytics';
import {
  ComplexityPanel,
  HealthPanel,
  ObsolescencePanel,
  PrioritizationPanel,
  ResiliencePanel,
  RiskPanel,
  type Selection,
} from '../components/dashboard/panels';

/**
 * Dashboard ejecutivo — Portfolio Health & Risk Intelligence.
 *
 * Responde la pregunta ejecutiva con las seis preguntas de la propuesta del MVP, en
 * una vista y con el filtro de la barra. La pantalla no calcula: pinta lo que llega y,
 * al hacer clic en cualquier celda, burbuja, barra o indicador, abre el mismo detalle
 * filtrado de otra forma (PLAN §8.10, ADR 0024). Es la pagina de inicio, y cada panel
 * explica su calculo con las definiciones vigentes (ADR 0025).
 */
export function DashboardPage() {
  const [params, setParams] = useSearchParams();
  const { filters, search } = useFilters();
  const [selection, setSelection] = useState<Selection | null>(null);

  const axis = (AXIS_KEYS as readonly string[]).includes(params.get('axis') ?? '') ? params.get('axis')! : 'country';
  const { data, isPending, error } = useDashboard(toApiFilters(filters), axis);

  const setAxis = (next: string) => {
    const updated = new URLSearchParams(params);
    if (next === 'country') updated.delete('axis');
    else updated.set('axis', next);
    setParams(updated);
  };

  if (isPending) return <Spinner label="Reading the portfolio…" />;
  if (error) return <QueryError error={error} title="The dashboard could not be loaded" />;

  const { dashboard } = data;

  return (
    <DefinitionsContext.Provider value={data.definitions}>
    <div className="flex flex-col gap-4">
      <Header view={data} />

      {dashboard.applications.length === 0 ? (
        <Card title="Dashboard">
          <p className="px-4 py-6 text-[13px] text-ink-2">
            No application matches the filter. Remove a filter above to see the portfolio again.
          </p>
        </Card>
      ) : (
        <>
          <Indicators kpis={dashboard.kpis} trend={data.trend} onSelect={setSelection} />
          <div className="rounded-md border border-rule bg-surface [&>details]:border-t-0">
            <ExplanationBlock
              explanation={dashboardExplanation(0, data.definitions)}
              definitions={data.definitions}
              title="How the indicators are calculated"
            />
          </div>

          <div className="grid gap-4 xl:grid-cols-2">
            <HealthPanel view={data} axis={axis} onAxis={setAxis} onSelect={setSelection} />
            <PrioritizationPanel prioritization={dashboard.prioritization} search={search} onSelect={setSelection} />
            <ComplexityPanel complexity={dashboard.complexity} onSelect={setSelection} />
            <RiskPanel view={data} axis={axis} onAxis={setAxis} onSelect={setSelection} search={search} />
            <ObsolescencePanel obsolescence={dashboard.obsolescence} />
            <ResiliencePanel resilience={dashboard.resilience} onSelect={setSelection} />
          </div>
        </>
      )}

      <Sources view={data} />

      {selection && (
        <Detail
          selection={selection}
          applications={dashboard.applications}
          search={search}
          onClose={() => setSelection(null)}
        />
      )}
    </div>
    </DefinitionsContext.Provider>
  );
}

// ------------------------------------------------------------ encabezado

function Header({ view }: { view: DashboardView }) {
  const captured = new Date(view.capturedAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
  const provisional = view.definitions.areas.filter((area) => area.status !== 'agreed').length;

  return (
    <header className="flex flex-col gap-3">
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-2">
        <div>
          <p className="font-mono text-[10.5px] font-semibold tracking-[0.12em] text-accent uppercase">
            Enterprise architecture decision intelligence
          </p>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">Portfolio Health &amp; Risk Intelligence</h1>
        </div>
        <p className="font-mono text-[11px] text-ink-3">
          Rules {view.rulesetVersion} · data of {captured} ·{' '}
          <Link
            to="/definitions"
            className="rounded-sm px-1.5 py-0.5 font-semibold underline-offset-2 hover:underline"
            style={
              provisional > 0
                ? { color: 'var(--app-warning-text)', background: 'var(--app-warning-wash)' }
                : { color: 'var(--app-good-text)', background: 'var(--app-good-wash)' }
            }
          >
            {provisional > 0
              ? `${provisional} of ${view.definitions.areas.length} definition areas provisional`
              : 'All definitions agreed'}
          </Link>
        </p>
      </div>

      <div className="rounded-md bg-accent px-4 py-3 text-on-accent">
        <p className="font-mono text-[10px] font-semibold tracking-[0.12em] uppercase opacity-80">Executive question</p>
        <p className="mt-1 max-w-[80ch] text-[15px] leading-snug font-medium">
          Where is the highest concentration of portfolio risk and modernization opportunity, why does it exist, and what
          architectural decision is required?
        </p>
      </div>
    </header>
  );
}

// ------------------------------------------------------------ indicadores

const KPI_SELECTION: Record<string, Selection> = {
  health: { title: 'Every application in the filter', predicate: () => true },
  atRisk: {
    title: 'Applications at risk',
    hint: 'In the critical and at-risk bands',
    predicate: (a) => a.band === 'critical' || a.band === 'at-risk',
  },
  candidates: {
    title: 'Modernization candidates',
    hint: 'High criticality and high opportunity',
    predicate: (a) => a.candidate,
  },
  currency: { title: 'Legacy platforms', hint: 'Currency Score below the legacy threshold', predicate: (a) => a.legacy },
  resilience: {
    title: 'High or critical resilience gaps',
    predicate: (a) => a.resilience === 'high' || a.resilience === 'critical',
  },
};

function Indicators({
  kpis,
  trend,
  onSelect,
}: {
  kpis: readonly DashboardKpi[];
  trend: readonly DashboardTrendPoint[];
  onSelect: (selection: Selection) => void;
}) {
  return (
    <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5" aria-label="Portfolio indicators">
      {kpis.map((kpi) => {
        const selection = KPI_SELECTION[kpi.key];
        const value =
          kpi.value === null ? '—' : kpi.key === 'health' ? fmt(kpi.value, 0) : `${fmt(kpi.value, 0)}%`;

        return (
          <li key={kpi.key}>
            <button
              type="button"
              onClick={() => selection && onSelect(selection)}
              className="flex h-full w-full flex-col gap-1.5 rounded-md border border-rule bg-surface px-4 py-3 text-left hover:border-[var(--app-ink-3)]"
            >
              <span className="font-mono text-[10.5px] font-semibold tracking-[0.08em] text-ink-3 uppercase">
                {kpiTitle(kpi.key)}
              </span>
              <span className="flex items-baseline gap-2">
                <span className="tnum text-[26px] leading-none font-semibold text-ink">{value}</span>
                {kpi.key === 'health' && kpi.value !== null && <span className="text-[12px] text-ink-3">/ 100</span>}
                {kpi.key === 'health' && kpi.level && <BandChip band={kpi.level} />}
              </span>
              <span className="text-[11.5px] text-ink-2">
                {kpi.key === 'health'
                  ? `${countOf(kpi.base, 'application')} · ${kpiSub(kpi.key)}`
                  : `${kpi.count} of ${kpi.base} · ${kpiSub(kpi.key)}`}
              </span>
              {kpi.key === 'health' && (
                <TrendLine points={trend} value={(p) => p.healthScore} format={(n) => fmt(n, 1)} height={28} />
              )}
              {kpi.key === 'atRisk' && (
                <TrendLine points={trend} value={(p) => p.atRiskShare} format={(n) => `${fmt(n, 0)}%`} height={28} />
              )}
            </button>
          </li>
        );
      })}
    </ul>
  );
}

// ------------------------------------------------------------- fuentes

function Sources({ view }: { view: DashboardView }) {
  return (
    <footer className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-md border border-rule bg-surface px-4 py-3">
      <span className="font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase">Data sources</span>
      <ul className="flex flex-wrap gap-1.5">
        {view.sources.map((source) => (
          <li
            key={source.key}
            className={`rounded-sm px-2 py-0.5 text-[11.5px] ${
              source.connected ? 'bg-surface-2 text-ink' : 'border border-dashed border-rule text-ink-3'
            }`}
          >
            {sourceName(source.key)}
            <span className="ml-1 font-mono text-[9.5px] tracking-wide uppercase">
              {source.connected ? 'connected' : 'not connected'}
            </span>
          </li>
        ))}
      </ul>
      <span className="ml-auto text-[11.5px] font-semibold text-ink-2">Decision first. Dashboard second.</span>
    </footer>
  );
}

// ------------------------------------------------------------- detalle

/**
 * El detalle: las mismas columnas sea cual sea el panel desde el que se abrio. Es un
 * dialogo: Escape lo cierra, el foco entra al boton de cerrar y vuelve al salir.
 */
function Detail({
  selection,
  applications,
  search,
  onClose,
}: {
  selection: Selection;
  applications: readonly DashboardApplication[];
  search: string;
  onClose: () => void;
}) {
  const close = useRef<HTMLButtonElement>(null);
  const rows = useMemo(
    () =>
      applications
        .filter(selection.predicate)
        .sort((a, b) => (b.riskScore ?? -1) - (a.riskScore ?? -1) || a.eaiCode.localeCompare(b.eaiCode)),
    [applications, selection],
  );

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
        aria-labelledby="detail-title"
        className="relative flex h-full w-full max-w-[860px] flex-col border-l border-rule bg-surface shadow-xl"
      >
        <header className="flex items-start justify-between gap-4 border-b border-rule px-5 py-4">
          <div className="min-w-0">
            <p className="font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase">Detail</p>
            <h2 id="detail-title" className="mt-0.5 text-[17px] leading-snug font-semibold text-ink">
              {selection.title}
            </h2>
            <p className="mt-0.5 text-[12.5px] text-ink-2">
              {countOf(rows.length, 'application')}
              {selection.hint ? ` · ${selection.hint}` : ''}
            </p>
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

        <div className="flex-1 overflow-auto">
          <table className="w-full min-w-[980px] text-[12.5px]">
            <thead className="sticky top-0 bg-surface-2">
              <tr className="border-b border-rule text-left">
                {['Application', 'Region', 'Country', 'CIO', 'Capability', 'Critical', 'Disposition', 'Score', 'Risk', 'Currency', 'Resilience'].map(
                  (heading) => (
                    <th
                      key={heading}
                      scope="col"
                      className="px-3 py-2 font-mono text-[10px] font-semibold tracking-[0.08em] text-ink-3 uppercase"
                    >
                      {heading}
                    </th>
                  ),
                )}
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.eaiCode} className="border-b border-rule last:border-0">
                  <td className="px-3 py-2">
                    <Link
                      to={`/applications/${row.eaiCode}${search}`}
                      className="font-medium text-accent underline-offset-2 hover:underline"
                    >
                      {row.applicationName}
                    </Link>
                    <span className="block font-mono text-[10.5px] text-ink-3">
                      {row.eaiCode}
                      {row.status ? ` · ${row.status}` : ''}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-ink-2">{row.region ?? '—'}</td>
                  <td className="px-3 py-2 text-ink-2">{row.country ?? '—'}</td>
                  <td className="px-3 py-2 text-ink-2">{row.cio ?? '—'}</td>
                  <td className="px-3 py-2 text-ink-2">{row.capability ?? '—'}</td>
                  <td className="px-3 py-2 text-ink-2" title={`Business criticality: ${scaleLabel(row.criticality)}`}>
                    {row.critical ? 'Yes' : 'No'}
                  </td>
                  <td className="px-3 py-2 text-ink-2">
                    {row.disposition ?? '—'}
                    {row.roadmap && <span className="block text-[11px] text-ink-3">{row.roadmap}</span>}
                  </td>
                  <td className="px-3 py-2" title={bandLabel(row.band)}>
                    <BandChip band={row.band} score={row.score} />
                  </td>
                  <td className="tnum px-3 py-2 font-mono" style={{ color: levelText(row.riskLevel) }} title={levelLabel(row.riskLevel)}>
                    {row.riskScore === null ? '—' : fmt(row.riskScore, 0)}
                  </td>
                  <td className="tnum px-3 py-2 font-mono text-ink-2">
                    {row.currency === null ? '—' : fmt(row.currency, 0)}
                    {row.legacy && <span className="ml-1 text-[10px] text-ink-3">legacy</span>}
                  </td>
                  <td className="px-3 py-2">{row.resilience ? <LevelChip level={row.resilience} /> : <span className="text-ink-3">—</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {rows.length === 0 && <p className="px-5 py-6 text-[13px] text-ink-2">No application in this selection.</p>}
        </div>
      </section>
    </div>
  );
}
