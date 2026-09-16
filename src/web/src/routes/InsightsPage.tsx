import { Link, useSearchParams } from 'react-router';
import { useInsights } from '../api/hooks';
import type {
  ComplexityClusterView,
  ModernizationCandidateView,
  ObsolescenceDriverView,
  PortfolioHealthView,
  ResilienceGapsView,
  RiskConcentrationView,
} from '../api/types';
import { toApiFilters, useFilters, type FilterKey } from '../filters';
import { countOf, formatNotice, groupLabel } from '../i18n/notices';
import {
  BandChip,
  Card,
  Label,
  QueryError,
  Spinner,
  bandColor,
  fmt,
  pillarColor,
  pillarName,
} from '../components/primitives';

/**
 * Pantalla 09 — las seis preguntas del portafolio.
 *
 * Cada seccion responde una pregunta y dice de que esta hecha la respuesta: no hay
 * un numero sin su aritmetica a la vista. El backend no redacta nada (ADR 0017):
 * manda codigos con datos y aqui se arma la frase.
 *
 * El filtro de la barra superior vale tambien aqui: las seis respuestas hablan
 * siempre del recorte que la persona tiene puesto, no del portafolio entero.
 */

/** Cuantas aplicaciones con la misma capacidad en un pais ya son una concentracion. */
const DUPLICATE_OPTIONS = [2, 3, 4, 5] as const;

const DEFAULT_DUPLICATES = 3;
const TOP = 10;

const COMPLEXITY_LABELS: Record<string, string> = {
  DuplicateCapabilityInCountry: 'Same capability, same country',
  CapabilityAcrossCountries: 'Same capability, many countries',
  DecommissionBacklog: 'Marked Eliminate, still active',
  LanguageSpread: 'One capability, many languages',
};

const RISK_GROUPINGS = [
  { key: 'lob', label: 'LOB' },
  { key: 'country', label: 'Country' },
  { key: 'cio', label: 'CIO' },
  { key: 'capability', label: 'Capability' },
] as const;

/** Que filtro aplica al abrir un grupo de riesgo. La capacidad no es un filtro del sitio. */
const FILTER_OF: Partial<Record<string, FilterKey>> = {
  lob: 'lob',
  country: 'country',
  cio: 'cio',
};

