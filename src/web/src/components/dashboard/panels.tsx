import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type {
  DashboardApplication,
  DashboardComplexity,
  DashboardMatrixPoint,
  DashboardObsolescence,
  DashboardPrioritization,
  DashboardResilience,
  DashboardView,
} from '../../api/types';
import { bandLabel, countOf, groupLabel } from '../../i18n/notices';
import {
  AXIS_KEYS,
  axisLabel,
  complexityDefinition,
  complexityName,
  factorName,
  levelLabel,
  opportunityAction,
  opportunityUnit,
  resilienceName,
  resilienceSource,
  scaleLabel,
  scaleShort,
} from '../../i18n/dashboard';
import { bandColor, fmt } from '../primitives';
import { Donut, Gauge, Heatmap, LevelBar, LevelChip, TrendLine, levelMark, levelText } from './charts';

/** Lo que se abre en el detalle: un titulo y que filas entran. */
export interface Selection {
  title: string;
  hint?: string | undefined;
  predicate: (application: DashboardApplication) => boolean;
}

type Select = (selection: Selection) => void;

/** El valor de una aplicacion en el eje del mapa de calor; vacio es «sin asignar». */
export function axisValue(application: DashboardApplication, axis: string): string {
  const value =
    axis === 'region'
      ? application.region
      : axis === 'cio'
        ? application.cio
        : axis === 'disposition'
          ? application.disposition
          : application.country;

  return value ?? '';
}

// ------------------------------------------------------------ marco

/**
 * Un panel por pregunta. El numero es el orden de la propuesta, y debajo de la
 * pregunta va la decision que el panel sirve: el dashboard existe para decidir.
 */
export function QuestionPanel({
  number,
  question,
  decision,
  right,
  children,
}: {
  number: number;
  question: string;
  decision: string;
  right?: ReactNode | undefined;
  children: ReactNode;
}) {
  return (
    <section
      aria-labelledby={`question-${number}`}
      className="flex min-w-0 flex-col rounded-md border border-rule bg-surface"
    >
      <header className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2 border-b border-rule px-4 py-3">
        <div className="flex min-w-0 items-start gap-3">
          <span
            aria-hidden="true"
            className="tnum mt-0.5 grid size-6 shrink-0 place-items-center rounded-full bg-accent font-mono text-[12px] font-semibold text-on-accent"
          >
            {number}
          </span>
          <div className="min-w-0">
            <h2 id={`question-${number}`} className="text-[15px] leading-snug font-semibold text-ink">
              {question}
            </h2>
            <p className="mt-0.5 text-[12.5px] text-ink-2">{decision}</p>
          </div>
        </div>
        {right}
      </header>
      <div className="flex flex-1 flex-col gap-4 px-4 py-4">{children}</div>
    </section>
  );
}

/** El eje de los mapas de calor. Es uno solo para las preguntas 1 y 4. */
export function AxisPicker({ axis, onAxis }: { axis: string; onAxis: (axis: string) => void }) {
  return (
    <div role="group" aria-label="Heatmap axis" className="flex flex-wrap gap-1">
      {AXIS_KEYS.map((key) => (
        <button
          key={key}
          type="button"
          onClick={() => onAxis(key)}
          aria-pressed={axis === key}
          className={`rounded-sm px-2 py-0.5 text-[11.5px] ${
            axis === key ? 'bg-accent-wash font-medium text-accent' : 'border border-rule text-ink-2 hover:bg-surface-2'
          }`}
        >
          {axisLabel(key)}
        </button>
      ))}
    </div>
  );
}

function SubHeading({ children }: { children: ReactNode }) {
  return (
    <h3 className="font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase">{children}</h3>
  );
}

// ------------------------------------------------------ 1 · salud

