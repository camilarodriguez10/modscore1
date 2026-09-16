import { Link, useParams } from 'react-router';
import { useRoadmapPlan } from '../api/hooks';
import type { AlertSeverity, AlertView, MilestoneView } from '../api/types';
import { BandChip, Card, QueryError, Field, Label, Spinner, fmt, pillarColor } from '../components/primitives';
import { countOf, formatNotice } from '../i18n/notices';

/**
 * Pantalla 05 — plan futuro. Es el requisito 6 del encargo: que le va a pasar a
 * esta aplicacion segun su disposition y su roadmap, por que pasos, y con que
 * resultado.
 *
 * El score proyectado no es un numero escrito a mano: sale del motor de what-if
 * aplicando las palancas que ese roadmap entrega.
 */
export function RoadmapPage() {
  const { eaiCode = '' } = useParams();
  const { data, isPending, error } = useRoadmapPlan(eaiCode);

  if (isPending) return <Spinner label="Building the future plan…" />;

  if (error) {
    return <QueryError error={error} title="The plan could not be loaded" />;
  }

  const { application } = data;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Label>Future plan</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">{application.applicationName}</h1>
          <p className="tnum mt-0.5 font-mono text-[12.5px] text-ink-3">EAI {application.eaiCode}</p>
        </div>

        <nav className="flex gap-2">
          <Link
            to={`/applications/${application.eaiCode}`}
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            View overview
          </Link>
          <Link
            to={`/applications/${application.eaiCode}/what-if`}
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            What-If
          </Link>
        </nav>
      </header>

      {/* Las alertas van arriba: si el plan se contradice con los datos, eso es
          lo primero que hay que saber. */}
      {data.alerts.length > 0 && (
        <ul className="flex flex-col gap-2">
          {data.alerts.map((alert) => (
            <AlertBanner key={alert.ruleKey} alert={alert} />
          ))}
        </ul>
      )}

      <Card title="Disposition and roadmap">
        <div className="flex flex-col gap-5 px-4 py-5">
          <p className="max-w-[70ch] text-[15px] text-ink">{formatNotice(data.outlook)}</p>

          <div className="grid gap-5 md:grid-cols-2">
            <div>
              <Label>Disposition</Label>
              <p className="mt-1 text-[16px] font-semibold text-ink">{data.disposition ?? '—'}</p>
              <p className="mt-1 max-w-[46ch] text-[13px] text-ink-2">
                {data.dispositionDefinition ?? 'No disposition assigned.'}
              </p>
            </div>

            <div>
              <Label>Roadmap</Label>
              <p className="mt-1 text-[16px] font-semibold text-ink">{data.roadmap ?? '—'}</p>
              <p className="mt-1 max-w-[46ch] text-[13px] text-ink-2">
                {data.roadmapDefinition ?? 'No roadmap assigned.'}
              </p>
            </div>
          </div>
        </div>

        <div className="grid gap-6 border-t border-rule px-4 py-5 sm:grid-cols-[auto_auto_1fr] sm:items-end">
          <div>
            <Label>Score today</Label>
            <p className="tnum mt-1 text-[38px] leading-none font-semibold text-ink">{fmt(data.baselineScore)}</p>
            <div className="mt-2">
              <BandChip band={data.baselineBand} />
            </div>
          </div>

          <div className="flex items-center gap-3">
            <span aria-hidden="true" className="text-[22px] text-ink-3">
              →
            </span>
            <div>
              <Label>When the roadmap finishes</Label>
              {data.projectedScore === null ? (
                <p className="mt-1 max-w-[34ch] text-[13px] text-ink-2">
                  {data.leavesPortfolio
                    ? 'The application leaves the portfolio: its score does not improve, it stops existing.'
                    : 'This roadmap does not move any score metric.'}
                </p>
              ) : (
                <>
                  <p className="tnum mt-1 flex items-baseline gap-2 text-[38px] leading-none font-semibold text-ink">
                    {fmt(data.projectedScore)}
                    <span className="tnum text-[15px] font-semibold" style={{ color: 'var(--app-good-text)' }}>
                      +{fmt(data.projectedDelta)}
                    </span>
                  </p>
                  {data.projectedBand && (
                    <div className="mt-2">
                      <BandChip band={data.projectedBand} />
                    </div>
                  )}
                </>
              )}
            </div>
          </div>

          <div className="sm:justify-self-end sm:text-right">
            <Label>Duration</Label>
            <p className="tnum mt-1 text-[20px] leading-none font-semibold text-ink-2">{countOf(data.totalWeeks, 'week')}</p>
            {data.completionDate && (
              <p className="tnum mt-1 font-mono text-[11px] text-ink-3">ends {formatDate(data.completionDate)}</p>
            )}
          </div>
        </div>
      </Card>

      {/* Que palancas del score entrega este roadmap, y por que esas. */}
      {data.deliveredBy.length > 0 && (
        <Card title="What this roadmap improves" subtitle={data.leverRationale ?? undefined}>
          <ul className="divide-y divide-rule">
            {data.deliveredBy.map((lever) => (
              <li key={lever.key} className="flex items-start gap-2.5 px-4 py-2.5">
                <span
                  aria-hidden="true"
                  className="mt-1.5 size-2 shrink-0 rounded-[2px]"
                  style={{ background: pillarColor(lever.pillarKey) }}
                />
                <div>
                  <p className="text-[13.5px] font-medium text-ink">{lever.name}</p>
                  <p className="mt-0.5 text-[12.5px] text-ink-2">{lever.action}</p>
                </div>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {data.milestones.length > 0 && (
        <Card
          title="Execution plan"
          subtitle={`Starting ${formatDate(data.startDate)}`}
          right={
            <span className="tnum font-mono text-[11px] text-ink-3">
              {countOf(data.milestones.length, 'milestone')} · {countOf(data.totalWeeks, 'week')}
            </span>
          }
        >
          <div className="px-4 pt-4">
            <Timeline milestones={data.milestones} />
          </div>

          <ol className="mt-2 divide-y divide-rule">
            {data.milestones.map((milestone) => (
              <li
                key={milestone.order}
                className="grid gap-x-4 gap-y-1 px-4 py-3 sm:grid-cols-[2rem_1fr_9rem_auto] sm:items-baseline"
              >
                <span className="tnum font-mono text-[12px] text-ink-3">{milestone.order}.</span>
                <span className="text-[13.5px] text-ink">{milestone.name}</span>
                <span className="font-mono text-[11.5px] text-ink-3">{milestone.owner}</span>
                <span className="tnum font-mono text-[11.5px] text-ink-2 sm:text-right">
                  {formatDate(milestone.start)} — {formatDate(milestone.end)}
                  <span className="ml-2 text-ink-3">{milestone.weeks} wk</span>
                </span>
              </li>
            ))}
          </ol>
        </Card>
      )}

      <Card title="Application context">
        <div className="grid gap-4 px-4 py-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="LOB" value={application.lob} />
          <Field label="Country" value={application.country} />
          <Field label="CIO" value={application.cio} />
          <Field label="CRE / AD Director" value={application.cre} />
          <Field
            label="Critical application"
            value={application.criticalApplication == null ? '—' : application.criticalApplication ? 'Yes' : 'No'}
          />
          <Field label="Language" value={application.codingLanguage} />
          <Field
            label="Users"
            value={application.userCount == null ? '—' : application.userCount.toLocaleString('en-US')}
          />
          <Field label="Weight with data" value={`${fmt(data.baselineWeight, 0)} of 100`} />
        </div>
      </Card>
    </div>
  );
}

/**
 * Barra de hitos proporcional a su duracion. El ancho de cada tramo es su peso
 * real en el plan, no un reparto igual.
 */
function Timeline({ milestones }: { milestones: readonly MilestoneView[] }) {
  return (
    <div>
      <div className="flex h-7 gap-[2px] overflow-hidden rounded-sm">
        {milestones.map((milestone, index) => (
          <div
            key={milestone.order}
            title={`${milestone.name} · ${countOf(milestone.weeks, 'week')}`}
            className="flex min-w-[3px] items-center justify-center bg-accent text-[10px] font-semibold text-on-accent"
            style={{
              flexGrow: milestone.weeks,
              flexBasis: 0,
              // Cada hito un poco mas claro que el anterior: se lee el avance.
              opacity: 0.55 + (index / Math.max(1, milestones.length - 1)) * 0.45,
            }}
          >
            {milestone.sharePercent > 8 && milestone.order}
          </div>
        ))}
      </div>
      <p className="mt-1.5 font-mono text-[10.5px] text-ink-3">width proportional to each milestone's duration</p>
    </div>
  );
}

/** Una contradiccion entre el plan declarado y los datos, con su evidencia. */
function AlertBanner({ alert }: { alert: AlertView }) {
  const { color, wash, icon } = severityStyle(alert.severity);

  return (
    <li
      className="flex gap-3 rounded-md border px-4 py-3"
      style={{ borderColor: `color-mix(in srgb, ${color} 32%, transparent)`, background: wash }}
    >
      <span aria-hidden="true" className="mt-0.5 text-[13px] font-bold" style={{ color }}>
        {icon}
      </span>
      <div>
        <p className="text-[13.5px] font-semibold" style={{ color }}>
          {alert.title}
        </p>
        <p className="mt-0.5 text-[13px] text-ink-2">{alert.detail}</p>
        <p className="mt-1 font-mono text-[11.5px] text-ink-3">{formatNotice(alert.evidence)}</p>
      </div>
    </li>
  );
}

/** El icono acompana siempre al color: la gravedad no se lee solo por el tono. */
export function severityStyle(severity: AlertSeverity): {
  color: string;
  wash: string;
  icon: string;
} {
  switch (severity) {
    case 'Critical':
      return { color: 'var(--app-critical-text)', wash: 'var(--app-critical-wash)', icon: '!' };
    case 'Warning':
      return { color: 'var(--app-serious-text)', wash: 'var(--app-serious-wash)', icon: '▲' };
    default:
      return { color: 'var(--app-warning-text)', wash: 'var(--app-warning-wash)', icon: 'i' };
  }
}

const dateFormat = new Intl.DateTimeFormat('en-US', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  timeZone: 'UTC',
});

/** Una fecha ISO ("2026-09-12") en ingles: "Sep 12, 2026". */
export function formatDate(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number);
  if (!year || !month || !day) return iso;
  return dateFormat.format(new Date(Date.UTC(year, month - 1, day)));
}
