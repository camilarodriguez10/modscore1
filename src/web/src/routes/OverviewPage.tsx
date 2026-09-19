import { Link, useParams } from 'react-router';
import { useOverview } from '../api/hooks';
import { ScoreHero, WeightAvailability } from '../components/charts';
import type { PillarCardView } from '../api/types';
import {
  Card,
  QueryError,
  Field,
  Label,
  Spinner,
  fmt,
  fmtScore,
  pillarColor,
} from '../components/primitives';
import { formatNotice } from '../i18n/notices';

/**
 * Pantalla 02 — resumen del score. Replica la lectura del dashboard v1.3:
 * valor crudo, x peso, ponderado, y el peso disponible frente al nominal.
 */
export function OverviewPage() {
  const { eaiCode = '' } = useParams();
  const { data, isPending, error } = useOverview(eaiCode);

  if (isPending) return <Spinner label="Loading the score…" />;

  if (error) {
    return <QueryError error={error} title="The application could not be loaded" />;
  }

  const { application, pillars } = data;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Label>Score overview</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">
            {application.applicationName}
          </h1>
          <p className="tnum mt-0.5 font-mono text-[12.5px] text-ink-3">
            EAI {application.eaiCode} · ruleset {data.rulesetVersion}
          </p>
        </div>

        <nav className="flex gap-2">
          <Link
            to={`/applications/${application.eaiCode}/calculation`}
            className="rounded-sm bg-accent px-3 py-1.5 text-[13px] font-medium text-on-accent hover:opacity-90"
          >
            See how it was calculated
          </Link>
          <Link
            to={`/applications/${application.eaiCode}/what-if`}
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            What-If
          </Link>
          <Link
            to={`/applications/${application.eaiCode}/roadmap`}
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            Future plan
          </Link>
          <Link
            to="/portfolio"
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            Portfolio
          </Link>
        </nav>
      </header>

      <div className="grid gap-4 lg:grid-cols-[1fr_1.4fr]">
        <Card title="Modernization Score">
          <div className="flex flex-col gap-5 px-4 py-4">
            <ScoreHero score={data.score} band={data.band} />
            <div className="border-t border-rule pt-4">
              <WeightAvailability available={data.denominator} />
            </div>
            <p className="tnum font-mono text-[12px] text-ink-3">
              numerator {fmt(data.numerator)} · denominator {fmt(data.denominator, 0)}
            </p>
          </div>
        </Card>

        <Card title="Identity">
          <div className="grid gap-4 px-4 py-4 sm:grid-cols-2">
            <Field label="LOB" value={application.lob} />
            <Field label="Sub-LOB" value={application.subLob} />
            <Field label="Region" value={application.region} />
            <Field label="Country" value={application.country} />
            <Field label="CIO" value={application.cio} />
            <Field label="CRE / AD Director" value={application.cre} />
            <Field label="Disposition" value={application.disposition} />
            <Field label="Roadmap" value={application.roadmap} />
            <Field label="Language" value={application.codingLanguage} />
            <Field
              label="Users"
              value={application.userCount == null ? '—' : application.userCount.toLocaleString('en-US')}
            />
            <Field
              label="Critical application"
              value={
                application.criticalApplication == null
                  ? '—'
                  : application.criticalApplication
                    ? 'Yes'
                    : 'No'
              }
            />
          </div>
        </Card>
      </div>

      {data.override && (
        <Card title="Manual override">
          <div className="px-4 py-4">
            <p className="text-[13px] text-ink-2">
              The calculated score is <span className="tnum font-mono">{fmt(data.score)}</span>. An
              override sets it to{' '}
              <span className="tnum font-mono font-semibold text-ink">
                {fmt(data.override.value)}
              </span>
              .
            </p>
            <p className="mt-1.5 text-[13px] text-ink-2">
              {data.override.reason} — {data.override.appliedBy}
            </p>
          </div>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        {pillars.map((pillar) => (
          <PillarPanel key={pillar.key} pillar={pillar} />
        ))}
      </div>
    </div>
  );
}

/**
 * Tarjeta de pilar del dashboard v1.3: el score del pilar, su peso disponible
 * frente al nominal, y cada metrica con su valor crudo, su peso y su ponderado.
 */
function PillarPanel({ pillar }: { pillar: PillarCardView }) {
  const color = pillarColor(pillar.key);

  return (
    <section className="flex flex-col rounded-md border border-rule bg-surface">
      <header
        className="flex items-baseline justify-between gap-3 rounded-t-md border-b px-4 py-3"
        style={{
          borderColor: 'var(--app-rule)',
          borderTop: `2px solid ${color}`,
        }}
      >
        <div>
          <h2 className="text-[13.5px] font-semibold text-ink">{pillar.name}</h2>
          <p className="tnum mt-0.5 font-mono text-[11px] text-ink-3">
            weight {fmt(pillar.availableWeight, 0)} of {fmt(pillar.totalWeight, 0)}
            {pillar.metricsExcluded > 0 && ` · ${pillar.metricsExcluded} without data`}
          </p>
        </div>
        <span className="tnum text-[26px] leading-none font-semibold" style={{ color }}>
          {pillar.score == null ? '—' : fmt(pillar.score)}
        </span>
      </header>

      <ul className="flex-1 divide-y divide-rule">
        {pillar.metrics.map((metric) => (
          <li key={metric.metricKey} className="px-4 py-2.5">
            <div className="flex items-baseline justify-between gap-3">
              <span
                className={`text-[13px] ${metric.isIncluded ? 'text-ink' : 'text-ink-3 line-through decoration-1'}`}
              >
                {metric.name}
              </span>
              <span className="tnum shrink-0 font-mono text-[12.5px] font-semibold text-ink">
                {metric.weightedScore == null ? '—' : fmt(metric.weightedScore)}
              </span>
            </div>
            <div className="tnum mt-0.5 flex items-baseline justify-between gap-3 font-mono text-[11px] text-ink-3">
              <span className="truncate">{formatNotice(metric.rawValue)}</span>
              <span className="shrink-0">
                {metric.mappedScore == null ? '' : `${fmtScore(metric.mappedScore)} × ${fmt(metric.weight, 0)}%`}
              </span>
            </div>
          </li>
        ))}
      </ul>

      <footer className="border-t border-rule px-4 py-2.5">
        <span className="tnum font-mono text-[11.5px] text-ink-3">
          pillar numerator {fmt(pillar.numerator)}
        </span>
      </footer>
    </section>
  );
}
