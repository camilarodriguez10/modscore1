import { useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { ApiError, exportUrls } from '../api/client';
import { useRules, useRulesComparison, useSimulateRules } from '../api/hooks';
import type {
  ApplicationComparison,
  MetricComparison,
  Notice,
  RulesAdminView,
  RulesImpact,
  RulesVersionView,
} from '../api/types';
import {
  BandChip,
  Card,
  ErrorPanel,
  Label,
  QueryError,
  Spinner,
  fmt,
  fmtScore,
  pillarColor,
  pillarName,
} from '../components/primitives';
import { bandLabel, countOf, formatNotice } from '../i18n/notices';
import { draftFrom, loadEditor } from '../rules/draft';

/**
 * Pantalla 10 — comparar reglas.
 *
 * Responde la pregunta de antes de publicar: que cambia entre dos versiones, cuanto se
 * mueve el portafolio y que le pasa a una aplicacion concreta, metrica por metrica. La
 * version de destino puede ser el borrador sin publicar de este navegador: se simula en el
 * servidor con los mismos motores que puntuaran despues, y no se guarda nada.
 *
 * La URL lleva la comparacion (`?from=v1.3&to=v1.4&eai=14381`): se puede compartir con otra
 * persona que administre reglas.
 */

const DRAFT = 'draft';
const EAI_PATTERN = /^\d{1,10}$/;

export function RulesComparePage() {
  const rules = useRules();

  if (rules.isPending) return <Spinner label="Loading the rules versions…" />;

  if (rules.error) {
    return <QueryError error={rules.error} title="The rules could not be loaded" />;
  }

  return <CompareWorkspace view={rules.data} />;
}

function versionLabel(version: RulesVersionView): string {
  return `${version.id}${version.isActive ? ' · active' : ''}${version.isBuiltIn ? ' · built in' : ''}`;
}

function CompareWorkspace({ view }: { view: RulesAdminView }) {
  const [params, setParams] = useSearchParams();
  const editor = useMemo(() => loadEditor(), []);

  const ids = view.versions.map((version) => version.id);
  const active = view.active.id;
  const defaultFrom =
    view.active.basedOn && ids.includes(view.active.basedOn) ? view.active.basedOn : (ids[1] ?? active);

  const requestedFrom = params.get('from') ?? '';
  const requestedTo = params.get('to') ?? '';
  const requestedEai = params.get('eai') ?? '';

  const to = requestedTo === DRAFT || ids.includes(requestedTo) ? requestedTo : active;
  const draftMode = to === DRAFT;
  const from = draftMode ? active : ids.includes(requestedFrom) ? requestedFrom : defaultFrom;
  const eai = EAI_PATTERN.test(requestedEai) ? requestedEai : '';

  const [eaiInput, setEaiInput] = useState(eai);
  const eaiInputValid = eaiInput.trim() === '' || EAI_PATTERN.test(eaiInput.trim());

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    setParams(next, { replace: true });
  };

  const pickApplication = (code: string) => {
    setEaiInput(code);
    setParam('eai', code);
  };

  const toOptions = [
    ...view.versions.map((version) => ({ value: version.id, label: versionLabel(version) })),
    ...(editor ? [{ value: DRAFT, label: `Your draft (on ${editor.basedOn})` }] : []),
  ];

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <Label>Scoring rules</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">Compare rules</h1>
          <p className="mt-0.5 max-w-[78ch] text-[13px] text-ink-2">
            What changes between two versions, how the whole portfolio moves and what happens to one
            application, metric by metric. Choose your draft as the target to see its effect before
            publishing.
          </p>
        </div>
        <Link to="/admin/rules" className="text-[13px] text-accent underline-offset-2 hover:underline">
          Back to the rules editor
        </Link>
      </header>

      <Card>
        <form
          className="flex flex-wrap items-end gap-4 px-4 py-4"
          onSubmit={(event) => {
            event.preventDefault();
            const code = eaiInput.trim();
            if (code === '' || EAI_PATTERN.test(code)) setParam('eai', code);
          }}
        >
          <Select
            label="From"
            value={from}
            disabled={draftMode}
            onChange={(value) => setParam('from', value)}
            options={view.versions.map((version) => ({ value: version.id, label: versionLabel(version) }))}
          />
          <Select label="To" value={to} onChange={(value) => setParam('to', value)} options={toOptions} />

          <label className="flex flex-col gap-1.5">
            <Label>Application (EAI code)</Label>
            <input
              value={eaiInput}
              onChange={(event) => setEaiInput(event.target.value)}
              inputMode="numeric"
              maxLength={10}
              placeholder="e.g. 14381"
              aria-invalid={!eaiInputValid}
              className="w-36 rounded-sm border border-rule bg-surface px-2 py-1.5 font-mono text-[13px]"
            />
          </label>

          <button
            type="submit"
            disabled={!eaiInputValid}
            className="rounded-sm bg-accent px-4 py-1.5 text-[13px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Show application
          </button>

          {eai && (
            <button
              type="button"
              onClick={() => pickApplication('')}
              className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
            >
              Clear application
            </button>
          )}

          {!draftMode && (
            <a
              href={exportUrls.rulesComparison(from, to)}
              download
              className="ml-auto rounded-sm border border-rule bg-surface px-3 py-1.5 text-[13px] hover:bg-surface-2"
              title="Every application with its score in both versions"
            >
              Download CSV
            </a>
          )}
        </form>
        {!eaiInputValid && (
          <p className="px-4 pb-3 text-[12.5px]" style={{ color: 'var(--app-critical)' }}>
            An EAI code is a sequence of up to 10 digits.
          </p>
        )}
      </Card>

      {draftMode ? (
        <DraftComparison view={view} eai={eai} onPickApplication={pickApplication} />
      ) : (
        <VersionComparison from={from} to={to} eai={eai} onPickApplication={pickApplication} />
      )}
    </div>
  );
}