export function HealthPanel({
  view,
  axis,
  onAxis,
  onSelect,
}: {
  view: DashboardView;
  axis: string;
  onAxis: (axis: string) => void;
  onSelect: Select;
}) {
  const { health, matrix } = view.dashboard;

  return (
    <QuestionPanel
      number={1}
      question="How healthy is the portfolio?"
      decision="Where should leadership focus architectural attention first?"
      right={<AxisPicker axis={axis} onAxis={onAxis} />}
    >
      <div className="flex flex-col gap-2">
        <SubHeading>Average score by primary capability and {axisLabel(axis).toLowerCase()}</SubHeading>
        <Heatmap
          matrix={matrix}
          measure="health"
          onSelect={(row, column) =>
            onSelect({
              title: `${groupLabel(row)} · ${groupLabel(column)}`,
              hint: 'Applications in this cell of the health heatmap',
              predicate: (a) => (a.capability ?? '') === row && axisValue(a, axis) === column,
            })
          }
        />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="flex flex-col gap-2">
          <SubHeading>Portfolio health summary</SubHeading>
          <Donut
            center={fmt(health.averageScore, 0)}
            centerLabel="average score"
            slices={[...health.bands].reverse().map((band) => ({
              key: band.code,
              label: `${bandLabel(band.code)} (${band.name})`,
              count: band.applications,
              share: band.share,
              color: bandColor(band.code),
            }))}
            onSelect={(code) =>
              onSelect({
                title: `${bandLabel(code)} band`,
                hint: 'Applications whose score falls in this band',
                predicate: (a) => a.band === code,
              })
            }
          />
        </div>

        <div className="flex flex-col gap-2">
          <SubHeading>Health trend by upload</SubHeading>
          <TrendLine points={view.trend} value={(p) => p.healthScore} format={(n) => fmt(n, 0)} height={80} labelled />

          <button
            type="button"
            onClick={() =>
              onSelect({
                title: 'Applications requiring intervention',
                hint: 'In the critical and at-risk bands',
                predicate: (a) => a.band === 'critical' || a.band === 'at-risk',
              })
            }
            className="mt-1 rounded-sm border border-rule px-3 py-2 text-left hover:bg-surface-2"
          >
            <span className="tnum block text-[22px] leading-none font-semibold" style={{ color: 'var(--app-critical-text)' }}>
              {fmt(health.interventionShare, 0)}%
            </span>
            <span className="mt-1 block text-[12px] text-ink-2">
              of the portfolio requires architectural intervention ({countOf(health.requiringIntervention, 'application')})
            </span>
          </button>
        </div>
      </div>
    </QuestionPanel>
  );
}

// ------------------------------------------------ 2 · prioridad

const MAX_BUBBLES = 14;

