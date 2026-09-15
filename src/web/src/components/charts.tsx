import { useState, type ReactNode } from 'react';
import type { ScoreBandView, WaterfallStepView } from '../api/types';
import { bandLabel, countOf, groupLabel } from '../i18n/notices';
import { BandChip, Label, bandColor, fmt, pillarColor, pillarName } from './primitives';

/**
 * Visualizaciones.
 *
 * Los tres hues de pilar son identidad y su orden es fijo. La leyenda esta
 * siempre presente y los segmentos llevan etiqueta directa cuando caben, para que
 * el color nunca sea el unico canal — la tabla de metricas debajo es el respaldo
 * para el caso en que no lo sea.
 */

// ------------------------------------------------------- score como cifra guia

/**
 * El score es un titular, no un grafico: una cifra grande con su posicion en la
 * escala. La escala arranca en 20 y no en 0, que es un detalle real del modelo:
 * el BRD nunca produce un score menor.
 */
export function ScoreHero({
  score,
  band,
  floor = 20,
  ceiling = 100,
}: {
  score: number;
  band: string;
  floor?: number;
  ceiling?: number;
}) {
  const position = ((score - floor) / (ceiling - floor)) * 100;

  return (
    <div className="flex flex-col gap-3">
      {/* El numero lleva el token de texto, no el color de estado: en modo claro
          dos de los cuatro tramos quedan bajo 3:1 y una cifra de 64px ilegible
          seria el peor lugar para gastarlo. El chip de al lado carga el color. */}
      <div className="flex items-end gap-3">
        <span className="text-[64px] leading-[0.9] font-semibold tracking-tight text-ink">
          {fmt(score)}
        </span>
        <BandChip band={band} />
      </div>

      <div>
        <div className="relative h-1.5 rounded-full bg-grid">
          <div
            className="absolute inset-y-0 left-0 rounded-full"
            style={{
              width: `${Math.max(0, Math.min(100, position))}%`,
              background: bandColor(band),
            }}
          />
        </div>
        <div className="mt-1 flex justify-between font-mono text-[10.5px] text-ink-3">
          <span className="tnum">{floor}</span>
          <span>BRD normalized scale</span>
          <span className="tnum">{ceiling}</span>
        </div>
      </div>
    </div>
  );
}

// ------------------------------------------------- composicion del numerador

interface Segment {
  key: string;
  name: string;
  pillarKey: string;
  value: number;
  points: number;
}

/**
 * Como se construye el numerador. El ancho total es el peso disponible, asi que
 * el hueco al final es exactamente lo que la aplicacion no gano: se ve el score
 * armandose y lo que le falta, en la misma imagen.
 */
export function NumeratorComposition({
  steps,
  numerator,
  denominator,
}: {
  steps: readonly WaterfallStepView[];
  numerator: number;
  denominator: number;
}) {
  const [hovered, setHovered] = useState<Segment | null>(null);

  const segments: Segment[] = steps.map((s) => ({
    key: s.metricKey,
    name: s.name,
    pillarKey: s.pillarKey,
    value: s.weightedScore,
    points: s.pointsOfFinalScore,
  }));

  const unearned = Math.max(0, denominator - numerator);
  const pillars = [...new Set(steps.map((s) => s.pillarKey))];

  return (
    <div className="flex flex-col gap-3">
      <div className="relative">
        <div className="flex h-9 gap-[2px] overflow-hidden rounded-sm">
          {segments.map((segment) => (
            <button
              key={segment.key}
              type="button"
              className="group relative min-w-[3px] cursor-default transition-opacity hover:opacity-85"
              style={{
                flexGrow: segment.value,
                flexBasis: 0,
                background: pillarColor(segment.pillarKey),
              }}
              onMouseEnter={() => setHovered(segment)}
              onMouseLeave={() => setHovered(null)}
              onFocus={() => setHovered(segment)}
              onBlur={() => setHovered(null)}
              aria-label={`${segment.name}: ${fmt(segment.value)} weighted points`}
            />
          ))}

          {unearned > 0 && (
            <div
              className="min-w-[3px] rounded-r-sm bg-grid"
              style={{ flexGrow: unearned, flexBasis: 0 }}
              aria-label={`Not earned: ${fmt(unearned)} points`}
            />
          )}
        </div>

        {hovered && (
          <div
            role="status"
            className="pointer-events-none absolute -top-1 left-0 z-10 -translate-y-full rounded-sm border border-rule bg-surface px-2.5 py-1.5 text-[12px] shadow-lg"
          >
            <span className="font-semibold text-ink">{hovered.name}</span>
            <span className="tnum ml-2 font-mono text-ink-2">
              {fmt(hovered.value)} weighted · +{fmt(hovered.points)} score points
            </span>
          </div>
        )}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
          {pillars.map((key) => (
            <span key={key} className="flex items-center gap-1.5 text-[12px] text-ink-2">
              <span
                aria-hidden="true"
                className="size-2.5 rounded-[2px]"
                style={{ background: pillarColor(key) }}
              />
              {pillarName(key)}
            </span>
          ))}
          {unearned > 0 && (
            <span className="flex items-center gap-1.5 text-[12px] text-ink-3">
              <span aria-hidden="true" className="size-2.5 rounded-[2px] bg-grid" />
              Not earned
            </span>
          )}
        </div>

        <span className="tnum font-mono text-[12px] text-ink-3">
          {fmt(numerator)} of {fmt(denominator, 0)} points available
        </span>
      </div>
    </div>
  );
}

