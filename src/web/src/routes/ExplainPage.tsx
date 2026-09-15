import { Link, useParams } from 'react-router';
import { useExplain } from '../api/hooks';
import { ContributionRows, NumeratorComposition, WeightAvailability } from '../components/charts';
import {
  Card,
  QueryError,
  Field,
  Label,
  Spinner,
  fmt,
  fmtScore,
  pillarColor,
  pillarName,
} from '../components/primitives';
import { formatNotice } from '../i18n/notices';

/**
 * Pantalla 03 — "Como se calculo". Es el requisito 4 del encargo y la razon por
 * la que el motor produce un desglose completo en lugar de solo un numero:
 * todo lo que se ve aqui se puede reproducir a mano con la formula del BRD.
 */
export function ExplainPage() {
  const { eaiCode = '' } = useParams();
  const { data, isPending, error } = useExplain(eaiCode);

  if (isPending) return <Spinner label="Calculating the breakdown…" />;

  if (error) {
    return <QueryError error={error} title="The breakdown could not be loaded" />;
  }

  const { application, formula, waterfall, included, excluded, informational } = data;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Label>How it was calculated</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">{application.applicationName}</h1>
          <p className="tnum mt-0.5 font-mono text-[12.5px] text-ink-3">
            EAI {application.eaiCode} · ruleset {formula.rulesetVersion}
          </p>
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
          <Link to="/" className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2">
            Portfolio
          </Link>
        </nav>
      </header>

      {/* La formula del BRD con los numeros de esta aplicacion sustituidos. */}
      <Card title="The formula, with this application's numbers">
        <div className="flex flex-col gap-4 px-4 py-4">
          <p className="tnum overflow-x-auto font-mono text-[clamp(15px,2.4vw,22px)] font-medium whitespace-nowrap text-ink">
            {formula.expression}
          </p>

          <ol className="flex flex-col gap-1.5 border-t border-rule pt-3">
            {formula.steps.map((step) => (
              <li key={step.code} className="flex gap-2.5 font-mono text-[12px] text-ink-2">
                <span aria-hidden="true" className="text-ink-3">
                  ·
                </span>
                {formatNotice(step)}
              </li>
            ))}
          </ol>
        </div>
      </Card>

      <div className="grid gap-4 lg:grid-cols-[1.65fr_1fr]">
        <Card
          title="How the numerator is built"
          subtitle="Each segment is a metric. The gap at the end is what the application did not earn."
        >
          <div className="px-4 py-4">
            <NumeratorComposition steps={waterfall} numerator={formula.numerator} denominator={formula.denominator} />
          </div>
        </Card>

        <Card title="Weight that enters the calculation">
          <div className="flex flex-col gap-4 px-4 py-4">
            <WeightAvailability available={formula.denominator} />
            <p className="text-[13px] text-ink-2">
              {excluded.length} {excluded.length === 1 ? 'metric' : 'metrics'} without data{' '}
              {excluded.length === 1 ? 'is' : 'are'} left out of the numerator and the denominator. A high score
              with little weight behind it says less than the same score with the full weight.
            </p>
          </div>
        </Card>
      </div>

      <Card
        title="Contribution by metric"
        subtitle="Sorted from largest to smallest. The last column is points of the final score."
        right={<span className="tnum font-mono text-[11px] text-ink-3">weighted · points</span>}
      >
        <ContributionRows steps={waterfall} />
      </Card>

      {/* La tabla completa: de valor crudo a puntaje ponderado, con procedencia. */}
      <Card title="Included metrics" subtitle="From raw value to applied rule, to score, to weight, to weighted.">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[860px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>Metric</Th>
                <Th>Raw value</Th>
                <Th>Applied rule</Th>
                <Th align="right">Score</Th>
                <Th align="right">Weight</Th>
                <Th align="right">Weighted</Th>
                <Th>Provenance</Th>
              </tr>
            </thead>
            <tbody>
              {included.map((metric) => (
                <tr key={metric.metricKey} className="border-b border-rule last:border-0">
                  <td className="px-4 py-2.5">
                    <span className="flex items-center gap-2">
                      <span
                        aria-hidden="true"
                        className="size-2 shrink-0 rounded-[2px]"
                        style={{ background: pillarColor(metric.pillarKey) }}
                      />
                      <span className="font-medium text-ink">{metric.name}</span>
                    </span>
                    <span className="mt-0.5 block font-mono text-[11px] text-ink-3">{pillarName(metric.pillarKey)}</span>
                  </td>
                  <td className="px-4 py-2.5 text-ink-2">{formatNotice(metric.rawValue)}</td>
                  <td className="px-4 py-2.5 font-mono text-[12px] text-ink-2">{metric.ruleApplied}</td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                    {metric.mappedScore == null ? '—' : fmtScore(metric.mappedScore)}
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink-2">{fmt(metric.weight, 0)}%</td>
                  <td className="tnum px-4 py-2.5 text-right font-mono font-semibold text-ink">
                    {metric.weightedScore == null ? '—' : fmt(metric.weightedScore)}
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="block font-mono text-[11px] text-ink-2">
                      {metric.provenance.system} · {metric.provenance.field}
                    </span>
                    <span className="block font-mono text-[10.5px] text-ink-3">
                      {metric.provenance.report}
                      {metric.provenance.provider ? ` · via ${metric.provenance.provider}` : ''}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="bg-accent-wash">
                <td className="px-4 py-2.5 font-semibold text-ink" colSpan={5}>
                  Numerator
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono font-semibold text-ink">{fmt(formula.numerator)}</td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>
      </Card>

      {excluded.length > 0 && (
        <Card title="Excluded metrics" subtitle="No data in the source. They leave the numerator and the denominator.">
          <ul className="divide-y divide-rule">
            {excluded.map((metric) => (
              <li key={metric.metricKey} className="grid gap-x-4 gap-y-1 px-4 py-2.5 sm:grid-cols-[14rem_5rem_1fr]">
                <span className="flex items-center gap-2 text-ink">
                  <span
                    aria-hidden="true"
                    className="size-2 shrink-0 rounded-[2px] opacity-40"
                    style={{ background: pillarColor(metric.pillarKey) }}
                  />
                  {metric.name}
                </span>
                <span className="tnum font-mono text-[12px] text-ink-3">weight {fmt(metric.weight, 0)}</span>
                <span className="text-[13px] text-ink-2">{formatNotice(metric.reason)}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {informational.length > 0 && (
        <Card
          title="Informational metrics"
          subtitle="They are evaluated and shown, but their weight is 0: they do not change the score."
        >
          <ul className="divide-y divide-rule">
            {informational.map((metric) => (
              <li key={metric.metricKey} className="grid gap-x-4 gap-y-1 px-4 py-2.5 sm:grid-cols-[14rem_1fr_auto]">
                <span className="text-ink">{metric.name}</span>
                <span className="font-mono text-[12px] text-ink-2">{metric.ruleApplied}</span>
                <span className="tnum font-mono text-[12px] text-ink-3">
                  {metric.mappedScore == null ? '—' : fmtScore(metric.mappedScore)}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      <Card title="Application context">
        <div className="grid gap-4 px-4 py-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="LOB" value={application.lob} />
          <Field label="Sub-LOB" value={application.subLob} />
          <Field label="Country" value={application.country} />
          <Field label="CIO" value={application.cio} />
          <Field label="CRE" value={application.cre} />
          <Field label="Disposition" value={application.disposition} />
          <Field label="Roadmap" value={application.roadmap} />
          <Field label="Language" value={application.codingLanguage} />
        </div>
      </Card>
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
