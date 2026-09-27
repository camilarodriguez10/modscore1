from patchlib import patch

patch(r"src\web\src\routes\CodePage.tsx", [
("""import type {
  CodeApplication,
  CodeApplicationView,
  CodeKpis,
  CodeLibraryRow,
  CodeLifecycleRow,
  CodeRepository,
  CodeSource,
  CodeStatusShare,
  CodeView,
} from '../api/types';""",
"""import type {
  CodeActionRow,
  CodeApplication,
  CodeApplicationView,
  CodeHorizon,
  CodeKpis,
  CodeLibraryRow,
  CodeLifecycleRow,
  CodeRepository,
  CodeSource,
  CodeStatusShare,
  CodeView,
  DashboardTrendPoint,
} from '../api/types';"""),
("""import {
  STATUS_KEYS,
  factorLabel,""",
"""import {
  STATUS_KEYS,
  actionText,
  factorLabel,"""),
("""  statusText,
  statusWash,
} from '../i18n/code';""",
"""  statusText,
  statusWash,
  urgencyLabel,
  urgencyStatus,
} from '../i18n/code';"""),
("""import { LevelBar, LevelChip } from '../components/dashboard/charts';""",
 """import { LevelBar, LevelChip, TrendLine } from '../components/dashboard/charts';"""),
# orden de paneles
("""            <LifecyclePanel statuses={analysis.statuses} rows={analysis.lifecycle} view={data} />""",
"""            <HistoryPanel trend={data.trend} view={data} />
            <OutlookPanel outlook={analysis.outlook} view={data} />
            <PlanPanel actions={analysis.actions} view={data} />

            <LifecyclePanel statuses={analysis.statuses} rows={analysis.lifecycle} view={data} />"""),
# columna en tres anos
("""                {['Application', 'Repos', 'Past EOL', 'Ending soon', 'Advisories', 'Libraries', 'Stale', 'Currency', 'Code risk'].map(
                  (heading) => (
                    <Th key={heading} right={!['Application', 'Code risk'].includes(heading)}>""",
"""                {['Application', 'Repos', 'Past EOL', 'Ending soon', 'Advisories', 'Libraries', 'Stale', 'Currency', 'Code risk', 'In 3 years'].map(
                  (heading) => (
                    <Th key={heading} right={!['Application', 'Code risk', 'In 3 years'].includes(heading)}>"""),
("""                    ) : (
                      <span className="text-[12px] text-ink-3">No repositories</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {rows.length === 0 && <p className="px-3 py-4 text-[13px] text-ink-2">No application in this selection.</p>}""",
"""                    ) : (
                      <span className="text-[12px] text-ink-3">No repositories</span>
                    )}
                  </td>
                  <td className="px-3 py-2">
                    {row.repositories === 0 ? (
                      <span className="text-ink-3">—</span>
                    ) : (
                      <span className="flex items-center gap-2 whitespace-nowrap">
                        <span className="tnum font-mono text-[12px]" style={{ color: row.projectedEolComponents > 0 ? statusText('eol') : undefined }}>
                          {row.projectedEolComponents} past EOL
                        </span>
                        <LevelChip level={row.projectedRiskLevel} />
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {rows.length === 0 && <p className="px-3 py-4 text-[13px] text-ink-2">No application in this selection.</p>}"""),
("""          <table className="w-full min-w-[980px] text-[12.5px]">
            <thead>
              <tr className="border-b border-rule text-left">
                {['Application', 'Repos',""",
"""          <table className="w-full min-w-[1120px] text-[12.5px]">
            <thead>
              <tr className="border-b border-rule text-left">
                {['Application', 'Repos',"""),
# detalle: sugerencias, proyeccion e historia
("""  const total = detail.factors.reduce((sum, f) => sum + f.total, 0);

  return (
    <div className="flex flex-col gap-5">
      <section aria-labelledby="factors-title">""",
"""  const total = detail.factors.reduce((sum, f) => sum + f.total, 0);

  return (
    <div className="flex flex-col gap-5">
      <Suggestions view={view} />
      <ApplicationOutlook view={view} />

      <section aria-labelledby="factors-title">"""),
("""      {detail.repositories.map((repository) => (
        <Repository key={repository.name} repository={repository} asOf={view.asOf} />
      ))}""",
"""      <ApplicationHistory view={view} />

      {detail.repositories.map((repository) => (
        <Repository key={repository.name} repository={repository} asOf={view.asOf} />
      ))}"""),
])

