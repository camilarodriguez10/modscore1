import { useState } from 'react';
import type { HistoryPointView } from '../api/types';
import { fmt } from './primitives';

/** Un momento a marcar sobre la tendencia, como la publicacion de unas reglas. */
export interface TrendMarker {
  id: string;
  /** Instante ISO 8601. */
  at: string;
  /** Texto corto sobre la linea, p. ej. "v1.4". */
  label: string;
  /** Lo que se lee debajo del grafico, p. ej. el motivo del cambio. */
  detail: string;
}

/**
 * Donde cae cada marca entre las cargas: el indice de la carga anterior mas la fraccion
 * del tramo hasta la siguiente, en proporcion al tiempo. Las cargas se dibujan a distancia
 * fija, asi que el tiempo solo cuenta dentro de cada tramo. Las marcas fuera de la serie no
 * tienen lugar en el grafico y se omiten.
 */
export function markerSlots(
  capturedAt: readonly string[],
  markers: readonly TrendMarker[],
): { marker: TrendMarker; slot: number }[] {
  const times = capturedAt.map((value) => new Date(value).getTime());
  if (times.length < 2 || times.some(Number.isNaN)) return [];

  const first = times[0]!;
  const last = times[times.length - 1]!;

  return markers.flatMap((marker) => {
    const at = new Date(marker.at).getTime();
    if (Number.isNaN(at) || at < first || at > last) return [];

    let index = 0;
    while (index < times.length - 2 && at > times[index + 1]!) index++;

    const start = times[index]!;
    const end = times[index + 1]!;
    const fraction = end > start ? (at - start) / (end - start) : 0;

    return [{ marker, slot: index + fraction }];
  });
}

const markerDate = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' });

/**
 * Serie temporal del score a lo largo de las cargas.
 *
 * La escala vertical va de 20 a 100 — la del modelo, no de 0 — porque el BRD
 * nunca produce un score menor y arrancar en cero aplastaria toda la variacion
 * en la mitad superior del grafico.
 *
 * Una sola serie, asi que no lleva caja de leyenda: el titulo de la tarjeta la
 * nombra. El peso disponible acompana cada punto porque un salto de score con
 * mas peso detras puede ser solo mas evidencia, no mejora real.
 *
 * Las marcas punteadas senalan cuando cambiaron las reglas. Todos los puntos se
 * calculan con las reglas vigentes (ADR 0018): la marca dice desde cuando rigen, no
 * que los puntos anteriores usen otras.
 */