// ------------------------------------------------------- aporte por metrica

/**
 * El detalle del grafico de arriba, fila a fila. Es tambien el respaldo de
 * accesibilidad: la misma informacion sin depender del color.
 */
export function ContributionRows({ steps }: { steps: readonly WaterfallStepView[] }) {
  const max = Math.max(...steps.map((s) => s.weightedScore), 1);

  return (
    <ol className="divide-y divide-rule">
      {steps.map((step) => (
        <li key={step.metricKey} className="grid grid-cols-[1fr_auto] items-center gap-4 px-4 py-2">
          <div className="flex min-w-0 items-center gap-2.5">
            <span
              aria-hidden="true"
              className="size-2.5 shrink-0 rounded-[2px]"
              style={{ background: pillarColor(step.pillarKey) }}
            />
            <span className="truncate text-[13px] text-ink">{step.name}</span>
          </div>

          <div className="flex items-center gap-3">
            <div className="hidden h-1.5 w-32 rounded-full bg-grid sm:block">
              <div
                className="h-full rounded-full"
                style={{
                  width: `${(step.weightedScore / max) * 100}%`,
                  background: pillarColor(step.pillarKey),
                }}
              />
            </div>
            <span className="tnum w-14 text-right font-mono text-[12.5px] text-ink">
              {fmt(step.weightedScore)}
            </span>
            <span className="tnum w-16 text-right font-mono text-[12.5px] text-ink-3">
              +{fmt(step.pointsOfFinalScore)}
            </span>
          </div>
        </li>
      ))}
    </ol>
  );
}

// --------------------------------------------------- distribucion por tramo

/**
 * Cuatro tramos ordenados de estado. El nombre del tramo va siempre visible: el
 * color de estado no puede ser el unico canal.
 */
export function BandDistribution({
  bands,
  onSelect,
  selected,
}: {
  bands: readonly ScoreBandView[];
  /** Si se pasa, cada tramo es un control: activarlo filtra el portafolio por el. */
  onSelect?: ((band: string) => void) | undefined;
  selected?: string | undefined;
}) {
  const max = Math.max(...bands.map((b) => b.count), 1);

  return (
    <ul className="flex flex-col gap-1 px-2 py-3">
      {bands.map((band) => (
        <li key={band.band}>
          <Row
            label={bandLabel(band.band)}
            description={`${bandLabel(band.band)}: ${countOf(band.count, 'application')}, ${fmt(band.percent, 1)}%`}
            active={selected === band.band}
            {...(onSelect ? { onSelect: () => onSelect(band.band) } : {})}
          >
            <span className="flex items-center gap-1.5 text-[12.5px] text-ink">
              <span
                aria-hidden="true"
                className="size-2 shrink-0 rounded-full"
                style={{ background: bandColor(band.band) }}
              />
              {bandLabel(band.band)}
            </span>

            <div className="h-2 rounded-full bg-grid">
              <div
                className="h-full rounded-full"
                style={{
                  width: `${(band.count / max) * 100}%`,
                  background: bandColor(band.band),
                }}
              />
            </div>

            <span className="tnum font-mono text-[12px] text-ink-2">
              {band.count}
              <span className="ml-1.5 text-ink-3">{fmt(band.percent, 1)}%</span>
            </span>
          </Row>
        </li>
      ))}
    </ul>
  );
}

/**
 * Una fila de agregado. Es un boton cuando filtra y un div cuando no: un control
 * que no hace nada al pulsarlo es peor que un texto, porque promete.
 */