// ------------------------------------------------------------ versiones

function VersionComparison({
  from,
  to,
  eai,
  onPickApplication,
}: {
  from: string;
  to: string;
  eai: string;
  onPickApplication: (eai: string) => void;
}) {
  const comparison = useRulesComparison(from, to, eai);

  if (comparison.isPending) return <Spinner label="Comparing the rules on the whole portfolio…" />;

  if (comparison.error) {
    if (comparison.error instanceof ApiError && comparison.error.status === 404 && eai) {
      return (
        <ErrorPanel
          title="Nothing to compare"
          detail={`There is no application ${eai} in the loaded portfolio, or one of the versions no longer exists.`}
        />
      );
    }

    return <QueryError error={comparison.error} title="The rules could not be compared" />;
  }

  const data = comparison.data;

  return (
    <ComparisonResult
      changes={data.changes}
      impact={data.impact}
      application={data.application}
      fromLabel={`Rules ${data.from.id}`}
      toLabel={`Rules ${data.to.id}`}
      selected={eai}
      onPickApplication={onPickApplication}
    />
  );
}

// ------------------------------------------------------------- borrador

function DraftComparison({
  view,
  eai,
  onPickApplication,
}: {
  view: RulesAdminView;
  eai: string;
  onPickApplication: (eai: string) => void;
}) {
  const editor = useMemo(() => loadEditor(), []);
  const state = useMemo(() => (editor ? draftFrom(editor, view.document) : null), [editor, view.document]);
  const simulate = useSimulateRules();
  const [ranFor, setRanFor] = useState<string | null>(null);

  if (!editor || !state) {
    return (
      <Card>
        <p className="px-4 py-5 text-[13px] text-ink-2">
          There is no draft in this browser tab.{' '}
          <Link to="/admin/rules" className="text-accent underline-offset-2 hover:underline">
            Prepare one in the rules editor
          </Link>
          , then come back to compare it.
        </p>
      </Card>
    );
  }

  if (editor.basedOn !== view.active.id) {
    return (
      <ErrorPanel
        title="The draft is out of date"
        detail={`It was prepared on rules ${editor.basedOn}, and rules ${view.active.id} are active now. Start it again from the rules editor.`}
      />
    );
  }

  if (state.invalid.size > 0) {
    return (
      <ErrorPanel
        title="The draft has fields without a valid number"
        detail="Fix them in the rules editor before comparing."
      />
    );
  }

  const key = `${eai}|${JSON.stringify(state.draft)}`;
  const result = simulate.data && ranFor === key ? simulate.data : null;

  const run = () =>
    simulate.mutate(
      { basedOn: editor.basedOn, draft: state.draft, eaiCode: eai || undefined },
      { onSuccess: () => setRanFor(key) },
    );

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <div className="flex flex-wrap items-center gap-3 px-4 py-4 text-[13px]">
          <p className="min-w-0 flex-1 text-ink-2">
            Simulates your draft on the whole portfolio{eai ? ` and on application ${eai}` : ''}, with the
            same engines that will score it once published. Nothing is saved.
          </p>
          <button
            type="button"
            onClick={run}
            disabled={simulate.isPending}
            className="rounded-sm bg-accent px-4 py-1.5 font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {simulate.isPending ? 'Simulating…' : result ? 'Simulate again' : 'Simulate the draft'}
          </button>
        </div>
      </Card>

      {simulate.error && <ErrorPanel title="The draft could not be simulated" detail={simulate.error.message} />}

      {result && !result.valid && (
        <ErrorPanel title="The draft is not valid" detail={result.errors.map(formatNotice).join(' ')} />
      )}

      {result?.valid && result.impact && (
        <>
          {eai && !result.application && (
            <ErrorPanel title="Application not found" detail={`There is no application ${eai} in the loaded portfolio.`} />
          )}
          <ComparisonResult
            changes={result.changes}
            impact={result.impact}
            application={result.application}
            fromLabel={`Rules ${view.active.id}`}
            toLabel="Your draft"
            selected={eai}
            onPickApplication={onPickApplication}
          />
        </>
      )}
    </div>
  );
}

