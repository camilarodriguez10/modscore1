import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../api/client';
import { bandLabel } from '../i18n/notices';

/**
 * Piezas compartidas. El color de estado nunca viaja solo: cada chip lleva su
 * etiqueta, porque en modo claro dos de los cuatro tramos quedan bajo 3:1 contra
 * la superficie a proposito.
 */

const BAND_STYLE: Record<string, { text: string; wash: string }> = {
  // Claves: los codigos de tramo del backend. El nombre lo pone bandLabel.
  critical: { text: 'var(--app-critical)', wash: 'var(--app-critical-wash)' },
  'at-risk': { text: 'var(--app-serious)', wash: 'var(--app-serious-wash)' },
  acceptable: { text: 'var(--app-warning)', wash: 'var(--app-warning-wash)' },
  solid: { text: 'var(--app-good)', wash: 'var(--app-good-wash)' },
};

export function bandColor(band: string): string {
  return BAND_STYLE[band]?.text ?? 'var(--app-ink-3)';
}

export function bandWash(band: string): string {
  return BAND_STYLE[band]?.wash ?? 'var(--app-surface-2)';
}

/** El color de pilar es identidad, no rango: el orden es fijo. */
export function pillarColor(pillarKey: string): string {
  switch (pillarKey) {
    case 'risk':
      return 'var(--app-risk)';
    case 'architecture':
      return 'var(--app-arch)';
    case 'stability':
      return 'var(--app-stab)';
    default:
      return 'var(--app-ink-3)';
  }
}

export function pillarName(pillarKey: string): string {
  switch (pillarKey) {
    case 'risk':
      return 'Risk Vectors';
    case 'architecture':
      return 'Architecture';
    case 'stability':
      return 'Stability';
    default:
      return pillarKey;
  }
}

export function BandChip({ band, score }: { band: string; score?: number }) {
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-sm border px-2 py-0.5 text-[11px] font-semibold whitespace-nowrap"
      style={{
        color: bandColor(band),
        background: bandWash(band),
        borderColor: `color-mix(in srgb, ${bandColor(band)} 28%, transparent)`,
      }}
    >
      <span
        aria-hidden="true"
        className="size-1.5 rounded-full"
        style={{ background: bandColor(band) }}
      />
      {score !== undefined && <span className="tnum font-mono">{fmt(score)}</span>}
      {bandLabel(band)}
    </span>
  );
}

export function Card({
  title,
  subtitle,
  right,
  children,
  className = '',
}: {
  // `| undefined` explicito: con `exactOptionalPropertyTypes`, pasar un valor
  // opcional que puede ser undefined exige que el tipo lo admita.
  title?: string | undefined;
  subtitle?: string | undefined;
  right?: ReactNode | undefined;
  children: ReactNode;
  className?: string | undefined;
}) {
  return (
    <section
      className={`rounded-md border border-rule bg-surface ${className}`}
    >
      {(title || right) && (
        <header className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-b border-rule px-4 py-3">
          <div>
            {title && (
              <h2 className="font-mono text-[11px] font-semibold tracking-[0.11em] text-ink-3 uppercase">
                {title}
              </h2>
            )}
            {subtitle && <p className="mt-0.5 text-[13px] text-ink-2">{subtitle}</p>}
          </div>
          {right}
        </header>
      )}
      {children}
    </section>
  );
}

export function Label({ children }: { children: ReactNode }) {
  return (
    <span className="font-mono text-[10.5px] font-semibold tracking-[0.1em] text-ink-3 uppercase">
      {children}
    </span>
  );
}

export function Field({ label, value }: { label: string; value: ReactNode }) {
  // `??` cubre null y undefined: una fuente que omita la propiedad no puede
  // dejar el campo en blanco sin marca.
  const shown = value === null || value === undefined || value === '' ? '—' : value;

  return (
    <div className="flex flex-col gap-0.5">
      <Label>{label}</Label>
      <span className="text-[13px] text-ink">{shown}</span>
    </div>
  );
}

/** Dos decimales, con el formato numerico en ingles del sitio (ADR 0017). */
export function fmt(value: number, decimals = 2): string {
  return value.toLocaleString('en-US', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}

/**
 * Puntaje mapeado. Los valores de banda son enteros (20, 60, 100) pero los
 * compuestos no lo son: mostrar 97,5 como "98" contradiria la regla que aparece
 * al lado. Se muestran decimales solo cuando existen.
 */
export function fmtScore(value: number): string {
  return Number.isInteger(value) ? fmt(value, 0) : fmt(value, 2);
}

export function Spinner({ label }: { label: string }) {
  return (
    <div className="flex items-center gap-2 px-4 py-8 text-[13px] text-ink-3">
      <span
        aria-hidden="true"
        className="size-3 animate-spin rounded-full border-2 border-rule border-t-accent"
      />
      {label}
    </div>
  );
}

/**
 * Estado inicial del adaptador de archivos: nadie ha publicado una carga todavia.
 *
 * No es un error — es la unica condicion del sistema que tiene una salida
 * concreta — asi que la pantalla ofrece esa salida en lugar de un panel rojo que
 * deja al usuario sin saber que hacer.
 */
export function NoDataPanel({ detail }: { detail: string }) {
  return (
    <div className="rounded-md border border-rule bg-surface-2 px-4 py-6">
      <p className="text-[13px] font-semibold">The first upload is missing</p>
      <p className="mt-1 max-w-[60ch] text-[13px] text-ink-2">{detail}</p>
      <Link
        to="/data"
        className="mt-3 inline-flex items-center rounded px-3 py-1.5 text-[13px] font-medium"
        style={{ background: 'var(--app-accent)', color: 'var(--app-on-accent)' }}
      >
        Go to Data
      </Link>
    </div>
  );
}

/**
 * El error de una consulta, resuelto en una sola pieza: un 409 significa que
 * falta la primera carga y se ofrece la accion; cualquier otro estado es un
 * fallo y se muestra como tal.
 */
export function QueryError({ error, title }: { error: unknown; title: string }) {
  const api = error instanceof ApiError ? error : null;

  if (api?.status === 409) {
    return <NoDataPanel detail={api.message} />;
  }

  // Fuera de alcance no es una falla sino una decision de acceso, y se dice asi.
  if (api?.status === 403) {
    return (
      <div className="rounded-md border border-rule bg-surface-2 px-4 py-6">
        <p className="text-[13px] font-semibold">Out of scope</p>
        <p className="mt-1 max-w-[60ch] text-[13px] text-ink-2">
          Your user does not have access to this part of the portfolio.
        </p>
      </div>
    );
  }

  return (
    <ErrorPanel
      title={api?.title ?? title}
      detail={api?.message ?? 'Try again in a few seconds.'}
    />
  );
}

export function ErrorPanel({ title, detail }: { title: string; detail: string }) {
  return (
    <div
      className="rounded-md border px-4 py-3"
      style={{
        borderColor: 'color-mix(in srgb, var(--app-critical) 30%, transparent)',
        background: 'var(--app-critical-wash)',
      }}
    >
      <p className="text-[13px] font-semibold" style={{ color: 'var(--app-critical)' }}>
        {title}
      </p>
      <p className="mt-0.5 text-[13px] text-ink-2">{detail}</p>
    </div>
  );
}