function Row({
  children,
  label,
  description,
  active,
  onSelect,
  columns = 'grid-cols-[7.5rem_1fr_auto]',
}: {
  children: ReactNode;
  label: string;
  /**
   * Como se lee la fila con un lector de pantalla. Sin esto, el nombre del boton
   * sale de pegar sus textos y "120" con "48.0%" se lee "12048.0%".
   */
  description: string;
  active: boolean;
  onSelect?: (() => void) | undefined;
  columns?: string | undefined;
}) {
  const shape = `grid ${columns} w-full items-center gap-3 rounded-sm px-2 py-1.5 text-left`;

  if (!onSelect) {
    return <div className={shape}>{children}</div>;
  }

  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={active}
      aria-label={description}
      title={active ? `Remove the ${label} filter` : `Filter by ${label}`}
      className={`${shape} cursor-pointer hover:bg-surface-2 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[var(--app-accent)]`}
      style={active ? { background: 'var(--app-accent-wash)' } : undefined}
    >
      {children}
    </button>
  );
}

// ------------------------------------------------------- resumen por grupo

export function GroupBars({
  groups,
  emptyLabel = 'No data',
  onSelect,
  selected,
  limit = 8,
}: {
  groups: readonly { key: string; count: number; averageScore: number }[];
  emptyLabel?: string;
  /** Si se pasa, cada barra es un control: activarla filtra el portafolio por ella. */
  onSelect?: ((key: string) => void) | undefined;
  selected?: string | undefined;
  limit?: number | undefined;
}) {
  if (groups.length === 0) {
    return <p className="px-4 py-4 text-[13px] text-ink-3">{emptyLabel}</p>;
  }

  const max = Math.max(...groups.map((g) => g.averageScore), 1);
  const shown = groups.slice(0, limit);

  return (
    <div className="flex flex-col gap-1 px-2 py-3">
      <ul className="flex flex-col gap-1">
        {shown.map((group) => (
          <li key={group.key}>
            <Row
              label={groupLabel(group.key)}
              description={`${groupLabel(group.key)}: ${countOf(group.count, 'application')}, average score ${fmt(group.averageScore)}`}
              active={selected === group.key}
              columns="grid-cols-[1fr_auto]"
              // La clave vacia es "sin asignar": no hay valor por el que filtrar.
              {...(onSelect && group.key !== '' ? { onSelect: () => onSelect(group.key) } : {})}
            >
              <div className="min-w-0">
                <div className="flex items-baseline justify-between gap-2">
                  <span className="truncate text-[12.5px] text-ink">{groupLabel(group.key)}</span>
                  <span className="tnum font-mono text-[11px] text-ink-3">{countOf(group.count, 'app')}</span>
                </div>
                <div className="mt-1 h-1.5 rounded-full bg-grid">
                  <div
                    className="h-full rounded-full bg-accent"
                    style={{ width: `${(group.averageScore / max) * 100}%` }}
                  />
                </div>
              </div>
              <span className="tnum w-12 text-right font-mono text-[12.5px] text-ink">
                {fmt(group.averageScore)}
              </span>
            </Row>
          </li>
        ))}
      </ul>

      {groups.length > shown.length && (
        <p className="px-2 pt-1 font-mono text-[11px] text-ink-3">
          + {groups.length - shown.length} more, in the selector above
        </p>
      )}
    </div>
  );
}

// ---------------------------------------------------------- peso disponible

/**
 * Cuanto peso entra al calculo y cuanto queda fuera por falta de dato. Es la
 * mitad menos evidente de la formula y la que mas explica un score alto con poca
 * evidencia detras.
 */
export function WeightAvailability({
  available,
  total = 100,
}: {
  available: number;
  total?: number;
}) {
  const percent = (available / total) * 100;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex h-6 gap-[2px] overflow-hidden rounded-sm">
        <div
          className="rounded-l-sm bg-accent"
          style={{ flexGrow: available, flexBasis: 0 }}
          aria-label={`Weight with data: ${fmt(available, 0)}`}
        />
        {available < total && (
          <div
            className="rounded-r-sm bg-grid"
            style={{ flexGrow: total - available, flexBasis: 0 }}
            aria-label={`Weight without data: ${fmt(total - available, 0)}`}
          />
        )}
      </div>
      <div className="flex items-baseline justify-between">
        <Label>Weight with data</Label>
        <span className="tnum font-mono text-[12px] text-ink-2">
          {fmt(available, 0)} of {total} · {fmt(percent, 0)}%
        </span>
      </div>
    </div>
  );
}