// ------------------------------------------------------------- resultado

function ComparisonResult({
  changes,
  impact,
  application,
  fromLabel,
  toLabel,
  selected,
  onPickApplication,
}: {
  changes: readonly Notice[];
  impact: RulesImpact;
  application: ApplicationComparison | null;
  fromLabel: string;
  toLabel: string;
  selected: string;
  onPickApplication: (eai: string) => void;
}) {
  return (
    <div className="flex flex-col gap-4">
      {application && <ApplicationCard application={application} fromLabel={fromLabel} toLabel={toLabel} />}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.5fr)]">
        <Card
          title="What changed"
          subtitle={changes.length === 0 ? 'Both sets of rules are identical.' : countOf(changes.length, 'change')}
        >
          {changes.length > 0 && (
            <ul className="flex flex-col divide-y divide-rule">
              {changes.map((change, index) => (
                <li key={`${change.code}-${index}`} className="px-4 py-2 text-[13px]">
                  {formatNotice(change)}
                </li>
              ))}
            </ul>
          )}
        </Card>

        <ImpactCard
          impact={impact}
          fromLabel={fromLabel}
          toLabel={toLabel}
          selected={selected}
          onPickApplication={onPickApplication}
        />
      </div>
    </div>
  );
}

const orDash = (value: number | null, format: (value: number) => string = fmt) =>
  value === null ? '—' : format(value);

const signed = (value: number) => `${value > 0 ? '+' : ''}${fmt(value)}`;

