import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { ApiError, STEP_UP_URL } from '../api/client';
import { usePublishRules, useRules, useRulesVersion, useSimulateRules } from '../api/hooks';
import type {
  Notice,
  RulesAdminView,
  RulesImpact,
  RulesMetric,
  RulesPublishView,
  RulesSimulationView,
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
import { kpiTitle } from '../i18n/dashboard';
import {
  AREA_KEYS,
  areaName,
  areaQuestion,
  formatParameter,
  parameterHelp,
  parameterLabel,
  unitOf,
} from '../i18n/analytics';
import {
  countChanges,
  draftFrom,
  editorFrom,
  loadEditor,
  parse,
  saveEditor,
  type BandEdit,
  type Editor,
  type MetricEdit,
} from '../rules/draft';

/**
 * Pantalla 09 — administracion de reglas (Fase 12).
 *
 * Borrador, simulacion sobre todo el portafolio y publicacion. El borrador vive en el
 * navegador —en la sesion, para sobrevivir a la reautenticacion— y el servidor no
 * confia en nada de el: vuelve a validar y a simular al publicar. Lo que queda
 * registrado es el impacto que calculo el servidor, no el que se vio aqui.
 *
 * Solo se editan valores: pesos, bandas, puntajes, que palancas se ofrecen, los
 * parametros del roadmap y las definiciones de las seis preguntas (ADR 0025). Que
 * metricas existen y de donde leen su dato es codigo.
 */

type Tab = 'formula' | 'levers' | 'roadmap' | 'analytics' | 'versions';

const TABS: readonly { key: Tab; label: string }[] = [
  { key: 'formula', label: 'Score formula' },
  { key: 'levers', label: 'What-if levers' },
  { key: 'roadmap', label: 'Roadmap' },
  { key: 'analytics', label: 'Portfolio analytics' },
  { key: 'versions', label: 'Versions' },
];

const CATEGORY_LABELS: Record<string, string> = {
  modern: 'Modern',
  heritage: 'Heritage',
  obsolete: 'Obsolete',
  notApprovedForUse: 'Not approved for use',
  invest: 'Invest',
  tolerate: 'Tolerate',
  eliminate: 'Eliminate',
  migrate: 'Migrate',
};

const round2 = (value: number) => Math.round(value * 100) / 100;

const signed = (value: number) => `${value > 0 ? '+' : ''}${fmt(value)}`;

const dateTime = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' });

function formatDateTime(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : dateTime.format(date);
}

// ------------------------------------------------------------- pantalla

export function RulesPage() {
  const rules = useRules();

  // Lo publicado se recuerda aqui, fuera del area que se reinicia al cambiar de version.
  const [published, setPublished] = useState<RulesPublishView | null>(null);

  if (rules.isPending) return <Spinner label="Loading the scoring rules…" />;

  if (rules.error) {
    return <QueryError error={rules.error} title="The rules could not be loaded" />;
  }

  return (
    <RulesWorkspace
      // Una version nueva vigente empieza un borrador limpio.
      key={rules.data.active.id}
      view={rules.data}
      published={published}
      onPublished={setPublished}
    />
  );
}

function RulesWorkspace({
  view,
  published,
  onPublished,
}: {
  view: RulesAdminView;
  published: RulesPublishView | null;
  onPublished: (result: RulesPublishView) => void;
}) {
  const [params, setParams] = useSearchParams();
  const requested = params.get('tab');
  const tab: Tab = TABS.some((t) => t.key === requested) ? (requested as Tab) : 'formula';

  const baseline = useMemo(() => editorFrom(view.document, view.active.id), [view]);
  const [editor, setEditor] = useState<Editor>(() => loadEditor() ?? baseline);

  const simulate = useSimulateRules();
  const publish = usePublishRules();
  const loadVersion = useRulesVersion();
  const [simulatedKey, setSimulatedKey] = useState<string | null>(null);
  const reviewRef = useRef<HTMLDivElement>(null);

  const { draft, invalid } = useMemo(() => draftFrom(editor, view.document), [editor, view.document]);
  const baseDraft = useMemo(() => draftFrom(baseline, view.document).draft, [baseline, view.document]);
  const changes = useMemo(() => countChanges(draft, baseDraft), [draft, baseDraft]);
  const draftKey = JSON.stringify(draft);
  const staleDraft = editor.basedOn !== view.active.id;
  const simulation = simulate.data && simulatedKey === draftKey ? simulate.data : null;

  const weightByPillar = useMemo(() => {
    const totals = new Map<string, number>();
    for (const metric of draft.metrics) {
      const pillar = view.document.metrics.find((m) => m.key === metric.key)?.pillar ?? '';
      totals.set(pillar, round2((totals.get(pillar) ?? 0) + metric.weight));
    }
    return totals;
  }, [draft, view.document]);

  const totalWeight = round2([...weightByPillar.values()].reduce((sum, weight) => sum + weight, 0));

  useEffect(() => {
    saveEditor(changes > 0 || editor.restoredFrom || editor.reason ? editor : null);
  }, [editor, changes]);

  const update = (change: (next: Editor) => void) =>
    setEditor((current) => {
      const next = structuredClone(current);
      change(next);
      return next;
    });

  const selectTab = (next: Tab) => {
    const updated = new URLSearchParams(params);
    updated.set('tab', next);
    setParams(updated);
  };

  const discard = () => {
    saveEditor(null);
    setEditor(baseline);
    simulate.reset();
    publish.reset();
  };

  const runSimulation = () => {
    const key = draftKey;
    simulate.mutate(
      { basedOn: editor.basedOn, draft },
      {
        onSuccess: () => {
          setSimulatedKey(key);
          reviewRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        },
      },
    );
  };

  const runPublish = () =>
    publish.mutate(
      { basedOn: editor.basedOn, reason: editor.reason, draft },
      {
        onSuccess: (result) => {
          if (result.published) {
            saveEditor(null);
            onPublished(result);
          }
        },
      },
    );

  const restore = (id: string) =>
    loadVersion.mutate(id, {
      onSuccess: (result) => {
        setEditor({
          ...editorFrom(result.document, view.active.id, result.version.id),
          reason: `Restore the rules of ${result.version.id}`,
        });
        simulate.reset();
        selectTab('formula');
      },
    });

  const stepUpRequired = publish.error instanceof ApiError && publish.error.code === 'step-up-required';

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <Label>Scoring rules</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">
            Rules {view.active.id}
            {view.active.isBuiltIn && <span className="font-normal text-ink-3"> · built in</span>}
          </h1>
          <p className="mt-0.5 max-w-[78ch] text-[13px] text-ink-2">
            {view.active.isBuiltIn
              ? 'The rules shipped with the application, which reproduce the v1.3 dashboard. '
              : `Published ${formatDateTime(view.active.publishedAt)} by ${view.active.publishedBy}: “${view.active.reason}”. `}
            Every change is published as a new version. Nothing is edited in place, and restoring a
            version publishes its content again.
          </p>
        </div>

        <div className="flex flex-wrap items-end gap-6">
          <Link
            to={changes > 0 ? '/admin/rules/compare?to=draft' : '/admin/rules/compare'}
            className="rounded-sm border border-rule bg-surface px-3 py-1.5 text-[13px] hover:bg-surface-2"
          >
            {changes > 0 ? 'Compare your draft' : 'Compare versions'}
          </Link>
          <dl className="flex gap-6">
            <div>
              <dt>
                <Label>Stored in</Label>
              </dt>
              <dd className="mt-0.5 text-[13px]">{view.store}</dd>
            </div>
            <div>
              <dt>
                <Label>Versions</Label>
              </dt>
              <dd className="tnum mt-0.5 text-[13px]">{view.versions.length}</dd>
            </div>
          </dl>
        </div>
      </header>

      {published?.version && (
        <div
          role="status"
          className="rounded-md border px-4 py-3 text-[13px]"
          style={{ borderColor: 'color-mix(in srgb, var(--app-good) 35%, transparent)', background: 'var(--app-good-wash)' }}
        >
          <p className="font-semibold">Published rules {published.version.id}</p>
          <p className="mt-0.5 text-ink-2">
            Every score in the portfolio is now calculated with them.
            {published.impact?.averageDelta !== null && published.impact?.averageDelta !== undefined && (
              <> The portfolio average moved {signed(published.impact.averageDelta)} points.</>
            )}{' '}
            <Link to="/portfolio" className="text-accent underline-offset-2 hover:underline">
              View the portfolio
            </Link>
          </p>
        </div>
      )}

      {staleDraft && (
        <div className="flex flex-wrap items-center gap-3 rounded-md border border-rule bg-surface-2 px-4 py-3 text-[13px]">
          <p>
            This draft was prepared on rules {editor.basedOn}, and rules {view.active.id} are active now. It
            cannot be published as it is.
          </p>
          <button
            type="button"
            onClick={discard}
            className="rounded-sm border border-rule bg-surface px-3 py-1.5 hover:bg-surface-2"
          >
            Start again from {view.active.id}
          </button>
        </div>
      )}

      <div ref={reviewRef} className="scroll-mt-4">
        {simulation && (
          <ReviewPanel
            simulation={simulation}
            view={view}
            editor={editor}
            staleDraft={staleDraft}
            onReason={(reason) => update((next) => { next.reason = reason; })}
            onPublish={runPublish}
            publishing={publish.isPending}
            publishResult={publish.data ?? null}
            publishError={publish.error && !stepUpRequired ? publish.error : null}
            stepUpRequired={stepUpRequired}
          />
        )}
        {simulate.error && <ErrorPanel title="The draft could not be simulated" detail={simulate.error.message} />}
      </div>

      <nav className="flex gap-1 border-b border-rule" aria-label="Rules sections">
        {TABS.map((t) => (
          <TabButton key={t.key} active={tab === t.key} onClick={() => selectTab(t.key)}>
            {t.label}
          </TabButton>
        ))}
      </nav>

      {tab === 'formula' && (
        <FormulaTab
          view={view}
          editor={editor}
          invalid={invalid}
          weightByPillar={weightByPillar}
          totalWeight={totalWeight}
          update={update}
        />
      )}
      {tab === 'levers' && <LeversTab view={view} editor={editor} invalid={invalid} update={update} />}
      {tab === 'roadmap' && <RoadmapTab view={view} editor={editor} invalid={invalid} update={update} />}
      {tab === 'analytics' && <AnalyticsTab view={view} editor={editor} invalid={invalid} update={update} />}
      {tab === 'versions' && (
        <VersionsTab view={view} onRestore={restore} restoring={loadVersion.isPending ? loadVersion.variables : null} />
      )}

      <section
        aria-label="Draft"
        className="sticky bottom-0 z-10 -mx-6 border-t border-rule bg-surface px-6 py-3"
        style={{ boxShadow: '0 -10px 24px -20px rgba(0, 0, 0, 0.45)' }}
      >
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          <p className="text-[13px]">
            <span className="font-semibold">{changes === 0 ? 'No changes' : countOf(changes, 'change')}</span>
            <span className="text-ink-3">
              {' '}
              · draft on rules {editor.basedOn}
              {editor.restoredFrom ? ` · content restored from ${editor.restoredFrom}` : ''}
            </span>
          </p>

          {totalWeight !== view.limits.totalWeight && (
            <p className="text-[12.5px]" style={{ color: 'var(--app-critical-text)' }}>
              Weights add up to {fmtScore(totalWeight)} of {fmtScore(view.limits.totalWeight)}
            </p>
          )}

          {invalid.size > 0 && (
            <p className="text-[12.5px]" style={{ color: 'var(--app-critical-text)' }}>
              {countOf(invalid.size, 'field')} without a valid number
            </p>
          )}

          <div className="ml-auto flex gap-2">
            <button
              type="button"
              onClick={discard}
              disabled={changes === 0 && !editor.restoredFrom}
              className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Discard draft
            </button>
            <button
              type="button"
              onClick={runSimulation}
              disabled={changes === 0 || invalid.size > 0 || simulate.isPending}
              className="rounded-sm bg-accent px-4 py-1.5 text-[13px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {simulate.isPending ? 'Simulating…' : simulation ? 'Simulate again' : 'Simulate impact'}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}

// ------------------------------------------------------------ revision

function ReviewPanel({
  simulation,
  view,
  editor,
  staleDraft,
  onReason,
  onPublish,
  publishing,
  publishResult,
  publishError,
  stepUpRequired,
}: {
  simulation: RulesSimulationView;
  view: RulesAdminView;
  editor: Editor;
  staleDraft: boolean;
  onReason: (reason: string) => void;
  onPublish: () => void;
  publishing: boolean;
  publishResult: RulesPublishView | null;
  publishError: Error | null;
  stepUpRequired: boolean;
}) {
  if (!simulation.valid) {
    return <NoticeList title="The draft cannot be published yet" notices={simulation.errors} critical />;
  }

  const impact = simulation.impact;
  const reasonLength = editor.reason.trim().length;
  const reasonOk = reasonLength >= view.limits.minReasonLength && reasonLength <= view.limits.maxReasonLength;
  const stale = simulation.stale || staleDraft;
  const rejected = publishResult && !publishResult.published ? publishResult.errors : [];

  return (
    <Card
      title="Impact on the portfolio"
      subtitle={
        impact
          ? `Simulated on ${countOf(impact.applications, 'application')}, starting from rules ${simulation.activeVersion}. Nothing changes until you publish.`
          : undefined
      }
    >
      <div className="flex flex-col gap-5 px-4 py-4">
        {impact && <ImpactSummary impact={impact} />}

        <NoticeList title="What changes" notices={simulation.changes} />

        {stale ? (
          <p className="text-[13px]" style={{ color: 'var(--app-critical-text)' }}>
            Rules {view.active.id} were published while you were editing. Start again from them to publish.
          </p>
        ) : (
          <div className="flex flex-col gap-2 border-t border-rule pt-4">
            <label className="flex flex-col gap-1">
              <Label>Reason for the change</Label>
              <input
                type="text"
                value={editor.reason}
                maxLength={view.limits.maxReasonLength}
                onChange={(event) => onReason(event.target.value)}
                placeholder="For example: MFA weighs more after the 2026 audit"
                className="max-w-[60ch] rounded-sm border border-rule bg-surface px-2.5 py-1.5 text-[13px] text-ink"
              />
              <span className="text-[12px] text-ink-3">
                It is saved with the version and in the audit log. Between {view.limits.minReasonLength} and{' '}
                {view.limits.maxReasonLength} characters.
              </span>
            </label>

            {!view.reauthenticated && !stepUpRequired && (
              <p className="text-[12.5px] text-ink-2">
                Publishing asks you to sign in again: a sign-in is valid for {view.reauthenticationMinutes} minutes.
              </p>
            )}

            {stepUpRequired ? (
              <div className="flex flex-wrap items-center gap-3 rounded-sm border border-rule bg-surface-2 px-3 py-2.5 text-[13px]">
                <p>Confirm your identity to publish. Your draft stays here while you sign in.</p>
                <a
                  href={STEP_UP_URL}
                  className="rounded-sm bg-accent px-3 py-1.5 font-medium text-on-accent hover:opacity-90"
                >
                  Sign in again
                </a>
              </div>
            ) : (
              <div className="flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  onClick={onPublish}
                  disabled={!reasonOk || publishing}
                  className="rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {publishing ? 'Publishing…' : `Publish as a new version`}
                </button>
                <span className="text-[12.5px] text-ink-3">
                  Every score in the portfolio will be calculated with these rules.
                </span>
              </div>
            )}

            {rejected.length > 0 && <NoticeList title="The rules were not published" notices={rejected} critical />}
            {publishError && <ErrorPanel title="The rules were not published" detail={publishError.message} />}
          </div>
        )}
      </div>
    </Card>
  );
}

function ImpactSummary({ impact }: { impact: RulesImpact }) {
  return (
    <div className="flex flex-col gap-4">
      <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3 lg:grid-cols-6">
        <Stat label="Average score">
          {impact.averageBefore === null || impact.averageAfter === null
            ? '—'
            : `${fmt(impact.averageBefore)} → ${fmt(impact.averageAfter)}`}
          {impact.averageDelta !== null && (
            <span className="block text-[12px] text-ink-3">{signed(impact.averageDelta)} points</span>
          )}
        </Stat>
        <Stat label="Go up">{countOf(impact.up, 'application')}</Stat>
        <Stat label="Go down">{countOf(impact.down, 'application')}</Stat>
        <Stat label="Unchanged">{countOf(impact.unchanged, 'application')}</Stat>
        <Stat label="Projected at plan end">
          {impact.projectedBefore === null || impact.projectedAfter === null
            ? '—'
            : `${fmt(impact.projectedBefore)} → ${fmt(impact.projectedAfter)}`}
        </Stat>
        <Stat label="Roadmap alerts">
          {impact.alertsBefore} → {impact.alertsAfter}
        </Stat>
      </dl>

      {impact.indicators.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[30rem] text-left text-[13px]">
            <caption className="mb-1.5 text-left">
              <Label>Dashboard indicators, whole portfolio</Label>
            </caption>
            <thead className="text-[11px] text-ink-3 uppercase">
              <tr>
                <th className="py-1 font-medium">Indicator</th>
                <th className="py-1 text-right font-medium">Before</th>
                <th className="py-1 text-right font-medium">After</th>
              </tr>
            </thead>
            <tbody>
              {impact.indicators.map((indicator) => {
                const unit = indicator.key === 'health' ? '' : ' %';
                const show = (value: number | null) => (value === null ? '—' : `${fmt(value)}${unit}`);
                const moved = indicator.before !== indicator.after || indicator.countBefore !== indicator.countAfter;

                return (
                  <tr key={indicator.key} className="border-t border-rule">
                    <td className="py-1.5">{kpiTitle(indicator.key)}</td>
                    <td className="tnum py-1.5 text-right font-mono">{show(indicator.before)}</td>
                    <td className={`tnum py-1.5 text-right font-mono ${moved ? 'font-semibold' : ''}`}>
                      {show(indicator.after)}
                      {indicator.key !== 'health' && (
                        <span className="ml-1.5 text-ink-3">({countOf(indicator.countAfter, 'app')})</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {impact.noLongerScored > 0 && (
        <p className="text-[13px]" style={{ color: 'var(--app-critical-text)' }}>
          {countOf(impact.noLongerScored, 'application')} would lose their score: their data no longer falls in
          any band.
        </p>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(16rem,22rem)_1fr]">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-[13px]">
            <caption className="mb-1.5 text-left">
              <Label>Applications per score band</Label>
            </caption>
            <thead className="text-[11px] text-ink-3 uppercase">
              <tr>
                <th className="py-1 font-medium">Band</th>
                <th className="py-1 text-right font-medium">Before</th>
                <th className="py-1 text-right font-medium">After</th>
              </tr>
            </thead>
            <tbody>
              {impact.bands.map((band) => (
                <tr key={band.band} className="border-t border-rule">
                  <td className="py-1.5">{bandLabel(band.band)}</td>
                  <td className="tnum py-1.5 text-right font-mono">{band.before}</td>
                  <td className="tnum py-1.5 text-right font-mono">
                    {band.after}
                    {band.after !== band.before && (
                      <span className="ml-1.5 text-ink-3">({band.after > band.before ? '+' : ''}{band.after - band.before})</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-[34rem] text-left text-[13px]">
            <caption className="mb-1.5 text-left">
              <Label>Applications that move the most</Label>
            </caption>
            <thead className="text-[11px] text-ink-3 uppercase">
              <tr>
                <th className="py-1 font-medium">Application</th>
                <th className="py-1 font-medium">Before</th>
                <th className="py-1 font-medium">After</th>
                <th className="py-1 text-right font-medium">Change</th>
              </tr>
            </thead>
            <tbody>
              {impact.movers.length === 0 ? (
                <tr className="border-t border-rule">
                  <td colSpan={4} className="py-2 text-ink-3">
                    No application changes its score.
                  </td>
                </tr>
              ) : (
                impact.movers.map((mover) => (
                  <tr key={mover.eaiCode} className="border-t border-rule">
                    <td className="py-1.5 pr-3">
                      <Link
                        to={`/applications/${encodeURIComponent(mover.eaiCode)}/calculation`}
                        className="text-accent underline-offset-2 hover:underline"
                      >
                        {mover.applicationName}
                      </Link>
                      <span className="ml-1.5 font-mono text-[11px] text-ink-3">{mover.eaiCode}</span>
                    </td>
                    <td className="py-1.5 pr-3">
                      {mover.before === null || !mover.bandBefore ? '—' : <BandChip band={mover.bandBefore} score={mover.before} />}
                    </td>
                    <td className="py-1.5 pr-3">
                      {mover.after === null || !mover.bandAfter ? 'No score' : <BandChip band={mover.bandAfter} score={mover.after} />}
                    </td>
                    <td className="tnum py-1.5 text-right font-mono">{mover.delta === null ? '—' : signed(mover.delta)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function Stat({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt>
        <Label>{label}</Label>
      </dt>
      <dd className="tnum mt-0.5 text-[15px] font-semibold">{children}</dd>
    </div>
  );
}

function NoticeList({ title, notices, critical = false }: { title: string; notices: readonly Notice[]; critical?: boolean }) {
  if (notices.length === 0) return null;

  return (
    <div
      className={critical ? 'rounded-md border px-4 py-3' : ''}
      style={
        critical
          ? { borderColor: 'color-mix(in srgb, var(--app-critical) 30%, transparent)', background: 'var(--app-critical-wash)' }
          : undefined
      }
    >
      <p className="text-[13px] font-semibold" style={critical ? { color: 'var(--app-critical-text)' } : undefined}>
        {title}
      </p>
      <ul className="mt-1 list-disc space-y-0.5 pl-5 text-[13px] text-ink-2">
        {notices.map((notice, index) => (
          <li key={`${notice.code}-${index}`}>{formatNotice(notice)}</li>
        ))}
      </ul>
    </div>
  );
}

// -------------------------------------------------------------- formula

type Update = (change: (next: Editor) => void) => void;

function FormulaTab({
  view,
  editor,
  invalid,
  weightByPillar,
  totalWeight,
  update,
}: {
  view: RulesAdminView;
  editor: Editor;
  invalid: Set<string>;
  weightByPillar: Map<string, number>;
  totalWeight: number;
  update: Update;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2 rounded-md border border-rule bg-surface px-4 py-3">
        <p className="text-[13px]">
          <span className="font-mono">
            Score = {view.document.floor} + (numerator ÷ denominator) × {view.document.range}
          </span>
          <span className="text-ink-3">
            {' '}
            · the scale stays at 20–100: score bands, targets and roadmap thresholds all read it.
          </span>
        </p>
        <p className="tnum ml-auto text-[13px]">
          Total weight{' '}
          <span
            className="font-semibold"
            style={totalWeight === view.limits.totalWeight ? undefined : { color: 'var(--app-critical-text)' }}
          >
            {fmtScore(totalWeight)}
          </span>{' '}
          of {fmtScore(view.limits.totalWeight)}
        </p>
      </div>

      {view.document.pillars.map((pillar) => (
        <Card
          key={pillar.key}
          title={pillar.name}
          subtitle={`Weight ${fmtScore(weightByPillar.get(pillar.key) ?? 0)} · ${fmtScore(pillar.nominalWeight)} in rules ${view.active.id}`}
          right={<span aria-hidden="true" className="size-2.5 rounded-[2px]" style={{ background: pillarColor(pillar.key) }} />}
        >
          <ul className="divide-y divide-rule">
            {view.document.metrics
              .filter((metric) => metric.pillar === pillar.key)
              .map((metric) => {
                const edit = editor.metrics[metric.key];
                if (!edit) return null;

                return (
                  <MetricEditor
                    key={metric.key}
                    metric={metric}
                    edit={edit}
                    invalid={invalid}
                    maxBands={view.limits.maxBands}
                    maxLabel={view.limits.maxBandLabelLength}
                    onChange={(change) =>
                      update((next) => {
                        const target = next.metrics[metric.key];
                        if (target) change(target);
                      })
                    }
                  />
                );
              })}
          </ul>
        </Card>
      ))}
    </div>
  );
}

function MetricEditor({
  metric,
  edit,
  invalid,
  maxBands,
  maxLabel,
  onChange,
}: {
  metric: RulesMetric;
  edit: MetricEdit;
  invalid: Set<string>;
  maxBands: number;
  maxLabel: number;
  onChange: (change: (metric: MetricEdit) => void) => void;
}) {
  const informational = parse(edit.weight) === 0;

  return (
    <li className="grid gap-4 px-4 py-3.5 md:grid-cols-[minmax(12rem,15rem)_5.5rem_1fr]">
      <div className="min-w-0">
        <p className="text-[13.5px] font-medium">{metric.name}</p>
        <p className="mt-0.5 font-mono text-[11px] text-ink-3">{metric.sourceSystem}</p>
        {informational && (
          <p className="mt-1 text-[12px] text-ink-3">Weight 0: shown on the screens, not scored.</p>
        )}
        {metric.note && <p className="mt-1 text-[12px] text-ink-3">{metric.note}</p>}
      </div>

      <NumberField
        label="Weight"
        value={edit.weight}
        invalid={invalid.has(`${metric.key}.weight`)}
        onChange={(value) => onChange((m) => { m.weight = value; })}
      />

      <div className="min-w-0">
        {edit.bands && (
          <BandsEditor
            metricKey={metric.key}
            bands={edit.bands}
            invalid={invalid}
            maxBands={maxBands}
            maxLabel={maxLabel}
            onChange={onChange}
          />
        )}

        {edit.cases && (
          <ScoresEditor
            title="Score per case"
            entries={edit.cases}
            idPrefix={`${metric.key}.case.`}
            invalid={invalid}
            label={(key) => key}
            onChange={(key, value) => onChange((m) => { if (m.cases) m.cases[key] = value; })}
          />
        )}

        {edit.categories && (
          <ScoresEditor
            title="Score per product category"
            entries={edit.categories}
            idPrefix={`${metric.key}.category.`}
            invalid={invalid}
            label={(key) => CATEGORY_LABELS[key] ?? key}
            onChange={(key, value) => onChange((m) => { if (m.categories) m.categories[key] = value; })}
          />
        )}

        {!edit.bands && !edit.cases && !edit.categories && (
          <p className="pt-5 text-[12.5px] text-ink-3">Direct value on the 0–100 scale.</p>
        )}
      </div>
    </li>
  );
}

const BAND_GRID = 'grid grid-cols-[4.5rem_4.5rem_4rem_minmax(6rem,1fr)_2rem] items-center gap-2';

function BandsEditor({
  metricKey,
  bands,
  invalid,
  maxBands,
  maxLabel,
  onChange,
}: {
  metricKey: string;
  bands: BandEdit[];
  invalid: Set<string>;
  maxBands: number;
  maxLabel: number;
  onChange: (change: (metric: MetricEdit) => void) => void;
}) {
  const setBand = (index: number, field: keyof BandEdit, value: string) =>
    onChange((m) => {
      const band = m.bands?.[index];
      if (band) band[field] = value;
    });

  return (
    <fieldset className="flex flex-col gap-1.5">
      <legend className="sr-only">Bands</legend>
      <div className={`${BAND_GRID} font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase`} aria-hidden="true">
        <span>From</span>
        <span>To</span>
        <span>Score</span>
        <span>Label</span>
        <span />
      </div>

      {bands.map((band, index) => (
        <div key={index} className={BAND_GRID}>
          <NumberField hideLabel label={`Band ${index + 1} from`} value={band.min} width="w-full" invalid={invalid.has(`${metricKey}.band${index}.min`)} onChange={(value) => setBand(index, 'min', value)} />
          <NumberField hideLabel label={`Band ${index + 1} to`} value={band.max} width="w-full" invalid={invalid.has(`${metricKey}.band${index}.max`)} onChange={(value) => setBand(index, 'max', value)} />
          <NumberField hideLabel label={`Band ${index + 1} score`} value={band.score} width="w-full" invalid={invalid.has(`${metricKey}.band${index}.score`)} onChange={(value) => setBand(index, 'score', value)} />
          <input
            type="text"
            value={band.label}
            maxLength={maxLabel}
            aria-label={`Band ${index + 1} label`}
            onChange={(event) => setBand(index, 'label', event.target.value)}
            className="w-full rounded-sm border border-rule bg-surface px-2 py-1 text-[13px] text-ink"
          />
          <button
            type="button"
            onClick={() => onChange((m) => { m.bands?.splice(index, 1); })}
            disabled={bands.length === 1}
            aria-label={`Remove band ${index + 1}`}
            className="rounded-sm text-[16px] leading-none text-ink-3 hover:text-ink disabled:opacity-30"
          >
            ×
          </button>
        </div>
      ))}

      {bands.length < maxBands && (
        <button
          type="button"
          onClick={() => onChange((m) => { m.bands?.push({ min: '', max: '', score: '', label: '' }); })}
          className="self-start text-[12.5px] text-accent underline-offset-2 hover:underline"
        >
          Add a band
        </button>
      )}
    </fieldset>
  );
}

function ScoresEditor({
  title,
  entries,
  idPrefix,
  invalid,
  label,
  onChange,
}: {
  title: string;
  entries: Record<string, string>;
  idPrefix: string;
  invalid: Set<string>;
  label: (key: string) => string;
  onChange: (key: string, value: string) => void;
}) {
  return (
    <fieldset>
      <legend className="mb-1.5">
        <Label>{title}</Label>
      </legend>
      <div className="grid gap-x-6 gap-y-1.5 sm:grid-cols-2">
        {Object.entries(entries).map(([key, value]) => (
          <div key={key} className="flex items-center justify-between gap-3 text-[13px]">
            <span className="min-w-0 text-ink-2">{label(key)}</span>
            <NumberField hideLabel label={`${title}: ${label(key)}`} value={value} invalid={invalid.has(`${idPrefix}${key}`)} onChange={(next) => onChange(key, next)} />
          </div>
        ))}
      </div>
    </fieldset>
  );
}

function NumberField({
  label,
  value,
  invalid,
  onChange,
  hideLabel = false,
  width = 'w-20',
}: {
  label: string;
  value: string;
  invalid: boolean;
  onChange: (value: string) => void;
  hideLabel?: boolean;
  width?: string;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className={hideLabel ? 'sr-only' : ''}>
        <Label>{label}</Label>
      </span>
      <input
        type="text"
        inputMode="decimal"
        value={value}
        aria-invalid={invalid}
        onChange={(event) => onChange(event.target.value)}
        className={`${width} tnum rounded-sm border bg-surface px-2 py-1 font-mono text-[13px] text-ink`}
        style={{ borderColor: invalid ? 'var(--app-critical)' : 'var(--app-rule)' }}
      />
    </label>
  );
}

// ------------------------------------------------------------- palancas

function LeversTab({
  view,
  editor,
  invalid,
  update,
}: {
  view: RulesAdminView;
  editor: Editor;
  invalid: Set<string>;
  update: Update;
}) {
  const roadmapName = (key: string) => view.document.roadmap.roadmaps.find((r) => r.key === key)?.name ?? key;

  return (
    <Card
      title="What-if levers"
      subtitle="A lever that is turned off leaves the What-If Lab, the reachable ceiling and target searches. Levers that raise a metric take it to the top band of the formula, or to 100 when the formula uses the value as it comes, so editing a band moves their target too."
    >
      <div className="overflow-x-auto">
        <table className="w-full min-w-[46rem] text-left text-[13px]">
          <thead className="text-[11px] text-ink-3 uppercase">
            <tr>
              <th className="px-4 py-2 font-medium">Offered</th>
              <th className="py-2 pr-4 font-medium">Lever</th>
              <th className="py-2 pr-4 font-medium">Pillar</th>
              <th className="py-2 pr-4 font-medium">Delivered by</th>
              <th className="py-2 pr-4 font-medium">Default quantity</th>
            </tr>
          </thead>
          <tbody>
            {view.document.levers.map((lever) => {
              const edit = editor.levers[lever.key];
              if (!edit) return null;

              // Los roadmaps del borrador, no los publicados: quitarla de un roadmap
              // tiene que verse aqui en el momento.
              const deliveredBy = Object.entries(editor.paths)
                .filter(([, path]) => path.levers.includes(lever.key))
                .map(([key]) => roadmapName(key));

              return (
                <tr key={lever.key} className="border-t border-rule align-top">
                  <td className="px-4 py-2.5">
                    <input
                      type="checkbox"
                      checked={edit.enabled}
                      aria-label={`Offer ${lever.name}`}
                      onChange={(event) =>
                        update((next) => {
                          const target = next.levers[lever.key];
                          if (target) target.enabled = event.target.checked;
                        })
                      }
                      className="mt-0.5 size-3.5 accent-[var(--app-accent)]"
                    />
                  </td>
                  <td className="py-2.5 pr-4">
                    <p className={`font-medium ${edit.enabled ? '' : 'text-ink-3'}`}>{lever.name}</p>
                    <p className="mt-0.5 text-[12px] text-ink-3">{lever.action}</p>
                  </td>
                  <td className="py-2.5 pr-4 whitespace-nowrap">
                    <span className="inline-flex items-center gap-1.5">
                      <span aria-hidden="true" className="size-2 rounded-[2px]" style={{ background: pillarColor(lever.pillarKey) }} />
                      {pillarName(lever.pillarKey)}
                    </span>
                  </td>
                  <td className="py-2.5 pr-4">
                    {deliveredBy.length === 0 ? <span className="text-ink-3">No roadmap</span> : deliveredBy.join(', ')}
                    {!edit.enabled && deliveredBy.length > 0 && (
                      <p className="mt-1 text-[12px]" style={{ color: 'var(--app-critical-text)' }}>
                        Remove it from these roadmaps before turning it off.
                      </p>
                    )}
                  </td>
                  <td className="py-2.5 pr-4">
                    {lever.isParameterized ? (
                      <NumberField
                        hideLabel
                        label={`Default quantity of ${lever.name}`}
                        value={edit.defaultParameter}
                        invalid={invalid.has(`${lever.key}.parameter`)}
                        onChange={(value) =>
                          update((next) => {
                            const target = next.levers[lever.key];
                            if (target) target.defaultParameter = value;
                          })
                        }
                      />
                    ) : (
                      <span className="text-ink-3">—</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

// -------------------------------------------------------------- roadmap

function RoadmapTab({
  view,
  editor,
  invalid,
  update,
}: {
  view: RulesAdminView;
  editor: Editor;
  invalid: Set<string>;
  update: Update;
}) {
  return (
    <div className="flex flex-col gap-4">
      <Card title="Coherence thresholds" subtitle="Used by the alerts that compare each plan against its data.">
        <div className="grid gap-4 px-4 py-3.5 sm:grid-cols-2">
          <div className="flex items-start gap-3">
            <NumberField label="Healthy score" value={editor.healthyScore} invalid={invalid.has('healthyScore')} onChange={(value) => update((next) => { next.healthyScore = value; })} />
            <p className="pt-5 text-[12.5px] text-ink-3">
              Reimagine on an application at or above this score raises an alert.
            </p>
          </div>
          <div className="flex items-start gap-3">
            <NumberField label="Significant user base" width="w-28" value={editor.userBase} invalid={invalid.has('userBase')} onChange={(value) => update((next) => { next.userBase = value; })} />
            <p className="pt-5 text-[12.5px] text-ink-3">
              Retiring or consolidating an application with this many users raises an alert.
            </p>
          </div>
        </div>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        {view.document.roadmap.roadmaps.map((path) => {
          const edit = editor.paths[path.key];
          if (!edit) return null;

          const totalWeeks = edit.weeks.reduce((sum, weeks) => sum + (parse(weeks) ?? 0), 0);

          return (
            <Card
              key={path.key}
              title={path.name}
              subtitle={`${path.disposition}${path.removesFromPortfolio ? ' · leaves the portfolio' : ''} · ${countOf(totalWeeks, 'week')}`}
            >
              <div className="flex flex-col gap-4 px-4 py-3.5">
                {path.removesFromPortfolio ? (
                  <p className="text-[12.5px] text-ink-3">
                    The application leaves the portfolio, so this roadmap projects no score and delivers no levers.
                  </p>
                ) : (
                  <fieldset>
                    <legend className="mb-1.5">
                      <Label>Levers it delivers</Label>
                    </legend>
                    <div className="grid gap-x-4 gap-y-1 sm:grid-cols-2">
                      {view.document.levers.map((lever) => {
                        const offered = editor.levers[lever.key]?.enabled ?? true;
                        const checked = edit.levers.includes(lever.key);

                        return (
                          <label
                            key={lever.key}
                            className={`flex items-start gap-2 text-[13px] ${offered ? '' : 'text-ink-3'}`}
                          >
                            <input
                              type="checkbox"
                              checked={checked}
                              disabled={!offered && !checked}
                              onChange={(event) =>
                                update((next) => {
                                  const target = next.paths[path.key];
                                  if (!target) return;
                                  target.levers = event.target.checked
                                    ? [...target.levers, lever.key]
                                    : target.levers.filter((key) => key !== lever.key);
                                })
                              }
                              className="mt-0.5 size-3.5 accent-[var(--app-accent)]"
                            />
                            <span>
                              {lever.name}
                              {!offered && ' (turned off)'}
                            </span>
                          </label>
                        );
                      })}
                    </div>
                  </fieldset>
                )}

                <fieldset>
                  <legend className="mb-1.5">
                    <Label>Milestones, in order, in weeks</Label>
                  </legend>
                  <ol className="flex flex-col gap-1.5">
                    {path.milestones.map((milestone, index) => (
                      <li key={milestone.name} className="flex items-center justify-between gap-3 text-[13px]">
                        <span className="min-w-0">
                          {milestone.name}
                          <span className="text-ink-3"> · {milestone.owner}</span>
                        </span>
                        <NumberField
                          hideLabel
                          label={`${milestone.name}, weeks`}
                          width="w-16"
                          value={edit.weeks[index] ?? ''}
                          invalid={invalid.has(`${path.key}.week${index}`)}
                          onChange={(value) =>
                            update((next) => {
                              const target = next.paths[path.key];
                              if (target) target.weeks[index] = value;
                            })
                          }
                        />
                      </li>
                    ))}
                  </ol>
                </fieldset>
              </div>
            </Card>
          );
        })}
      </div>
    </div>
  );
}

// ------------------------------------------------------------- versiones

function VersionsTab({
  view,
  onRestore,
  restoring,
}: {
  view: RulesAdminView;
  onRestore: (id: string) => void;
  restoring: string | null | undefined;
}) {
  return (
    <Card
      title="Published versions"
      subtitle="Newest first. Loading a version puts its content in the draft; publishing it creates a new version, so the history is never rewritten."
    >
      <ul className="divide-y divide-rule">
        {view.versions.map((version) => (
          <li key={version.id} className="grid gap-3 px-4 py-3.5 md:grid-cols-[6.5rem_1fr_auto]">
            <div>
              <p className="font-mono text-[14px] font-semibold">{version.id}</p>
              {version.isActive && (
                <span
                  className="mt-1 inline-block rounded-sm px-1.5 py-0.5 text-[11px] font-semibold"
                  style={{ background: 'var(--app-accent-wash)', color: 'var(--app-accent)' }}
                >
                  Active
                </span>
              )}
            </div>

            <div className="min-w-0">
              <p className="text-[13px]">
                {version.isBuiltIn ? 'The rules shipped with the application: BRD weights, verified against the v1.3 dashboard.' : version.reason}
              </p>
              <p className="mt-0.5 font-mono text-[11px] text-ink-3">
                {version.isBuiltIn
                  ? 'built in'
                  : `${formatDateTime(version.publishedAt)} · ${version.publishedBy} · based on ${version.basedOn ?? '—'}`}
              </p>

              {version.changes.length > 0 && (
                <details className="mt-1.5">
                  <summary className="cursor-pointer text-[12.5px] text-accent">
                    {countOf(version.changes.length, 'change')}
                    {version.impact && version.impact.averageDelta !== null && (
                      <span className="text-ink-3">
                        {' '}
                        · average {signed(version.impact.averageDelta)} · {version.impact.up} up, {version.impact.down} down
                      </span>
                    )}
                  </summary>
                  <ul className="mt-1 list-disc space-y-0.5 pl-5 text-[12.5px] text-ink-2">
                    {version.changes.map((change, index) => (
                      <li key={`${change.code}-${index}`}>{formatNotice(change)}</li>
                    ))}
                  </ul>
                </details>
              )}
            </div>

            <div>
              {!version.isActive && (
                <button
                  type="button"
                  onClick={() => onRestore(version.id)}
                  disabled={Boolean(restoring)}
                  className="rounded-sm border border-rule px-3 py-1.5 text-[13px] whitespace-nowrap hover:bg-surface-2 disabled:opacity-50"
                >
                  {restoring === version.id ? 'Loading…' : 'Load into draft'}
                </button>
              )}
            </div>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={`-mb-px border-b-2 px-3 py-1.5 text-[13.5px] ${
        active ? 'border-[var(--app-accent)] font-medium text-accent' : 'border-transparent text-ink-2 hover:text-ink'
      }`}
    >
      {children}
    </button>
  );
}

// -------------------------------------------------- definiciones (ADR 0025)

/**
 * Las definiciones con que se responden las seis preguntas: el umbral de cada nivel,
 * cuantas filas lista cada panel, los pesos de Insights, y el estado y el dueno de cada
 * area. No mueven ningun score: la simulacion muestra como se mueven los indicadores
 * del dashboard. La lista sale del documento, asi que un parametro nuevo aparece aqui
 * sin tocar la pantalla.
 */
function AnalyticsTab({
  view,
  editor,
  invalid,
  update,
}: {
  view: RulesAdminView;
  editor: Editor;
  invalid: Set<string>;
  update: Update;
}) {
  const { analytics } = view.document;

  return (
    <div className="flex flex-col gap-4">
      <p className="max-w-[90ch] text-[13px] text-ink-2">
        The values the dashboard and the Insights screen use to answer the six questions. They change no score; the
        simulation shows how the dashboard indicators move. Mark an area as agreed when the business confirms it:
        the dashboard stops calling it provisional. The significant user base ({fmt(analytics.significantUserBase, 0)}{' '}
        users) is edited in the Roadmap tab.{' '}
        <Link to="/definitions" className="text-accent underline underline-offset-2">
          See how each value is used
        </Link>
      </p>

      <div className="grid gap-4 xl:grid-cols-2">
        {AREA_KEYS.map((area) => {
          const parameters = analytics.parameters.filter((p) => p.area === area);
          const current = analytics.areas.find((a) => a.key === area);
          const edit = editor.areas?.[area] ?? { status: current?.status ?? 'provisional', owner: current?.owner ?? '' };

          const setArea = (change: Partial<{ status: string; owner: string }>) =>
            update((next) => {
              next.areas = { ...(next.areas ?? {}), [area]: { ...edit, ...change } };
            });

          return (
            <Card key={area} title={areaName(area)} subtitle={areaQuestion(area)}>
              <div className="flex flex-col gap-4 px-4 py-3.5">
                <div className="flex flex-wrap items-end gap-4">
                  <label className="flex flex-col gap-1">
                    <Label>Status</Label>
                    <select
                      value={edit.status}
                      onChange={(event) => setArea({ status: event.target.value })}
                      className="rounded-sm border border-rule bg-surface px-2 py-1 text-[13px] text-ink"
                    >
                      <option value="provisional">Provisional</option>
                      <option value="agreed">Agreed by the business</option>
                    </select>
                  </label>
                  <label className="flex min-w-[12rem] flex-1 flex-col gap-1">
                    <Label>Owner</Label>
                    <input
                      type="text"
                      value={edit.owner}
                      maxLength={60}
                      onChange={(event) => setArea({ owner: event.target.value })}
                      className="rounded-sm border border-rule bg-surface px-2 py-1 text-[13px] text-ink"
                    />
                  </label>
                </div>

                {area === 'currency' && (
                  <p className="text-[12.5px] text-ink-2">
                    The Currency Score is the score CMT delivers (Raw_Tech_Score, Currency Model Detail Report). Its
                    source is fixed; the threshold is yours.
                  </p>
                )}
                {area === 'obsolescence' && (
                  <p className="text-[12.5px] text-ink-2">
                    The concepts and the metrics each one groups are part of the code. Only the status and the owner
                    are edited here.
                  </p>
                )}

                {parameters.length > 0 && (
                  <div className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
                    {parameters.map((parameter) => {
                      const id = `analytics.${parameter.key}`;
                      const value = editor.analytics?.[parameter.key] ?? String(parameter.value);

                      return (
                        <div key={parameter.key} className="flex flex-col gap-1">
                          <NumberField
                            label={parameterLabel(parameter.key)}
                            value={value}
                            invalid={invalid.has(id)}
                            width="w-24"
                            onChange={(next) =>
                              update((draft) => {
                                draft.analytics = { ...(draft.analytics ?? {}), [parameter.key]: next };
                              })
                            }
                          />
                          <p className="text-[11.5px] leading-snug text-ink-2">
                            {parameterHelp(parameter.key)} Range {formatParameter({ ...parameter, value: parameter.min })}
                            {' – '}
                            {formatParameter({ ...parameter, value: parameter.max })} ({unitOf(parameter.kind)}).
                            {String(parameter.value) !== value && (
                              <> Published: {formatParameter(parameter)}.</>
                            )}
                          </p>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