# Componentes nuevos, al final del archivo
from patchlib import ROOT
p = ROOT / r"src\web\src\routes\CodePage.tsx"
t = p.read_text(encoding="utf-8", newline="")
nl = "\r\n" if "\r\n" in t else "\n"
block = r"""
// ------------------------------------------------------------ en el tiempo

const HORIZON_NAMES = ['Today', 'In 1 year', 'In 2 years', 'In 3 years'];
const horizonName = (years: number) => HORIZON_NAMES[years] ?? `In ${years} years`;
const pct = (value: number | null) => (value === null ? '—' : `${fmt(value, 0)}%`);

/** Cuanto cambio contra el primer punto, con signo, y si eso es mejor o peor. */
function Delta({ from, to, lowerIsBetter = false, suffix = '' }: { from: number | null; to: number | null; lowerIsBetter?: boolean; suffix?: string }) {
  if (from === null || to === null) return null;
  const change = to - from;
  if (Math.abs(change) < 0.05) return <span className="text-[11.5px] text-ink-3">no change</span>;
  const better = lowerIsBetter ? change < 0 : change > 0;

  return (
    <span className="tnum text-[11.5px] font-semibold" style={{ color: better ? statusText('supported') : statusText('eol') }}>
      {change > 0 ? '+' : '−'}
      {fmt(Math.abs(change), 1)}
      {suffix} {better ? 'better' : 'worse'}
    </span>
  );
}

/** Score, Currency Score y Risk Score del portafolio filtrado en cada carga. */
function HistoryPanel({ trend, view }: { trend: readonly DashboardTrendPoint[]; view: CodeView }) {
  const first = trend[0];
  const last = trend[trend.length - 1];
  const series: {
    key: string;
    title: string;
    sub: string;
    value: (p: DashboardTrendPoint) => number | null;
    lowerIsBetter?: boolean;
  }[] = [
    { key: 'score', title: 'Modernization Score', sub: 'average, 20–100', value: (p) => p.healthScore },
    { key: 'currency', title: 'Currency Score', sub: 'average from CMT, 0–100', value: (p) => p.currency },
    { key: 'risk', title: 'Risk Score', sub: 'average, 0–100, lower is better', value: (p) => p.riskScore, lowerIsBetter: true },
  ];

  return (
    <Card
      title="How did score, currency and risk move across uploads?"
      subtitle="The filtered portfolio in every upload, oldest first."
    >
      <div className="flex flex-col gap-4 px-4 py-4">
        {trend.length < 2 ? (
          <p className="text-[13px] text-ink-2">
            {trend.length === 1
              ? 'There is one upload so far: the comparison starts with the next one. The sample timeline in data\\timeline has four quarters to load.'
              : 'There are no uploads yet.'}
          </p>
        ) : (
          <>
            <div className="grid gap-4 md:grid-cols-3">
              {series.map((s) => {
                const from = first ? s.value(first) : null;
                const to = last ? s.value(last) : null;
                return (
                  <div key={s.key} className="flex flex-col gap-1">
                    <Label>{s.title}</Label>
                    <span className="flex items-baseline gap-2">
                      <span className="tnum text-[22px] leading-none font-semibold">{to === null ? '—' : fmt(to, 1)}</span>
                      <Delta from={from} to={to} lowerIsBetter={s.lowerIsBetter ?? false} />
                    </span>
                    <span className="text-[11.5px] text-ink-2">
                      {s.sub} · since {first?.label}
                    </span>
                    <TrendLine points={trend} value={s.value} format={(n) => fmt(n, 1)} height={56} labelled />
                  </div>
                );
              })}
            </div>

            <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] text-[12.5px]">
                <caption className="sr-only">Score, currency and risk by upload</caption>
                <thead>
                  <tr className="border-b border-rule text-left">
                    {['Upload', 'Applications', 'Score', 'Currency', 'Risk'].map((heading) => (
                      <Th key={heading} right={heading !== 'Upload'}>
                        {heading}
                      </Th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {trend.map((point) => (
                    <tr key={point.versionId} className="border-b border-rule last:border-0">
                      <td className="px-3 py-1.5">
                        {point.label}
                        <span className="block text-[11px] text-ink-3">{formatDay(point.capturedAt.slice(0, 10))}</span>
                      </td>
                      <td className="tnum px-3 py-1.5 text-right font-mono">{point.applications}</td>
                      <td className="tnum px-3 py-1.5 text-right font-mono">{point.healthScore === null ? '—' : fmt(point.healthScore, 2)}</td>
                      <td className="tnum px-3 py-1.5 text-right font-mono">{point.currency === null ? '—' : fmt(point.currency, 2)}</td>
                      <td className="tnum px-3 py-1.5 text-right font-mono">{point.riskScore === null ? '—' : fmt(point.riskScore, 2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
      <ExplanationBlock explanation={codeExplanation(5, view.definitions)} definitions={view.definitions} />
    </Card>
  );
}

/** Una barra fina con los componentes por estado en un horizonte. */
function HorizonBar({ statuses, label }: { statuses: readonly CodeStatusShare[]; label: string }) {
  const total = statuses.reduce((sum, s) => sum + s.components, 0);
  const shown = STATUS_KEYS.map((key) => statuses.find((s) => s.status === key)).filter(
    (s): s is CodeStatusShare => s !== undefined && s.components > 0,
  );

  return (
    <div
      className="flex h-2.5 w-full gap-0.5 overflow-hidden rounded-sm"
      role="img"
      aria-label={`${label}: ${shown.map((s) => `${statusLabel(s.status)} ${s.components}`).join(', ')}`}
    >
      {shown.map((s) => (
        <span key={s.status} style={{ width: `${(s.components / total) * 100}%`, background: statusMark(s.status) }} />
      ))}
    </div>
  );
}

/** El portafolio hoy y en uno, dos y tres años, si nada cambia. */
function OutlookPanel({ outlook, view }: { outlook: readonly CodeHorizon[]; view: CodeView }) {
  const today = outlook[0];

  return (
    <Card
      title="Where is obsolescence heading if nothing changes?"
      subtitle="Today and in one, two and three years, with the same versions, the same advisories and no new commits."
    >
      <div className="grid gap-px bg-[var(--app-rule)] sm:grid-cols-2 lg:grid-cols-4">
        {outlook.map((h) => (
          <section key={h.years} className="flex flex-col gap-2 bg-surface px-4 py-3" aria-label={horizonName(h.years)}>
            <div className="flex items-baseline justify-between gap-2">
              <h3 className="text-[13.5px] font-semibold">{horizonName(h.years)}</h3>
              <span className="font-mono text-[11px] text-ink-3">{formatDay(h.on)}</span>
            </div>
            <HorizonBar statuses={h.statuses} label={horizonName(h.years)} />
            <div className="flex flex-col">
              <Label>Code currency</Label>
              <span className="flex items-baseline gap-2">
                <span className="tnum text-[24px] leading-none font-semibold">{pct(h.currency)}</span>
                {h.years > 0 && today && <Delta from={today.currency} to={h.currency} suffix=" pts" />}
              </span>
              <span className="text-[11px] text-ink-2">of components still supported</span>
            </div>
            <dl className="grid grid-cols-[1fr_auto] gap-x-3 gap-y-1 text-[12px]">
              <dt className="text-ink-2">Components past end of life</dt>
              <dd className="tnum text-right font-mono" style={{ color: h.eolComponents > 0 ? statusText('eol') : undefined }}>
                {h.eolComponents}
              </dd>
              <dt className="text-ink-2">Applications affected</dt>
              <dd className="tnum text-right font-mono">{h.eolApplications}</dd>
              <dt className="text-ink-2">High or critical code risk</dt>
              <dd className="tnum text-right font-mono">{h.highRiskApplications}</dd>
              <dt className="text-ink-2">Average code risk index</dt>
              <dd className="tnum text-right font-mono">{h.averageRiskIndex === null ? '—' : fmt(h.averageRiskIndex, 1)}</dd>
            </dl>
          </section>
        ))}
      </div>
      <ExplanationBlock explanation={codeExplanation(4, view.definitions)} definitions={view.definitions} />
    </Card>
  );
}

export function UrgencyChip({ urgency }: { urgency: string }) {
  const status = urgencyStatus(urgency);

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-sm px-1.5 py-0.5 text-[11px] font-semibold whitespace-nowrap ${
        status === 'unknown' ? 'border border-dashed border-rule' : ''
      }`}
      style={{ color: statusText(status), background: statusWash(status) }}
    >
      <span aria-hidden="true" className="size-1.5 rounded-full" style={{ background: statusMark(status) }} />
      {urgencyLabel(urgency)}
    </span>
  );
}

function Points({ now, avoided }: { now: number; avoided: number }) {
  if (now === 0 && avoided === 0) return <span className="text-ink-3">—</span>;

  return (
    <span className="tnum font-mono text-[11.5px] whitespace-nowrap">
      {now > 0 && <span className="font-semibold">−{fmt(now, 0)} now</span>}
      {now > 0 && avoided > 0 && ' · '}
      {avoided > 0 && <span className="text-ink-2">avoids {fmt(avoided, 0)}</span>}
    </span>
  );
}

/** El plan de mejora del portafolio: cada acción una vez, con todos los repositorios donde aplica. */
function PlanPanel({ actions, view }: { actions: readonly CodeActionRow[]; view: CodeView }) {
  return (
    <Card
      title="What should change first?"
      subtitle="The improvement plan: each action once, across every repository where it applies, most urgent first."
    >
      <div className="overflow-x-auto px-1 py-2">
        <table className="w-full min-w-[860px] text-[12.5px]">
          <thead>
            <tr className="border-b border-rule text-left">
              {['When', 'Action', 'Ends', 'Target supported until', 'Repos', 'Apps', 'Risk index'].map((heading) => (
                <Th key={heading} right={heading === 'Repos' || heading === 'Apps'}>
                  {heading}
                </Th>
              ))}
            </tr>
          </thead>
          <tbody>
            {actions.map((action) => (
              <tr
                key={`${action.kind}-${action.subject}-${action.current}-${action.target ?? ''}`}
                className="border-b border-rule align-top last:border-0"
              >
                <td className="px-3 py-2">
                  <UrgencyChip urgency={action.urgency} />
                </td>
                <td className="px-3 py-2">
                  <span className="font-medium text-ink">{actionText(action)}</span>
                  {action.ecosystem && <span className="block text-[11px] text-ink-3">{action.ecosystem}</span>}
                </td>
                <td className="px-3 py-2 text-ink-2">{action.deadline ? formatDay(action.deadline) : '—'}</td>
                <td className="px-3 py-2 text-ink-2">
                  {action.target === null ? '—' : action.targetEol ? formatDay(action.targetEol) : 'No date announced'}
                </td>
                <td className="tnum px-3 py-2 text-right font-mono">{action.repositories}</td>
                <td className="tnum px-3 py-2 text-right font-mono">{action.applications}</td>
                <td className="px-3 py-2">
                  <Points now={action.pointsNow} avoided={action.pointsAvoided} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {actions.length === 0 && <p className="px-3 py-4 text-[13px] text-ink-2">Nothing to change in the next three years.</p>}
      </div>
      <ExplanationBlock explanation={codeExplanation(6, view.definitions)} definitions={view.definitions} />
    </Card>
  );
}

// ------------------------------------------------------------ detalle en el tiempo

function Suggestions({ view }: { view: CodeApplicationView }) {
  const { suggestions } = view.detail;

  return (
    <section aria-labelledby="suggestions-title">
      <h3 id="suggestions-title" className="text-[13.5px] font-semibold">
        Suggested improvements
      </h3>
      {suggestions.length === 0 ? (
        <p className="mt-1.5 text-[12.5px] text-ink-2">Nothing to change in the next three years.</p>
      ) : (
        <ol className="mt-2 flex flex-col divide-y divide-[var(--app-rule)] rounded-md border border-rule">
          {suggestions.map((s) => (
            <li
              key={`${s.repository}-${s.kind}-${s.subject}-${s.current}`}
              className="grid gap-x-3 gap-y-1 px-3 py-2 text-[12.5px] sm:grid-cols-[auto_1fr_auto]"
            >
              <UrgencyChip urgency={s.urgency} />
              <span>
                <span className="font-medium text-ink">{actionText(s)}</span>
                <span className="block font-mono text-[11px] break-all text-ink-3">
                  {s.repository}
                  {s.deadline ? ` · ends ${formatDay(s.deadline)}` : ''}
                  {s.targetEol ? ` · target supported until ${formatDay(s.targetEol)}` : ''}
                </span>
              </span>
              <Points now={s.pointsNow} avoided={s.pointsAvoided} />
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function ApplicationOutlook({ view }: { view: CodeApplicationView }) {
  const { outlook } = view.detail;
  const today = outlook[0];

  return (
    <section aria-labelledby="app-outlook-title">
      <h3 id="app-outlook-title" className="text-[13.5px] font-semibold">
        Today and in 1, 2 and 3 years, if nothing changes
      </h3>
      <div className="mt-2 overflow-x-auto">
        <table className="w-full min-w-[560px] text-[12.5px]">
          <thead>
            <tr className="border-b border-rule text-left">
              <Th>Measure</Th>
              {outlook.map((h) => (
                <Th key={h.years} right>
                  {horizonName(h.years)}
                </Th>
              ))}
            </tr>
          </thead>
          <tbody>
            <tr className="border-b border-rule">
              <th scope="row" className="px-3 py-1.5 text-left font-normal text-ink-2">
                Components past end of life
              </th>
              {outlook.map((h) => (
                <td key={h.years} className="tnum px-3 py-1.5 text-right font-mono" style={{ color: h.eolComponents > 0 ? statusText('eol') : undefined }}>
                  {h.eolComponents} of {h.components}
                </td>
              ))}
            </tr>
            <tr className="border-b border-rule">
              <th scope="row" className="px-3 py-1.5 text-left font-normal text-ink-2">
                Ending soon
              </th>
              {outlook.map((h) => (
                <td key={h.years} className="tnum px-3 py-1.5 text-right font-mono">
                  {h.approachingComponents}
                </td>
              ))}
            </tr>
            <tr className="border-b border-rule">
              <th scope="row" className="px-3 py-1.5 text-left font-normal text-ink-2">
                Code currency
              </th>
              {outlook.map((h) => (
                <td key={h.years} className="tnum px-3 py-1.5 text-right font-mono">
                  {pct(h.currency)}
                </td>
              ))}
            </tr>
            <tr>
              <th scope="row" className="px-3 py-1.5 text-left font-normal text-ink-2">
                Code risk index
              </th>
              {outlook.map((h) => (
                <td key={h.years} className="px-3 py-1.5 text-right">
                  {h.riskIndex === null ? (
                    '—'
                  ) : (
                    <span className="inline-flex items-center gap-2">
                      <span className="tnum font-mono">{fmt(h.riskIndex, 0)}</span>
                      <LevelChip level={h.riskLevel} />
                    </span>
                  )}
                </td>
              ))}
            </tr>
          </tbody>
        </table>
      </div>
      {today && outlook.length > 1 && (
        <p className="mt-1.5 text-[11.5px] text-ink-2">
          Same versions, same advisories and no new commits: only the calendar moves.
        </p>
      )}
    </section>
  );
}

function ApplicationHistory({ view }: { view: CodeApplicationView }) {
  const points = view.history.filter((p) => p.score !== null || p.currency !== null || p.riskScore !== null);

  return (
    <section aria-labelledby="app-history-title">
      <h3 id="app-history-title" className="text-[13.5px] font-semibold">
        Across uploads
      </h3>
      {points.length < 2 ? (
        <p className="mt-1.5 text-[12.5px] text-ink-2">
          {points.length === 1 ? 'One upload so far: the comparison starts with the next one.' : 'No uploads with this application.'}
        </p>
      ) : (
        <div className="mt-2 overflow-x-auto">
          <table className="w-full min-w-[520px] text-[12.5px]">
            <thead>
              <tr className="border-b border-rule text-left">
                {['Upload', 'Modernization Score', 'Currency Score', 'Risk Score'].map((heading) => (
                  <Th key={heading} right={heading !== 'Upload'}>
                    {heading}
                  </Th>
                ))}
              </tr>
            </thead>
            <tbody>
              {points.map((p) => (
                <tr key={p.versionId} className="border-b border-rule last:border-0">
                  <td className="px-3 py-1.5">{p.label}</td>
                  <td className="tnum px-3 py-1.5 text-right font-mono">{p.score === null ? '—' : fmt(p.score, 2)}</td>
                  <td className="tnum px-3 py-1.5 text-right font-mono">{p.currency === null ? '—' : fmt(p.currency, 2)}</td>
                  <td className="tnum px-3 py-1.5 text-right font-mono">{p.riskScore === null ? '—' : fmt(p.riskScore, 2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
"""
t = t.rstrip("\r\n") + nl + block.replace("\n", nl)
p.write_text(t, encoding="utf-8", newline="")
print("ok components")