export function InsightsPage() {
  const [params, setParams] = useSearchParams();
  const { filters, toggle, search } = useFilters();

  const duplicates = DUPLICATE_OPTIONS.find((n) => String(n) === params.get('duplicates')) ?? DEFAULT_DUPLICATES;
  const riskBy = RISK_GROUPINGS.some((g) => g.key === params.get('riskBy'))
    ? params.get('riskBy')!
    : 'lob';

  const { data, isPending, error } = useInsights(toApiFilters(filters), { duplicates, top: TOP });

  const setParam = (key: string, value: string, fallback: string) => {
    const next = new URLSearchParams(params);
    if (value === fallback) next.delete(key);
    else next.set(key, value);
    setParams(next);
  };

  if (isPending) return <Spinner label="Reading the portfolio…" />;

  if (error) return <QueryError error={error} title="The portfolio questions could not be answered" />;

  if (data.health.applications === 0) {
    return (
      <Card title="Insights">
        <p className="px-4 py-6 text-[13px] text-ink-2">
          No application matches the filter, so there is nothing to answer about. Remove a filter
          above to see the portfolio again.
        </p>
      </Card>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <header>
        <Label>Insights</Label>
        <h1 className="mt-1 text-[22px] leading-tight font-semibold">
          Six questions about {countOf(data.health.applications, 'application')}
        </h1>
        <p className="mt-1 max-w-[80ch] text-[13px] text-ink-2">
          Every answer is computed from the scored portfolio and its roadmaps with rules{' '}
          <span className="font-mono text-[12px]">{data.rulesetVersion}</span>. Nothing here is an
          opinion: each row shows the arithmetic behind it.
        </p>
      </header>

      <Health health={data.health} healthyScore={data.options.healthyScore} />

      <Priorities
        candidates={data.priorities}
        search={search}
        significantUserBase={data.options.significantUserBase}
      />

      <Complexity
        clusters={data.complexity}
        duplicates={duplicates}
        onDuplicates={(value) => setParam('duplicates', String(value), String(DEFAULT_DUPLICATES))}
      />

      <Risk
        concentrations={data.risk}
        riskBy={riskBy}
        onRiskBy={(value) => setParam('riskBy', value, 'lob')}
        onFilter={toggle}
        active={filters}
      />

      <Obsolescence drivers={data.obsolescence} />

      <Resilience gaps={data.resilience} search={search} />
    </div>
  );
}

// ------------------------------------------------ 1. ¿que tan sano esta?

function Health({ health, healthyScore }: { health: PortfolioHealthView; healthyScore: number }) {
  const worst = health.bands.find((b) => b.code === 'critical');

  return (
    <Card
      title="How healthy is the portfolio?"
      subtitle={`Average and median of every score, how much of the model had data, and how many critical applications sit below ${fmt(healthyScore, 0)}.`}
    >
      <dl className="grid gap-x-6 gap-y-4 px-4 py-4 sm:grid-cols-2 lg:grid-cols-5">
        <Stat label="Applications" value={String(health.applications)} />
        <Stat label="Average score" value={fmt(health.averageScore)} />
        <Stat
          label="Median score"
          value={fmt(health.medianScore)}
          hint={
            Math.abs(health.averageScore - health.medianScore) >= 3
              ? 'A tail is pulling the average'
              : 'In line with the average'
          }
        />
        <Stat
          label="Data coverage"
          value={`${fmt(health.dataCoverage, 1)}%`}
          hint="Share of the model's weight that had data"
        />
        <Stat
          label="Critical below healthy"
          value={String(health.criticalBelowHealthy)}
          hint="Where to look first"
          {...(health.criticalBelowHealthy > 0 ? { color: 'var(--app-serious-text)' } : {})}
        />
      </dl>

      <div className="border-t border-rule px-4 py-4">
        <Label>Distribution by band</Label>
        <ul className="mt-2 flex flex-col gap-2">
          {health.bands.map((band) => (
            <li key={band.code} className="grid grid-cols-[9.5rem_1fr_4.5rem] items-center gap-3">
              <BandChip band={band.code} />
              <span className="h-2 rounded-sm bg-surface-2">
                <span
                  className="block h-2 rounded-sm"
                  style={{ width: `${band.share}%`, background: bandColor(band.code) }}
                />
              </span>
              <span className="tnum text-right font-mono text-[12px] text-ink-2">
                {band.applications} · {fmt(band.share, 1)}%
              </span>
            </li>
          ))}
        </ul>

        {worst && worst.applications > 0 && (
          <p className="mt-3 text-[12.5px] text-ink-3">
            {countOf(worst.applications, 'application')} in the critical band. Their score is below
            every threshold the ruleset considers acceptable.
          </p>
        )}
      </div>
    </Card>
  );
}

// -------------------------------------- 2. ¿que modernizar primero?

function Priorities({
  candidates,
  search,
  significantUserBase,
}: {
  candidates: readonly ModernizationCandidateView[];
  search: string;
  significantUserBase: number;
}) {
  return (
    <Card
      title="Which applications should be modernized first?"
      subtitle={`Impact against effort: the uplift comes from running the application's own roadmap, the weeks from its milestones, and the business weight from being critical, serving ${significantUserBase.toLocaleString('en-US')}+ users or covering three or more capabilities.`}
    >
      {candidates.length === 0 ? (
        <p className="px-4 py-6 text-[13px] text-ink-2">
          No application in this selection has a roadmap that raises its score, so there is nothing
          to rank by value per week.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[920px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>Application</Th>
                <Th align="right">Today</Th>
                <Th align="right">When finished</Th>
                <Th align="right">Uplift</Th>
                <Th align="right">Weeks</Th>
                <Th align="right">Weight</Th>
                <Th align="right">Points / week</Th>
                <Th>Why it is here</Th>
              </tr>
            </thead>
            <tbody>
              {candidates.map((candidate) => (
                <tr key={candidate.eaiCode} className="border-b border-rule last:border-0">
                  <td className="px-4 py-2.5">
                    <Link
                      to={`/applications/${candidate.eaiCode}/roadmap${search}`}
                      className="font-medium text-accent underline-offset-2 hover:underline"
                    >
                      {candidate.applicationName}
                    </Link>
                    <span className="mt-0.5 block font-mono text-[11px] text-ink-3">
                      {candidate.eaiCode}
                      {candidate.country ? ` · ${candidate.country}` : ''}
                      {candidate.roadmapName ? ` · ${candidate.roadmapName}` : ''}
                    </span>
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink-2">
                    {fmt(candidate.currentScore)}
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                    {candidate.projectedScore === null ? '—' : fmt(candidate.projectedScore)}
                  </td>
                  <td
                    className="tnum px-4 py-2.5 text-right font-mono"
                    style={{ color: 'var(--app-good-text)' }}
                  >
                    +{fmt(candidate.uplift)}
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">{candidate.weeks}</td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">
                    ×{fmt(candidate.businessWeight, 2)}
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono font-semibold text-ink">
                    {fmt(candidate.valuePerWeek)}
                  </td>
                  <td className="px-4 py-2.5">
                    <ul className="flex flex-col gap-0.5">
                      {candidate.evidence.map((notice) => (
                        <li key={notice.code} className="text-[12px] text-ink-2">
                          {formatNotice(notice)}
                        </li>
                      ))}
                    </ul>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

// ------------------------------------- 3. ¿donde reducir complejidad?

function Complexity({
  clusters,
  duplicates,
  onDuplicates,
}: {
  clusters: readonly ComplexityClusterView[];
  duplicates: number;
  onDuplicates: (value: number) => void;
}) {
  return (
    <Card
      title="Where should we reduce complexity?"
      subtitle="Four shapes of concentration: the same capability more than once in a country, the same capability solved differently across countries, applications marked Eliminate that are still active, and one capability written in many languages."
      right={
        <div className="flex items-center gap-2">
          <Label>Duplicate from</Label>
          <div className="flex gap-1">
            {DUPLICATE_OPTIONS.map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => onDuplicates(option)}
                aria-pressed={duplicates === option}
                title={`Flag a capability once ${option} applications share it in a country`}
                className={`tnum rounded-sm px-2 py-0.5 font-mono text-[12px] ${
                  duplicates === option
                    ? 'bg-accent-wash font-semibold text-accent'
                    : 'border border-rule text-ink-2 hover:bg-surface-2'
                }`}
              >
                {option}
              </button>
            ))}
          </div>
        </div>
      }
    >
      {clusters.length === 0 ? (
        <p className="px-4 py-6 text-[13px] text-ink-2">
          Nothing reaches the threshold of {duplicates} applications. Lower it above to see smaller
          concentrations.
        </p>
      ) : (
        <ul className="divide-y divide-rule">
          {clusters.map((cluster) => (
            <li key={`${cluster.kind}-${cluster.dimension}`} className="px-4 py-3">
              <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                <div>
                  <p className="text-[13.5px] text-ink">{formatNotice(cluster.evidence)}</p>
                  <p className="mt-0.5 font-mono text-[11px] text-ink-3">
                    {COMPLEXITY_LABELS[cluster.kind] ?? cluster.kind}
                  </p>
                </div>
                <p className="tnum font-mono text-[12px] text-ink-2">
                  {cluster.applications} apps · {cluster.users.toLocaleString('en-US')} users · avg{' '}
                  {fmt(cluster.averageScore)}
                </p>
              </div>

              <ul className="mt-1.5 flex flex-wrap gap-x-3 gap-y-1">
                {cluster.eaiCodes.map((eai) => (
                  <li key={eai}>
                    <Link
                      to={`/applications/${eai}`}
                      className="font-mono text-[11.5px] text-accent underline-offset-2 hover:underline"
                    >
                      {eai}
                    </Link>
                  </li>
                ))}
              </ul>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

// ------------------------------- 4. ¿donde se concentra el riesgo?

function Risk({
  concentrations,
  riskBy,
  onRiskBy,
  onFilter,
  active,
}: {
  concentrations: readonly RiskConcentrationView[];
  riskBy: string;
  onRiskBy: (value: string) => void;
  onFilter: (key: FilterKey, value: string) => void;
  active: Record<string, string | undefined>;
}) {
  const rows = concentrations.filter((c) => c.grouping === riskBy);
  const top = rows[0]?.riskIndex ?? 0;
  const filterKey = FILTER_OF[riskBy];

  return (
    <Card
      title="Where is technology risk concentrated?"
      subtitle="Critical and high flaws, weak vulnerability management, obsolete or unapproved components, and critical applications exposed to the internet. The index weighs them together to rank groups inside this portfolio."
      right={
        <div className="flex items-center gap-2">
          <Label>Group by</Label>
          <div className="flex gap-1">
            {RISK_GROUPINGS.map((option) => (
              <button
                key={option.key}
                type="button"
                onClick={() => onRiskBy(option.key)}
                aria-pressed={riskBy === option.key}
                className={`rounded-sm px-2 py-0.5 text-[12px] ${
                  riskBy === option.key
                    ? 'bg-accent-wash font-medium text-accent'
                    : 'border border-rule text-ink-2 hover:bg-surface-2'
                }`}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>
      }
    >
      {rows.length === 0 ? (
        <p className="px-4 py-6 text-[13px] text-ink-2">
          No group in this selection carries a risk signal. Try another grouping above.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[880px] text-[13px]">
            <thead>
              <tr className="border-b border-rule bg-surface-2">
                <Th>Group</Th>
                <Th align="right">Apps</Th>
                <Th align="right">Critical flaws</Th>
                <Th align="right">High flaws</Th>
                <Th align="right">Weak vuln. mgmt</Th>
                <Th align="right">Obsolete parts</Th>
                <Th align="right">Exposed critical</Th>
                <Th align="right">Avg score</Th>
                <Th>Index</Th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={`${row.grouping}-${row.dimension}`} className="border-b border-rule last:border-0">
                  <td className="px-4 py-2.5">
                    {filterKey ? (
                      <button
                        type="button"
                        onClick={() => onFilter(filterKey, row.dimension)}
                        aria-pressed={active[filterKey] === row.dimension}
                        title={`Filter every screen by ${groupLabel(row.dimension)}`}
                        className="font-medium text-accent underline-offset-2 hover:underline"
                      >
                        {groupLabel(row.dimension)}
                      </button>
                    ) : (
                      <span className="font-medium text-ink">{groupLabel(row.dimension)}</span>
                    )}
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">{row.applications}</td>
                  <Count value={row.criticalFlaws} tone="var(--app-critical-text)" />
                  <Count value={row.highFlaws} tone="var(--app-serious-text)" />
                  <Count value={row.weakVulnerabilityManagement} />
                  <Count value={row.obsoleteComponents} />
                  <Count value={row.internetFacingCritical} tone="var(--app-critical-text)" />
                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink-2">
                    {fmt(row.averageScore)}
                  </td>
                  <td className="px-4 py-2.5">
                    <span className="flex items-center gap-2">
                      <span className="h-2 w-24 rounded-sm bg-surface-2">
                        <span
                          className="block h-2 rounded-sm"
                          style={{
                            width: `${top === 0 ? 0 : (row.riskIndex / top) * 100}%`,
                            background: 'var(--app-serious-text)',
                          }}
                        />
                      </span>
                      <span className="tnum font-mono text-[12px] text-ink-2">{fmt(row.riskIndex, 0)}</span>
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

function Count({ value, tone }: { value: number; tone?: string }) {
  return (
    <td
      className="tnum px-4 py-2.5 text-right font-mono"
      style={value > 0 && tone ? { color: tone } : undefined}
    >
      {value === 0 ? <span className="text-ink-3">—</span> : value.toLocaleString('en-US')}
    </td>
  );
}

// ---------------------------------- 5. ¿que causa la obsolescencia?

function Obsolescence({ drivers }: { drivers: readonly ObsolescenceDriverView[] }) {
  const top = drivers[0]?.lostPoints ?? 0;
  const blind = drivers.filter((d) => d.weightWithoutData > 0);

  return (
    <Card
      title="What is driving obsolescence?"
      subtitle="The points the portfolio loses on each metric: its weight times the share of the score it did not earn, added across every application. Weight with no data is listed apart — that is blindness, not obsolescence."
    >
      <div className="overflow-x-auto">
        <table className="w-full min-w-[760px] text-[13px]">
          <thead>
            <tr className="border-b border-rule bg-surface-2">
              <Th>Metric</Th>
              <Th>Pillar</Th>
              <Th align="right">Apps affected</Th>
              <Th align="right">Weight without data</Th>
              <Th align="right">Share</Th>
              <Th>Points lost</Th>
            </tr>
          </thead>
          <tbody>
            {drivers.map((driver) => (
              <tr key={driver.metricKey} className="border-b border-rule last:border-0">
                <td className="px-4 py-2.5">
                  <span className="font-medium text-ink">{driver.name}</span>
                  <span className="mt-0.5 block font-mono text-[11px] text-ink-3">{driver.metricKey}</span>
                </td>
                <td className="px-4 py-2.5">
                  <span
                    className="inline-flex items-center gap-1.5 text-[12px] text-ink-2"
                    title={pillarName(driver.pillarKey)}
                  >
                    <span
                      aria-hidden="true"
                      className="size-1.5 rounded-full"
                      style={{ background: pillarColor(driver.pillarKey) }}
                    />
                    {pillarName(driver.pillarKey)}
                  </span>
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">
                  {driver.applicationsAffected}
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono text-ink-3">
                  {driver.weightWithoutData === 0 ? '—' : fmt(driver.weightWithoutData, 1)}
                </td>
                <td className="tnum px-4 py-2.5 text-right font-mono text-ink-2">
                  {fmt(driver.share, 1)}%
                </td>
                <td className="px-4 py-2.5">
                  <span className="flex items-center gap-2">
                    <span className="h-2 w-28 rounded-sm bg-surface-2">
                      <span
                        className="block h-2 rounded-sm"
                        style={{
                          width: `${top === 0 ? 0 : (driver.lostPoints / top) * 100}%`,
                          background: pillarColor(driver.pillarKey),
                        }}
                      />
                    </span>
                    <span className="tnum font-mono text-[12px] text-ink-2">{fmt(driver.lostPoints, 1)}</span>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {blind.length > 0 && (
        <p className="border-t border-rule px-4 py-3 text-[12.5px] text-ink-3">
          {blind.length === 1 ? 'One metric has' : `${blind.length} metrics have`} weight that never
          entered a score because the source had no value. Filling that data changes the answer to
          this question before any modernization does.
        </p>
      )}
    </Card>
  );
}

// ------------------------------- 6. ¿donde faltan defensas?

function Resilience({ gaps, search }: { gaps: ResilienceGapsView; search: string }) {
  return (
    <Card
      title="Where are the resilience gaps?"
      subtitle="Critical applications outside the disaster recovery scope or with no monitoring, applications with a low operational score, and applications with no recovery objectives declared."
    >
      <dl className="grid gap-x-6 gap-y-4 border-b border-rule px-4 py-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat
          label="Critical without DR"
          value={String(gaps.criticalWithoutDisasterRecovery)}
          {...(gaps.criticalWithoutDisasterRecovery > 0 ? { color: 'var(--app-critical-text)' } : {})}
        />
        <Stat
          label="Critical without monitoring"
          value={String(gaps.criticalWithoutMonitoring)}
          {...(gaps.criticalWithoutMonitoring > 0 ? { color: 'var(--app-serious-text)' } : {})}
        />
        <Stat label="Low operational score" value={String(gaps.withLowOperationalScore)} />
        <Stat label="Without RTO or RPO" value={String(gaps.withoutRecoveryObjectives)} />
      </dl>

      {gaps.applications.length === 0 ? (
        <p className="px-4 py-6 text-[13px] text-ink-2">
          No application in this selection is missing a continuity defence.
        </p>
      ) : (
        <ul className="divide-y divide-rule">
          {gaps.applications.map((application) => (
            <li key={application.eaiCode} className="grid gap-x-4 gap-y-1 px-4 py-3 sm:grid-cols-[1fr_auto]">
              <div>
                <Link
                  to={`/applications/${application.eaiCode}${search}`}
                  className="text-[13.5px] font-medium text-accent underline-offset-2 hover:underline"
                >
                  {application.applicationName}
                </Link>
                {application.critical && (
                  <span
                    className="ml-2 rounded-sm px-1.5 py-0.5 font-mono text-[10.5px] font-semibold"
                    style={{ color: 'var(--app-critical-text)', background: 'var(--app-critical-wash)' }}
                  >
                    CRITICAL
                  </span>
                )}
                <span className="mt-0.5 block font-mono text-[11px] text-ink-3">
                  {application.eaiCode}
                  {application.lob ? ` · ${application.lob}` : ''}
                  {application.country ? ` · ${application.country}` : ''}
                  {` · RTO ${application.recoveryTimeObjective ?? '—'} · RPO ${application.recoveryPointObjective ?? '—'}`}
                </span>

                <ul className="mt-1 flex flex-wrap gap-x-4 gap-y-0.5">
                  {application.gaps.map((notice) => (
                    <li key={notice.code} className="text-[12px] text-ink-2">
                      {formatNotice(notice)}
                    </li>
                  ))}
                </ul>
              </div>

              <span className="tnum self-center font-mono text-[13px] text-ink-2 sm:text-right">
                {fmt(application.score)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

// -----------------------------------------------------------------------

function Stat({
  label,
  value,
  hint,
  color,
}: {
  label: string;
  value: string;
  hint?: string;
  color?: string;
}) {
  return (
    <div>
      <dt>
        <Label>{label}</Label>
      </dt>
      {/* La pista va dentro del <dd>: un <dl> solo admite pares dt/dd, y un parrafo
          suelto entre ellos deja fuera a quien navega por estructura. */}
      <dd
        className="tnum mt-0.5 text-[20px] leading-none font-semibold text-ink"
        style={color ? { color } : undefined}
      >
        {value}
        {hint && <span className="mt-1 block text-[11.5px] font-normal text-ink-3">{hint}</span>}
      </dd>
    </div>
  );
}

function Th({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'right' }) {
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
