import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useScenario, useTarget, useWhatIfLab } from '../api/hooks';
import type { LeverView, ScenarioView } from '../api/types';
import {
  Card,
  QueryError,
  Label,
  Spinner,
  fmt,
  pillarColor,
  pillarName,
} from '../components/primitives';
import { ApiError } from '../api/client';
import { formatNotice } from '../i18n/notices';

/**
 * Pantalla 04 — What-If Lab. Es el requisito 5 del encargo.
 *
 * Cada palanca es una accion que un equipo puede ejecutar de verdad, y su
 * ganancia la calcula el mismo motor que produce el score real: no hay una
 * segunda formula que pueda desviarse de la primera.
 */
export function WhatIfPage() {
  const { eaiCode = '' } = useParams();
  const { data, isPending, error } = useWhatIfLab(eaiCode);

  const [selected, setSelected] = useState<readonly string[]>([]);
  const scenario = useScenario(eaiCode);
  const target = useTarget(eaiCode);
  const [targetScore, setTargetScore] = useState(90);

  if (isPending) return <Spinner label="Evaluating the levers…" />;

  if (error) {
    return <QueryError error={error} title="The lab could not be loaded" />;
  }

  const { application } = data;

  const toggle = (key: string) => {
    const next = selected.includes(key)
      ? selected.filter((k) => k !== key)
      : [...selected, key];

    setSelected(next);
    target.reset();

    if (next.length === 0) {
      scenario.reset();
    } else {
      scenario.mutate(next.map((k) => ({ key: k })));
    }
  };

  const applyTargetPlan = () => {
    setSelected([]);
    scenario.reset();
    target.mutate(targetScore);
  };

  const adoptPlan = (plan: ScenarioView) => {
    const keys = plan.applied.map((a) => a.key);
    setSelected(keys);
    target.reset();
    scenario.mutate(keys.map((k) => ({ key: k })));
  };

  const active = scenario.data ?? target.data?.scenario ?? null;
  const projected = active?.projectedScore ?? data.baselineScore;
  const delta = active?.delta ?? 0;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Label>What-If Lab</Label>
          <h1 className="mt-1 text-[22px] leading-tight font-semibold">
            {application.applicationName}
          </h1>
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
            to={`/applications/${application.eaiCode}/calculation`}
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            How it was calculated
          </Link>
          <Link
            to={`/applications/${application.eaiCode}/roadmap`}
            className="rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            Future plan
          </Link>
        </nav>
      </header>

      {/* Comparacion: donde esta hoy, donde quedaria, y hasta donde puede llegar. */}
      <Card title="Baseline and projection">
        <div className="grid gap-6 px-4 py-5 sm:grid-cols-[auto_auto_1fr] sm:items-end">
          <div>
            <Label>Today</Label>
            <p className="tnum mt-1 text-[40px] leading-none font-semibold text-ink-3">
              {fmt(data.baselineScore)}
            </p>
            <p className="tnum mt-1 font-mono text-[11px] text-ink-3">
              weight {fmt(data.baselineWeight, 0)} of 100
            </p>
          </div>

          <div className="flex items-center gap-3">
            <span aria-hidden="true" className="text-[22px] text-ink-3">
              →
            </span>
            <div>
              <Label>With the scenario</Label>
              <p className="tnum mt-1 flex items-baseline gap-2 text-[40px] leading-none font-semibold text-ink">
                {fmt(projected)}
                {delta > 0 && (
                  <span
                    className="tnum text-[15px] font-semibold"
                    style={{ color: 'var(--app-good)' }}
                  >
                    +{fmt(delta)}
                  </span>
                )}
              </p>
              {active && (
                <p className="tnum mt-1 font-mono text-[11px] text-ink-3">
                  weight {fmt(active.projectedWeight, 0)} of 100
                </p>
              )}
            </div>
          </div>

          <div className="sm:justify-self-end sm:text-right">
            <Label>Reachable ceiling</Label>
            <p className="tnum mt-1 text-[20px] leading-none font-semibold text-ink-2">
              {fmt(data.ceilingScore)}
            </p>
            <p className="tnum mt-1 font-mono text-[11px] text-ink-3">
              +{fmt(data.headroom)} over the baseline
            </p>
          </div>
        </div>

        <div className="border-t border-rule px-4 py-3">
          <ProjectionBar
            baseline={data.baselineScore}
            projected={projected}
            ceiling={data.ceilingScore}
          />
        </div>
      </Card>

      {/* La nota de interaccion: por que el conjunto no es la suma de las partes. */}
      {active?.interactionNote && (
        <div
          className="rounded-md border px-4 py-3 text-[13px] text-ink-2"
          style={{
            borderColor: 'color-mix(in srgb, var(--app-warning) 34%, transparent)',
            background: 'var(--app-warning-wash)',
          }}
        >
          {formatNotice(active.interactionNote)}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[1.5fr_1fr]">
        <Card
          title="Available levers"
          subtitle="The gain is how much the score rises if only that lever moves."
          right={
            selected.length > 0 ? (
              <button
                type="button"
                onClick={() => {
                  setSelected([]);
                  scenario.reset();
                  target.reset();
                }}
                className="rounded-sm border border-rule px-2.5 py-1 text-[12px] text-ink-2 hover:bg-surface-2"
              >
                Clear selection
              </button>
            ) : undefined
          }
        >
          <ul className="divide-y divide-rule">
            {data.actionable.map((lever) => (
              <LeverRow
                key={lever.key}
                lever={lever}
                checked={selected.includes(lever.key)}
                maxDelta={data.actionable[0]?.delta ?? 1}
                onToggle={() => toggle(lever.key)}
              />
            ))}
            {data.actionable.length === 0 && (
              <li className="px-4 py-5 text-[13px] text-ink-2">
                There are no levers left to move in this application.
              </li>
            )}
          </ul>
        </Card>

        <div className="flex flex-col gap-4">
          {/* Objetivo inverso: el usuario pone la meta, el motor devuelve el plan. */}
          <Card title="Reach a target score">
            <div className="flex flex-col gap-3 px-4 py-4">
              <label className="flex flex-col gap-1.5">
                <Label>Target score</Label>
                <div className="flex items-center gap-3">
                  <input
                    type="range"
                    min={20}
                    max={100}
                    step={1}
                    value={targetScore}
                    onChange={(event) => setTargetScore(Number(event.target.value))}
                    className="flex-1 accent-[var(--app-accent)]"
                  />
                  <span className="tnum w-10 text-right font-mono text-[15px] font-semibold text-ink">
                    {targetScore}
                  </span>
                </div>
              </label>

              <button
                type="button"
                onClick={applyTargetPlan}
                disabled={target.isPending}
                className="rounded-sm bg-accent px-3 py-2 text-[13px] font-medium text-on-accent hover:opacity-90 disabled:opacity-60"
              >
                {target.isPending ? 'Searching…' : 'Find the minimum plan'}
              </button>

              {target.data && (
                <div className="flex flex-col gap-3 border-t border-rule pt-3">
                  <p className="text-[13px] text-ink-2">{formatNotice(target.data.explanation)}</p>

                  {target.data.scenario && (
                    <>
                      <ol className="flex flex-col gap-1.5">
                        {target.data.scenario.applied.map((step, index) => (
                          <li key={step.key} className="flex gap-2.5 text-[13px]">
                            <span className="tnum font-mono text-ink-3">{index + 1}.</span>
                            <span className="text-ink">{step.name}</span>
                          </li>
                        ))}
                      </ol>
                      <button
                        type="button"
                        onClick={() => adoptPlan(target.data!.scenario!)}
                        className="self-start rounded-sm border border-rule px-3 py-1.5 text-[12.5px] text-ink-2 hover:bg-surface-2"
                      >
                        Load this plan into the levers
                      </button>
                    </>
                  )}
                </div>
              )}

              {target.error && (
                <p className="text-[13px]" style={{ color: 'var(--app-critical)' }}>
                  {target.error instanceof ApiError
                    ? target.error.message
                    : 'The plan could not be calculated.'}
                </p>
              )}
            </div>
          </Card>

          {data.notActionable.length > 0 && (
            <Card
              title="Not simulable"
              subtitle="Because of how the data arrives, not because it is already achieved."
            >
              <ul className="divide-y divide-rule">
                {data.notActionable.map((lever) => (
                  <li key={lever.key} className="px-4 py-2.5">
                    <p className="text-[13px] font-medium text-ink">{lever.name}</p>
                    <p className="mt-0.5 text-[12.5px] text-ink-2">{formatNotice(lever.notActionableReason)}</p>
                  </li>
                ))}
              </ul>
            </Card>
          )}

          {data.alreadyAchieved.length > 0 && (
            <Card title="Already achieved">
              <ul className="flex flex-wrap gap-x-4 gap-y-1.5 px-4 py-4">
                {data.alreadyAchieved.map((lever) => (
                  <li key={lever.key} className="flex items-center gap-1.5 text-[12.5px] text-ink-2">
                    <span aria-hidden="true" style={{ color: 'var(--app-good)' }}>
                      ✓
                    </span>
                    {lever.name}
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </div>
      </div>

      {active && active.applied.length > 1 && (
        <Card
          title="Scenario breakdown"
          subtitle="The individual effect of each lever, measured separately."
        >
          <div className="overflow-x-auto">
            <table className="w-full min-w-[600px] text-[13px]">
              <thead>
                <tr className="border-b border-rule bg-surface-2">
                  <th
                    scope="col"
                    className="px-4 py-2.5 text-left font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase"
                  >
                    Lever
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-2.5 text-left font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase"
                  >
                    Action
                  </th>
                  <th
                    scope="col"
                    className="px-4 py-2.5 text-right font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase"
                  >
                    Alone
                  </th>
                </tr>
              </thead>
              <tbody>
                {active.individualEffects.map((effect) => (
                  <tr key={effect.key} className="border-b border-rule last:border-0">
                    <td className="px-4 py-2.5">
                      <span className="flex items-center gap-2">
                        <span
                          aria-hidden="true"
                          className="size-2 shrink-0 rounded-[2px]"
                          style={{ background: pillarColor(effect.pillarKey) }}
                        />
                        <span className="font-medium text-ink">{effect.name}</span>
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-ink-2">{effect.action}</td>
                    <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                      +{fmt(effect.delta)}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="bg-accent-wash">
                  <td className="px-4 py-2.5 font-semibold text-ink" colSpan={2}>
                    Sum of the parts {fmt(active.sumOfIndividualDeltas)} · combined
                  </td>
                  <td className="tnum px-4 py-2.5 text-right font-mono font-semibold text-ink">
                    +{fmt(active.delta)}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </Card>
      )}
    </div>
  );
}

/** Una palanca: su ganancia, la accion real que representa y su peso aportado. */
function LeverRow({
  lever,
  checked,
  maxDelta,
  onToggle,
}: {
  lever: LeverView;
  checked: boolean;
  maxDelta: number;
  onToggle: () => void;
}) {
  return (
    <li>
      <label className="grid cursor-pointer grid-cols-[auto_1fr_auto] items-start gap-3 px-4 py-3 hover:bg-surface-2">
        <input
          type="checkbox"
          checked={checked}
          onChange={onToggle}
          className="mt-0.5 size-3.5 accent-[var(--app-accent)]"
        />

        <div className="min-w-0">
          <span className="flex items-center gap-2">
            <span
              aria-hidden="true"
              className="size-2 shrink-0 rounded-[2px]"
              style={{ background: pillarColor(lever.pillarKey) }}
            />
            <span className="text-[13.5px] font-medium text-ink">{lever.name}</span>
          </span>
          <p className="mt-0.5 text-[12.5px] text-ink-2">{lever.action}</p>
          <p className="mt-0.5 font-mono text-[11px] text-ink-3">
            {pillarName(lever.pillarKey)}
            {lever.weightAdded > 0 && ` · adds ${fmt(lever.weightAdded, 0)} of weight to the calculation`}
          </p>
        </div>

        <div className="flex flex-col items-end gap-1">
          <span className="tnum font-mono text-[14px] font-semibold text-ink">
            +{fmt(lever.delta)}
          </span>
          <div className="h-1.5 w-20 rounded-full bg-grid">
            <div
              className="h-full rounded-full"
              style={{
                width: `${(lever.delta / maxDelta) * 100}%`,
                background: pillarColor(lever.pillarKey),
              }}
            />
          </div>
        </div>
      </label>
    </li>
  );
}

/**
 * Donde esta el score hoy, donde quedaria y hasta donde puede llegar, sobre la
 * escala real del modelo — que arranca en 20, no en 0.
 */
function ProjectionBar({
  baseline,
  projected,
  ceiling,
}: {
  baseline: number;
  projected: number;
  ceiling: number;
}) {
  const position = (value: number) => ((value - 20) / 80) * 100;

  return (
    <div>
      <div className="relative h-2.5 rounded-full bg-grid">
        <div
          className="absolute inset-y-0 left-0 rounded-full bg-ink-3 opacity-40"
          style={{ width: `${position(ceiling)}%` }}
        />
        <div
          className="absolute inset-y-0 left-0 rounded-full bg-ink-3"
          style={{ width: `${position(baseline)}%` }}
        />
        {projected > baseline && (
          <div
            className="absolute inset-y-0 rounded-full"
            style={{
              left: `${position(baseline)}%`,
              width: `${position(projected) - position(baseline)}%`,
              background: 'var(--app-good)',
            }}
          />
        )}
      </div>

      <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 font-mono text-[10.5px] text-ink-3">
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="size-2 rounded-[2px] bg-ink-3" />
          today {fmt(baseline)}
        </span>
        {projected > baseline && (
          <span className="flex items-center gap-1.5">
            <span
              aria-hidden="true"
              className="size-2 rounded-[2px]"
              style={{ background: 'var(--app-good)' }}
            />
            scenario {fmt(projected)}
          </span>
        )}
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="size-2 rounded-[2px] bg-ink-3 opacity-40" />
          ceiling {fmt(ceiling)}
        </span>
        <span className="ml-auto">20–100 scale</span>
      </div>
    </div>
  );
}