export function ScoreTrend({
  points,
  markers = [],
  floor = 20,
  ceiling = 100,
}: {
  points: readonly HistoryPointView[];
  markers?: readonly TrendMarker[];
  floor?: number;
  ceiling?: number;
}) {
  const [hovered, setHovered] = useState<number | null>(null);

  const series = points.filter((point): point is HistoryPointView & { score: number } =>
    point.score !== null,
  );

  if (series.length < 2) {
    return (
      <p className="text-[13px] text-ink-3">
        At least two uploads are needed to draw a trend.
      </p>
    );
  }

  // Margenes con espacio para las etiquetas de los extremos: nada se sale del
  // viewBox ni se pisa con el eje.
  const width = 720;
  const height = 200;
  const left = 42;
  const right = 18;
  const top = 16;
  const bottom = 30;

  const plotWidth = width - left - right;
  const plotHeight = height - top - bottom;

  const xOf = (index: number) => left + (index / (series.length - 1)) * plotWidth;
  const yOf = (score: number) => top + plotHeight - ((score - floor) / (ceiling - floor)) * plotHeight;

  const line = series
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${xOf(index)} ${yOf(point.score)}`)
    .join(' ');

  const area = `${line} L ${xOf(series.length - 1)} ${top + plotHeight} L ${xOf(0)} ${top + plotHeight} Z`;

  const placed = markerSlots(
    series.map((point) => point.capturedAt),
    markers,
  );

  // Ticks en valores que la escala alcanza de verdad.
  const ticks = [floor, 40, 60, 80, ceiling];
  const last = series[series.length - 1]!;
  const active = hovered === null ? null : series[hovered]!;
  const shown = active ?? last;
  const markedText =
    placed.length === 0 ? '' : `, ${placed.length} rules ${placed.length === 1 ? 'change' : 'changes'} marked`;

  return (
    <div className="flex flex-col gap-3">
      <div className="overflow-x-auto">
        <svg
          viewBox={`0 0 ${width} ${height}`}
          className="h-auto w-full min-w-[520px]"
          role="img"
          aria-label={`Score trend, from ${fmt(series[0]!.score)} to ${fmt(last.score)}${markedText}`}
        >
          {ticks.map((tick) => (
            <g key={tick}>
              <line
                x1={left}
                x2={width - right}
                y1={yOf(tick)}
                y2={yOf(tick)}
                stroke="var(--app-grid)"
                strokeWidth={1}
                fill="none"
              />
              <text
                x={left - 8}
                y={yOf(tick) + 4}
                textAnchor="end"
                fontSize={10.5}
                fontFamily="var(--font-mono)"
                fill="var(--app-ink-3)"
              >
                {tick}
              </text>
            </g>
          ))}

          {placed.map(({ marker, slot }) => {
            const x = left + (slot / (series.length - 1)) * plotWidth;
            const nearRight = x > width - right - 48;

            return (
              <g key={`marker-${marker.id}`}>
                <line
                  x1={x}
                  x2={x}
                  y1={top}
                  y2={top + plotHeight}
                  stroke="var(--app-ink-3)"
                  strokeWidth={1}
                  strokeDasharray="4 3"
                  fill="none"
                />
                <text
                  x={nearRight ? x - 4 : x + 4}
                  y={top + 10}
                  textAnchor={nearRight ? 'end' : 'start'}
                  fontSize={10}
                  fontFamily="var(--font-mono)"
                  fill="var(--app-ink-2)"
                >
                  {marker.label}
                </text>
              </g>
            );
          })}

          <path d={area} fill="var(--app-accent)" opacity={0.1} stroke="none" />
          <path d={line} fill="none" stroke="var(--app-accent)" strokeWidth={2} />

          {active && (
            <line
              x1={xOf(hovered!)}
              x2={xOf(hovered!)}
              y1={top}
              y2={top + plotHeight}
              stroke="var(--app-ink-3)"
              strokeWidth={1}
              strokeDasharray="3 3"
              fill="none"
            />
          )}

          {series.map((point, index) => (
            <circle
              key={point.versionId}
              cx={xOf(index)}
              cy={yOf(point.score)}
              r={index === series.length - 1 ? 5 : 4}
              fill="var(--app-surface)"
              stroke="var(--app-accent)"
              strokeWidth={2}
            />
          ))}

          {/* Areas de contacto mas anchas que la marca, para poder apuntarlas. */}
          {series.map((point, index) => (
            <rect
              key={`hit-${point.versionId}`}
              x={xOf(index) - plotWidth / (series.length * 2)}
              y={top}
              width={plotWidth / series.length}
              height={plotHeight}
              fill="transparent"
              onMouseEnter={() => setHovered(index)}
              onMouseLeave={() => setHovered(null)}
            />
          ))}

          {series.map((point, index) => (
            <text
              key={`label-${point.versionId}`}
              x={xOf(index)}
              y={height - 10}
              textAnchor={
                index === 0 ? 'start' : index === series.length - 1 ? 'end' : 'middle'
              }
              fontSize={10.5}
              fontFamily="var(--font-mono)"
              fill="var(--app-ink-3)"
            >
              {point.label}
            </text>
          ))}
        </svg>
      </div>

      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1">
        <span className="text-[13px] text-ink-2">
          <span className="font-medium text-ink">{shown.label}</span>
          <span className="tnum ml-2 font-mono">
            score {fmt(shown.score)} · weight {fmt(shown.availableWeight ?? 0, 0)} of 100
          </span>
          {shown.deltaFromPrevious !== null && (
            <span
              className="tnum ml-2 font-mono"
              style={{
                color:
                  shown.deltaFromPrevious >= 0 ? 'var(--app-good)' : 'var(--app-critical)',
              }}
            >
              {shown.deltaFromPrevious >= 0 ? '+' : ''}
              {fmt(shown.deltaFromPrevious)} since the previous upload
            </span>
          )}
        </span>

        <span className="font-mono text-[10.5px] text-ink-3">20–100 scale</span>
      </div>

      {markers.length > 0 && (
        <ul className="flex flex-col gap-1 border-t border-rule pt-3 text-[12.5px] text-ink-2" aria-label="Rules changes">
          {markers.map((marker) => {
            const date = new Date(marker.at);

            return (
              <li key={marker.id} className="flex flex-wrap items-baseline gap-x-2">
                <span aria-hidden="true" className="font-mono text-ink-3">
                  ┆
                </span>
                <span className="font-medium text-ink">Rules {marker.label}</span>
                {!Number.isNaN(date.getTime()) && (
                  <span className="tnum font-mono text-ink-3">{markerDate.format(date)}</span>
                )}
                <span className="min-w-0">{marker.detail}</span>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
