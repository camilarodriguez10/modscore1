import { countOf } from './notices';

/**
 * Nombres de lo que muestra la pantalla Code (ADR 0026). El backend manda claves; aqui
 * se nombran. Una clave que este catalogo no conoce se muestra tal cual.
 */

/** Los estados de ciclo de vida, del peor al mejor; "unknown" al final: no es bueno, es no saber. */
export const STATUS_KEYS = ['eol', 'approaching', 'security', 'supported', 'unknown'] as const;

const STATUSES: Record<string, { label: string; hint: string }> = {
  eol: { label: 'Past end of life', hint: 'No more security fixes.' },
  approaching: { label: 'Ending soon', hint: 'The end of life falls within the configured window.' },
  security: { label: 'Support ended', hint: 'Active support is over; security fixes continue until the end of life.' },
  supported: { label: 'Supported', hint: 'In active support.' },
  unknown: { label: 'Not assessable', hint: 'The product or version is not in the end-of-life catalog.' },
};

export const statusLabel = (status: string) => STATUSES[status]?.label ?? status;
export const statusHint = (status: string) => STATUSES[status]?.hint ?? '';

/**
 * El color de cada estado, con la paleta de estados del sitio: vencido es critico,
 * proximo es serio, sin soporte activo es advertencia. Nunca va solo: cada chip lleva
 * el nombre del estado.
 */
const STATUS_STYLE: Record<string, { mark: string; text: string; wash: string }> = {
  eol: { mark: 'var(--app-critical)', text: 'var(--app-critical-text)', wash: 'var(--app-critical-wash)' },
  approaching: { mark: 'var(--app-serious)', text: 'var(--app-serious-text)', wash: 'var(--app-serious-wash)' },
  security: { mark: 'var(--app-warning)', text: 'var(--app-warning-text)', wash: 'var(--app-warning-wash)' },
  supported: { mark: 'var(--app-good)', text: 'var(--app-good-text)', wash: 'var(--app-good-wash)' },
};

export const statusMark = (status: string) => STATUS_STYLE[status]?.mark ?? 'var(--app-ink-3)';
export const statusText = (status: string) => STATUS_STYLE[status]?.text ?? 'var(--app-ink-2)';
export const statusWash = (status: string) => STATUS_STYLE[status]?.wash ?? 'var(--app-surface-2)';

const KINDS: Record<string, string> = {
  runtime: 'Runtime',
  framework: 'Framework',
  library: 'Library',
  server: 'App server',
  os: 'OS image',
  database: 'Database',
  unknown: 'Unknown',
};

export const kindLabel = (kind: string) => KINDS[kind] ?? kind;

const FACTORS: Record<string, string> = {
  eolComponent: 'Components past end of life',
  approachingComponent: 'Components ending soon',
  criticalVulnerability: 'Critical advisories',
  highVulnerability: 'High advisories',
  deprecatedLibrary: 'Deprecated libraries',
  outdatedLibrary: 'Outdated libraries',
  staleRepository: 'Stale repositories',
};

export const factorLabel = (key: string) => FACTORS[key] ?? key;

const MISMATCHES: Record<string, { label: string; hint: string }> = {
  hiddenObsolescence: {
    label: 'Hidden obsolescence',
    hint: 'Current by its Currency Score, but the code declares components past their end of life.',
  },
  inventoryBehind: {
    label: 'Inventory behind the code',
    hint: 'Legacy by its Currency Score, but no declared component is past its end of life.',
  },
};

export const mismatchLabel = (key: string) => MISMATCHES[key]?.label ?? key;
export const mismatchHint = (key: string) => MISMATCHES[key]?.hint ?? '';

export const URGENCY_KEYS = ['now', 'year1', 'year2', 'year3', 'planned'] as const;

const URGENCIES: Record<string, string> = {
  now: 'Now',
  year1: 'Within 1 year',
  year2: 'Within 2 years',
  year3: 'Within 3 years',
  planned: 'Plan it',
};

export const urgencyLabel = (urgency: string) => URGENCIES[urgency] ?? urgency;

/** El color de la urgencia: la misma paleta de estados que el fin de vida. */
export const urgencyStatus = (urgency: string) =>
  urgency === 'now' ? 'eol' : urgency === 'year1' ? 'approaching' : urgency === 'planned' ? 'unknown' : 'security';

/** Lo que hay que hacer, en una frase corta. */
export function actionText(action: {
  kind: string;
  subject: string;
  ecosystem: string | null;
  current: string;
  targetSubject: string | null;
  target: string | null;
}): string {
  const { kind, subject, current, targetSubject, target } = action;

  switch (kind) {
    case 'upgrade':
      return `Upgrade ${subject} ${current} → ${target ?? '?'}`;
    case 'migrate':
      return `Migrate ${subject} ${current} → ${targetSubject ?? ''} ${target ?? ''}`.trim();
    case 'awaitRelease':
      return `${subject} ${current}: no newer supported release yet`;
    case 'updateLibrary':
      return target ? `Update ${subject} ${current} → ${target}` : `Update ${subject} ${current}`;
    case 'replaceLibrary':
      return `Replace ${subject} ${current} (deprecated)`;
    case 'maintainRepository':
      return current ? `Resume maintenance: no commits since ${formatDay(current)}` : 'Resume maintenance';
    case 'retireRepository':
      return 'Archived: unmap it or confirm the application no longer uses it';
    default:
      return `${kind} ${subject}`;
  }
}

/** Una fecha yyyy-mm-dd como se lee en el sitio, sin correrla por la zona horaria. */
export function formatDay(day: string | null | undefined): string {
  if (!day) return '—';
  const [year, month, date] = day.split('-').map(Number);
  if (!year || !month || !date) return day;

  return new Date(Date.UTC(year, month - 1, date)).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });
}

/** Cuanto falta, o cuanto hace, en meses completos. */
export function monthsFromNow(months: number | null | undefined): string {
  if (months === null || months === undefined) return '';
  if (months === 0) return 'this month';
  return months < 0 ? `${countOf(-months, 'month')} ago` : `in ${countOf(months, 'month')}`;
}