function ImpactCard({
  impact,
  fromLabel,
  toLabel,
  selected,
  onPickApplication,
}: {
  impact: RulesImpact;
  fromLabel: string;
  toLabel: string;
  selected: string;
  onPickApplication: (eai: string) => void;
}) {
  return (
    <Card
      title="Portfolio impact"
      subtitle={`${countOf(impact.applications, 'application')}, scored with ${fromLabel.toLowerCase()} and with ${toLabel.toLowerCase()}`}
    >
      <div className="flex flex-col gap-4 px-4 py-4">
        <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3">
          <Stat label="Go up" value={String(impact.up)} />
          <Stat label="Go down" value={String(impact.down)} />
          <Stat label="Unchanged" value={String(impact.unchanged)} />
          <Stat label="Average score" value={`${orDash(impact.averageBefore)} → ${orDash(impact.averageAfter)}`} />
          <Stat
            label="Average when plans finish"
            value={`${orDash(impact.projectedBefore)} → ${orDash(impact.projectedAfter)}`}
          />
          <Stat label="Coherence alerts" value={`${impact.alertsBefore} → ${impact.alertsAfter}`} />
          {(impact.noLongerScored > 0 || impact.newlyScored > 0) && (
            <Stat label="Lose / gain a score" value={`${impact.noLongerScored} / ${impact.newlyScored}`} />
          )}
        </dl>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[320px] text-[13px]">
            <caption className="sr-only">Applications per score band</caption>
            <thead>
              <tr className="border-b border-rule text-left">
                <Th>Band</Th>
                <Th align="right">Before</Th>
                <Th align="right">After</Th>
              </tr>
            </thead>
            <tbody>
              {impact.bands.map((band) => (
                <tr key={band.band} className="border-b border-rule last:border-0">
                  <td className="py-1.5 pr-3">{bandLabel(band.band)}</td>
                  <td className="tnum py-1.5 text-right font-mono">{band.before}</td>
                  <td className="tnum py-1.5 text-right font-mono">{band.after}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {impact.movers.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[480px] text-[13px]">
              <caption className="pb-1 text-left">
                <Label>Applications that move the most</Label>
              </caption>
              <thead>
                <tr className="border-b border-rule text-left">
                  <Th>Application</Th>
                  <Th align="right">Before</Th>
                  <Th align="right">After</Th>
                  <Th align="right">Δ</Th>
                </tr>
              </thead>
              <tbody>
                {impact.movers.map((mover) => (
                  <tr
                    key={mover.eaiCode}
                    className={`border-b border-rule last:border-0 ${mover.eaiCode === selected ? 'bg-accent-wash' : ''}`}
                  >
                    <td className="py-1.5 pr-3">
                      <button
                        type="button"
                        onClick={() => onPickApplication(mover.eaiCode)}
                        className="text-left text-accent underline-offset-2 hover:underline"
                        title="Show this application metric by metric"
                      >
                        <span className="font-mono">{mover.eaiCode}</span> {mover.applicationName}
                      </button>
                    </td>
                    <td className="tnum py-1.5 text-right font-mono">{orDash(mover.before)}</td>
                    <td className="tnum py-1.5 text-right font-mono">{orDash(mover.after)}</td>
                    <td className="tnum py-1.5 text-right font-mono">
                      {mover.delta === null ? 'no score' : signed(mover.delta)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Card>
  );
}

function ApplicationCard({
  application,
  fromLabel,
  toLabel,
}: {
  application: ApplicationComparison;
  fromLabel: string;
  toLabel: string;
}) {
  const changed = application.metrics.filter((metric) => metric.changed).length;
  const delta = application.delta;

  return (
    <Card
      title={`${application.eaiCode} · ${application.applicationName}`}
      subtitle={
        changed === 0
          ? 'No metric changes its contribution.'
          : `${countOf(changed, 'metric')} ${changed === 1 ? 'changes' : 'change'} its contribution.`
      }
      right={
        <Link
          to={`/applications/${encodeURIComponent(application.eaiCode)}/calculation`}
          className="text-[13px] text-accent underline-offset-2 hover:underline"
        >
          Open the current calculation
        </Link>
      }
    >
      <div className="flex flex-col gap-4 px-4 py-4">
        <div className="flex flex-wrap items-center gap-x-8 gap-y-3">
          <ScoreSide label={fromLabel} score={application.scoreFrom} band={application.bandFrom} weight={application.denominatorFrom} />
          <span aria-hidden="true" className="text-[18px] text-ink-3">
            →
          </span>
          <ScoreSide label={toLabel} score={application.scoreTo} band={application.bandTo} weight={application.denominatorTo} />
          {delta !== null && (
            <span
              className="tnum font-mono text-[16px] font-semibold"
              style={{
                color: delta > 0 ? 'var(--app-good)' : delta < 0 ? 'var(--app-critical)' : 'var(--app-ink-3)',
              }}
            >
              {signed(delta)}
            </span>
          )}
        </div>

        <dl className="grid gap-3 sm:grid-cols-3">
          {application.pillars.map((pillar) => (
            <div key={pillar.key} className="rounded-sm border border-rule px-3 py-2">
              <dt className="flex items-center gap-2">
                <span aria-hidden="true" className="size-2 rounded-full" style={{ background: pillarColor(pillar.key) }} />
                <span className="text-[12.5px] text-ink-2">{pillarName(pillar.key)}</span>
              </dt>
              <dd className="tnum mt-1 font-mono text-[14px]">
                {orDash(pillar.scoreFrom)} → {orDash(pillar.scoreTo)}
              </dd>
            </div>
          ))}
        </dl>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] text-[13px]">
            <caption className="sr-only">Every metric with both sets of rules</caption>
            <thead>
              <tr className="border-b border-rule bg-surface-2 text-left">
                <Th>Metric</Th>
                <Th>Value</Th>
                <Th align="right">Weight</Th>
                <Th align="right">Score</Th>
                <Th align="right">Contribution</Th>
                <Th>Rule applied</Th>
              </tr>
            </thead>
            <tbody>
              {application.metrics.map((metric) => (
                <MetricRow key={metric.key} metric={metric} />
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </Card>
  );
}

function MetricRow({ metric }: { metric: MetricComparison }) {
  const pair = (before: string, after: string) => (before === after ? after : `${before} → ${after}`);

  const rule = (included: boolean, applied: string | null, exclusion: Notice | null) =>
    included ? (applied ?? '—') : `excluded: ${formatNotice(exclusion) || 'no data'}`;

  return (
    <tr className={`border-b border-rule last:border-0 ${metric.changed ? 'bg-accent-wash' : ''}`}>
      <td className="px-3 py-2">
        <span className="flex items-center gap-2">
          <span aria-hidden="true" className="size-2 shrink-0 rounded-full" style={{ background: pillarColor(metric.pillar) }} />
          <span className={metric.changed ? 'font-medium' : ''}>{metric.name}</span>
        </span>
      </td>
      <td className="px-3 py-2 text-ink-2">{formatNotice(metric.display)}</td>
      <td className="tnum px-3 py-2 text-right font-mono">
        {pair(fmtScore(metric.weightFrom), fmtScore(metric.weightTo))}
      </td>
      <td className="tnum px-3 py-2 text-right font-mono">
        {pair(orDash(metric.mappedFrom, fmtScore), orDash(metric.mappedTo, fmtScore))}
      </td>
      <td className="tnum px-3 py-2 text-right font-mono">
        {pair(orDash(metric.weightedFrom), orDash(metric.weightedTo))}
      </td>
      <td className="px-3 py-2 font-mono text-[12px] text-ink-2">
        {pair(
          rule(metric.includedFrom, metric.ruleFrom, metric.exclusionFrom),
          rule(metric.includedTo, metric.ruleTo, metric.exclusionTo),
        )}
      </td>
    </tr>
  );
}

function ScoreSide({
  label,
  score,
  band,
  weight,
}: {
  label: string;
  score: number | null;
  band: string | null;
  weight: number | null;
}) {
  return (
    <div className="flex flex-col gap-1">
      <Label>{label}</Label>
      {score === null || band === null ? (
        <span className="text-[13px] text-ink-3">No score: a value falls outside every band</span>
      ) : (
        <span className="flex items-center gap-2">
          <span className="tnum font-mono text-[20px] font-semibold">{fmt(score)}</span>
          <BandChip band={band} />
        </span>
      )}
      {weight !== null && <span className="tnum font-mono text-[11px] text-ink-3">weight {fmtScore(weight)} of 100</span>}
    </div>
  );
}

function Select({
  label,
  value,
  options,
  onChange,
  disabled = false,
}: {
  label: string;
  value: string;
  options: readonly { value: string; label: string }[];
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  return (
    <label className="flex flex-col gap-1.5">
      <Label>{label}</Label>
      <select
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className="min-w-44 rounded-sm border border-rule bg-surface px-2 py-1.5 text-[13px] disabled:opacity-60"
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-0.5">
      <dt>
        <Label>{label}</Label>
      </dt>
      <dd className="tnum font-mono text-[14px]">{value}</dd>
    </div>
  );
}

function Th({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'right' }) {
  return (
    <th
      scope="col"
      className={`px-3 py-2 font-mono text-[10.5px] font-semibold tracking-[0.08em] text-ink-3 uppercase ${
        align === 'right' ? 'text-right' : 'text-left'
      }`}
    >
      {children}
    </th>
  );
}