export function PrioritizationPanel({
  prioritization,
  search,
  onSelect,
}: {
  prioritization: DashboardPrioritization;
  search: string;
  onSelect: Select;
}) {
  const byCell = new Map<string, DashboardMatrixPoint[]>();

  for (const point of prioritization.points) {
    const key = `${point.criticality}-${point.opportunity}`;
    byCell.set(key, [...(byCell.get(key) ?? []), point]);
  }

  const inQuadrant = (criticality: number, opportunity: number) =>
    criticality >= prioritization.minCriticality && opportunity >= prioritization.minOpportunity;

  return (
    <QuestionPanel
      number={2}
      question="Which applications should be modernized first?"
      decision="Which applications should receive modernization funding first?"
    >
      <div className="flex flex-col gap-2">
        <SubHeading>Prioritization matrix · bubble size is risk</SubHeading>

        <div className="grid grid-cols-[auto_1fr] gap-x-2">
          <span className="self-center text-[10.5px] font-medium text-ink-3 [writing-mode:vertical-rl] rotate-180">
            Business criticality
          </span>

          <div>
            <div className="grid grid-cols-[3.2rem_repeat(4,minmax(0,1fr))] gap-[3px]">
              {[3, 2, 1, 0].map((criticality) => (
                <div key={criticality} className="contents">
                  <span className="self-center pr-1 text-right text-[10.5px] text-ink-2">{scaleLabel(criticality)}</span>
                  {[0, 1, 2, 3].map((opportunity) => {
                    const points = [...(byCell.get(`${criticality}-${opportunity}`) ?? [])].sort(
                      (a, b) => (b.riskScore ?? 0) - (a.riskScore ?? 0),
                    );
                    const quadrant = inQuadrant(criticality, opportunity);

                    return (
                      <div
                        key={opportunity}
                        className="flex min-h-[4.6rem] flex-wrap content-center items-center justify-center gap-[3px] rounded-sm p-1"
                        style={{ background: quadrant ? 'var(--app-accent-wash)' : 'var(--app-surface-2)' }}
                      >
                        {points.slice(0, MAX_BUBBLES).map((point) => {
                          const size = 9 + ((point.riskScore ?? 0) / 100) * 15;
                          return (
                            <button
                              key={point.eaiCode}
                              type="button"
                              title={`${point.applicationName} · risk ${point.riskScore === null ? 'no data' : fmt(point.riskScore, 0)}`}
                              aria-label={`${point.applicationName}: criticality ${scaleLabel(point.criticality)}, opportunity ${scaleLabel(point.opportunity)}, risk ${
                                point.riskScore === null ? 'no data' : fmt(point.riskScore, 0)
                              } ${levelLabel(point.riskLevel)}`}
                              onClick={() =>
                                onSelect({
                                  title: point.applicationName,
                                  hint: `Criticality ${scaleLabel(point.criticality)} · opportunity ${scaleLabel(point.opportunity)}`,
                                  predicate: (a) => a.eaiCode === point.eaiCode,
                                })
                              }
                              className="rounded-full border-2 border-[var(--app-surface)]"
                              style={{ width: size, height: size, background: levelMark(point.riskLevel) }}
                            />
                          );
                        })}
                        {points.length > MAX_BUBBLES && (
                          <button
                            type="button"
                            onClick={() =>
                              onSelect({
                                title: `Criticality ${scaleLabel(criticality)} · opportunity ${scaleLabel(opportunity)}`,
                                predicate: (a) => a.criticality === criticality && a.opportunity === opportunity,
                              })
                            }
                            className="tnum rounded-sm px-1 font-mono text-[10px] text-ink-2 hover:underline"
                          >
                            +{points.length - MAX_BUBBLES}
                          </button>
                        )}
                      </div>
                    );
                  })}
                </div>
              ))}
              <span />
              {[0, 1, 2, 3].map((opportunity) => (
                <span key={opportunity} className="pt-1 text-center text-[10.5px] text-ink-2">
                  {scaleLabel(opportunity)}
                </span>
              ))}
            </div>
            <p className="mt-0.5 text-center text-[10.5px] font-medium text-ink-3">Modernization opportunity</p>
          </div>
        </div>

        <p className="text-[11.5px] text-ink-3">
          The shaded cells are the candidates: criticality and opportunity {scaleLabel(prioritization.minCriticality).toLowerCase()} or
          above. {countOf(prioritization.outsideMatrix, 'application')} stay out of the matrix because they leave the portfolio or have
          no plan.
        </p>
      </div>

      <div className="flex flex-col gap-2">
        <SubHeading>Top 10 modernization candidates</SubHeading>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[420px] text-[12.5px]">
            <thead>
              <tr className="border-b border-rule text-left">
                <Th>#</Th>
                <Th>Application</Th>
                <Th align="center">Crit.</Th>
                <Th align="center">Opp.</Th>
                <Th align="right">Uplift</Th>
                <Th align="right">Risk</Th>
              </tr>
            </thead>
            <tbody>
              {prioritization.top.map((point, index) => (
                <tr key={point.eaiCode} className="border-b border-rule last:border-0">
                  <td className="tnum py-1.5 pr-2 font-mono text-ink-3">{index + 1}</td>
                  <td className="py-1.5 pr-2">
                    <Link
                      to={`/applications/${point.eaiCode}/roadmap${search}`}
                      className="font-medium text-accent underline-offset-2 hover:underline"
                    >
                      {point.applicationName}
                    </Link>
                    <span className="block font-mono text-[10.5px] text-ink-3">
                      {point.eaiCode}
                      {point.roadmapName ? ` · ${point.roadmapName}` : ''}
                    </span>
                  </td>
                  <td className="py-1.5 text-center font-mono" title={scaleLabel(point.criticality)}>
                    {scaleShort(point.criticality)}
                  </td>
                  <td className="py-1.5 text-center font-mono" title={scaleLabel(point.opportunity)}>
                    {scaleShort(point.opportunity)}
                  </td>
                  <td className="tnum py-1.5 text-right font-mono" style={{ color: 'var(--app-good-text)' }}>
                    +{fmt(point.uplift, 1)}
                  </td>
                  <td
                    className="tnum py-1.5 text-right font-mono font-semibold"
                    style={{ color: levelText(point.riskLevel) }}
                    title={levelLabel(point.riskLevel)}
                  >
                    {point.riskScore === null ? '—' : fmt(point.riskScore, 0)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </QuestionPanel>
  );
}

// ---------------------------------------------- 3 · complejidad

export function ComplexityPanel({ complexity, onSelect }: { complexity: DashboardComplexity; onSelect: Select }) {
  const measured = complexity.dimensions
    .filter((d) => d.applications !== null)
    .sort((a, b) => (b.applications ?? 0) - (a.applications ?? 0));
  const pending = complexity.dimensions.filter((d) => d.applications === null);
  const split = Math.ceil(measured.length / 2);
  const rows = [measured.slice(0, split), measured.slice(split)].filter((row) => row.length > 0);
  const largest = Math.max(1, ...measured.map((d) => d.applications ?? 0));

  const selectDimension = (key: string) =>
    onSelect({
      title: complexityName(key),
      hint: complexityDefinition(key),
      predicate: (a) => a.complexity.includes(key),
    });

  const selectOpportunity = (key: string) => {
    if (key === 'rationalize' || key === 'consolidate') {
      selectDimension('duplicateTechnologies');
    } else if (key === 'standardize') {
      selectDimension('technologyProliferation');
    } else if (key === 'retire') {
      onSelect({
        title: 'Marked Eliminate and still active',
        hint: 'The complexity the portfolio has already decided to remove',
        predicate: (a) =>
          a.disposition === 'Eliminate' && (a.status === null || a.status.toLowerCase().includes('active')),
      });
    }
  };

  return (
    <QuestionPanel
      number={3}
      question="Where should we reduce complexity?"
      decision="Where can we simplify the portfolio and reduce operating costs?"
    >
      <div className="flex flex-col gap-2">
        <SubHeading>Complexity concentration</SubHeading>
        <div className="flex flex-col gap-[3px]">
          {rows.map((row, index) => (
            <div key={index} className="flex min-h-[5.5rem] gap-[3px]">
              {row.map((dimension) => {
                const count = dimension.applications ?? 0;
                const depth = 14 + Math.round((count / largest) * 30);

                return (
                  <button
                    key={dimension.key}
                    type="button"
                    onClick={() => selectDimension(dimension.key)}
                    aria-label={`${complexityName(dimension.key)}: ${count} applications, ${fmt(dimension.share ?? 0, 0)}% of the measured complexity`}
                    className="flex min-w-[7rem] flex-col justify-between rounded-sm p-2.5 text-left hover:outline hover:outline-1 hover:outline-[var(--app-ink-3)]"
                    style={{
                      flexGrow: Math.max(count, 1),
                      flexBasis: 0,
                      background: `color-mix(in srgb, var(--app-accent) ${depth}%, var(--app-surface))`,
                    }}
                  >
                    <span className="text-[12px] leading-tight font-medium text-ink">{complexityName(dimension.key)}</span>
                    <span>
                      <span className="tnum block text-[20px] leading-none font-semibold text-ink">
                        {fmt(dimension.share ?? 0, 0)}%
                      </span>
                      {/* Tinta plena: la secundaria no llega a 4.5:1 sobre la tesela mas teñida. */}
                      <span className="tnum font-mono text-[10.5px] text-ink">{count} apps</span>
                    </span>
                  </button>
                );
              })}
            </div>
          ))}
        </div>

        {pending.length > 0 && (
          <ul className="flex flex-wrap gap-[3px]">
            {pending.map((dimension) => (
              <li
                key={dimension.key}
                className="flex-1 rounded-sm border border-dashed border-rule px-2.5 py-1.5 text-[11.5px] text-ink-3"
              >
                <span className="font-medium text-ink-2">{complexityName(dimension.key)}</span> ·{' '}
                {complexityDefinition(dimension.key)}
              </li>
            ))}
          </ul>
        )}

        <p className="text-[11.5px] text-ink-3">
          Every measured dimension uses a provisional definition. Shares add up the measured complexity; an application can
          fall in more than one dimension.
        </p>
      </div>

      <div className="flex flex-col gap-2">
        <SubHeading>Complexity reduction opportunities</SubHeading>
        <ul className="grid gap-1.5 sm:grid-cols-2">
          {complexity.opportunities.map((opportunity) => (
            <li key={opportunity.key}>
              <button
                type="button"
                onClick={() => selectOpportunity(opportunity.key)}
                className="w-full rounded-sm border border-rule px-3 py-2 text-left hover:bg-surface-2"
              >
                <span className="block text-[12.5px] font-medium text-ink">{opportunityAction(opportunity.key)}</span>
                <span className="block text-[12px] text-ink-2">{opportunityUnit(opportunity.key, opportunity.count)}</span>
              </button>
            </li>
          ))}
        </ul>
        <p className="text-[11.5px] text-ink-3">The potential benefit needs cost or effort per application (DQ-02).</p>
      </div>
    </QuestionPanel>
  );
}

// ------------------------------------------------ 4 · riesgo

export function RiskPanel({
  view,
  axis,
  onAxis,
  onSelect,
  search,
}: {
  view: DashboardView;
  axis: string;
  onAxis: (axis: string) => void;
  onSelect: Select;
  search: string;
}) {
  const { risk, matrix } = view.dashboard;

  return (
    <QuestionPanel
      number={4}
      question="Where is technology risk concentrated?"
      decision="Which technology risks require immediate mitigation?"
      right={<AxisPicker axis={axis} onAxis={onAxis} />}
    >
      <div className="flex flex-col gap-2">
        <SubHeading>Average risk by primary capability and {axisLabel(axis).toLowerCase()}</SubHeading>
        <Heatmap
          matrix={matrix}
          measure="risk"
          onSelect={(row, column) =>
            onSelect({
              title: `${groupLabel(row)} · ${groupLabel(column)}`,
              hint: 'Applications in this cell of the risk heatmap',
              predicate: (a) => (a.capability ?? '') === row && axisValue(a, axis) === column,
            })
          }
        />
        <p className="text-[11.5px] text-ink-3">
          Risk goes from 0 to 100, higher is worse: the Modernization Risk Vectors pillar, inverted.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-[1fr_auto]">
        <div className="flex flex-col gap-2">
          <SubHeading>Top 5 risk hotspots</SubHeading>
          <ol className="flex flex-col divide-y divide-rule">
            {risk.hotspots.map((hotspot, index) => (
              <li key={hotspot.eaiCode} className="flex items-center gap-3 py-1.5">
                <span className="tnum w-4 font-mono text-[11.5px] text-ink-3">{index + 1}</span>
                <span className="min-w-0 flex-1">
                  <Link
                    to={`/applications/${hotspot.eaiCode}${search}`}
                    className="text-[12.5px] font-medium text-accent underline-offset-2 hover:underline"
                  >
                    {hotspot.applicationName}
                  </Link>
                  <span className="block truncate font-mono text-[10.5px] text-ink-3">
                    {hotspot.eaiCode}
                    {hotspot.capability ? ` · ${hotspot.capability}` : ''}
                  </span>
                </span>
                <span className="tnum font-mono text-[12.5px] font-semibold" style={{ color: levelText(hotspot.riskLevel) }}>
                  {fmt(hotspot.riskScore, 0)}
                </span>
                <LevelChip level={hotspot.riskLevel} />
              </li>
            ))}
          </ol>
        </div>

        <div className="flex min-w-[11rem] flex-col items-center justify-center gap-1.5 rounded-sm border border-rule px-4 py-3 text-center">
          <SubHeading>Risk concentration index</SubHeading>
          <span className="tnum text-[28px] leading-none font-semibold" style={{ color: levelText(risk.concentrationLevel) }}>
            {fmt(risk.concentrationIndex, 2)}
          </span>
          <LevelChip level={risk.concentrationLevel} />
          <span className="max-w-[12rem] text-[11px] leading-snug text-ink-3">
            Share of all risk held by the riskiest fifth of the heatmap cells
          </span>
        </div>
      </div>
    </QuestionPanel>
  );
}

// ------------------------------------------- 5 · obsolescencia

export function ObsolescencePanel({ obsolescence }: { obsolescence: DashboardObsolescence }) {
  const measured = obsolescence.factors.filter((f) => f.status !== 'needs-data');
  const pending = obsolescence.factors.filter((f) => f.status === 'needs-data');
  const largest = Math.max(1, ...measured.map((f) => f.share));

  return (
    <QuestionPanel
      number={5}
      question="What is driving obsolescence?"
      decision="Why is risk increasing, and what should be addressed first?"
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <SubHeading>Share of the points the portfolio loses</SubHeading>
          <ul className="flex flex-col gap-1.5">
            {measured.map((factor) => (
              <li key={factor.key} className="grid grid-cols-[minmax(0,12rem)_1fr_3rem] items-center gap-2">
                <span className="text-[12px] leading-tight text-ink">
                  {factorName(factor.key)}
                  {factor.currency && (
                    <span className="ml-1 font-mono text-[9.5px] tracking-wide text-accent uppercase">currency</span>
                  )}
                </span>
                <span className="h-3 rounded-sm bg-surface-2">
                  <span
                    className="block h-3 rounded-sm"
                    style={{
                      width: `${(factor.share / largest) * 100}%`,
                      background: factor.currency ? 'var(--app-accent)' : 'var(--app-ink-3)',
                    }}
                  />
                </span>
                <span className="tnum text-right font-mono text-[11.5px] text-ink-2">{fmt(factor.share, 0)}%</span>
              </li>
            ))}
            {pending.map((factor) => (
              <li key={factor.key} className="grid grid-cols-[minmax(0,12rem)_1fr] items-center gap-2">
                <span className="text-[12px] leading-tight text-ink-2">{factorName(factor.key)}</span>
                <span className="rounded-sm border border-dashed border-rule px-2 py-0.5 text-[11px] text-ink-3">
                  Needs lifecycle dates from TLM
                </span>
              </li>
            ))}
          </ul>
        </div>

        <div className="flex flex-wrap items-center justify-center gap-x-8 gap-y-2 border-t border-rule pt-3">
          <Gauge value={obsolescence.currencyDrivenShare} label="of the lost points come from lifecycle and technology currency" />
          <p className="max-w-[14rem] text-center text-[11.5px] text-ink-3">
            Average Currency Score{' '}
            <span className="tnum font-mono text-ink-2">
              {obsolescence.averageCurrency === null ? '—' : fmt(obsolescence.averageCurrency, 0)}
            </span>{' '}
            across {countOf(obsolescence.withCurrency, 'application')}
          </p>
        </div>
      </div>
    </QuestionPanel>
  );
}

// --------------------------------------------- 6 · resiliencia

export function ResiliencePanel({ resilience, onSelect }: { resilience: DashboardResilience; onSelect: Select }) {
  const fieldOf = (key: string): ((a: DashboardApplication) => string | null) | null =>
    key === 'recovery' ? (a) => a.recovery : key === 'operationalVulnerabilities' ? (a) => a.operational : null;

  return (
    <QuestionPanel
      number={6}
      question="Where are the resilience gaps?"
      decision="Which resilience weaknesses create the highest operational exposure?"
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <SubHeading>Resilience exposure by dimension</SubHeading>
          <ul className="flex flex-col gap-2">
            {resilience.dimensions.map((dimension) => {
              const field = fieldOf(dimension.key);

              return (
                <li key={dimension.key} className="grid grid-cols-[minmax(0,11rem)_1fr] items-center gap-3">
                  <span className="text-[12px] leading-tight text-ink">
                    {resilienceName(dimension.key)}
                    <span className="block text-[10.5px] text-ink-3">{resilienceSource(dimension.key)}</span>
                  </span>
                  {dimension.status === 'needs-data' || !field ? (
                    <span className="rounded-sm border border-dashed border-rule px-2 py-1 text-[11px] text-ink-3">
                      No source yet
                    </span>
                  ) : (
                    <LevelBar
                      name={resilienceName(dimension.key)}
                      levels={dimension.levels}
                      onSelect={(level) =>
                        onSelect({
                          title: `${resilienceName(dimension.key)}: ${levelLabel(level)}`,
                          predicate: (a) => field(a) === level,
                        })
                      }
                    />
                  )}
                </li>
              );
            })}
          </ul>
        </div>

        <div className="flex flex-col gap-2 border-t border-rule pt-3">
          <SubHeading>Resilience risk distribution</SubHeading>
          <Donut
            center={`${fmt(resilience.exposedShare, 0)}%`}
            centerLabel="high or critical"
            slices={[...resilience.overall].reverse().map((level) => ({
              key: level.level,
              label: levelLabel(level.level),
              count: level.applications,
              share: level.share,
              color: levelMark(level.level),
            }))}
            onSelect={(level) =>
              onSelect({
                title: `Resilience: ${levelLabel(level)}`,
                hint: 'The worst level across the measured dimensions',
                predicate: (a) => a.resilience === level,
              })
            }
          />
        </div>
      </div>

      <p className="rounded-sm bg-surface-2 px-3 py-2 text-[13px] text-ink">
        <span className="tnum font-semibold">{fmt(resilience.exposedShare, 0)}%</span>{' '}
        of the portfolio has high or critical resilience gaps, measured on {countOf(resilience.withData, 'application')} with
        recovery or operational data.
      </p>
    </QuestionPanel>
  );
}

function Th({ children, align = 'left' }: { children: ReactNode; align?: 'left' | 'right' | 'center' }) {
  return (
    <th
      scope="col"
      className={`py-1.5 font-mono text-[10px] font-semibold tracking-[0.08em] text-ink-3 uppercase ${
        align === 'right' ? 'text-right' : align === 'center' ? 'text-center' : 'text-left'
      }`}
    >
      {children}
    </th>
  );
}
