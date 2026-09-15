import type { RulesDocument, RulesDraft } from '../api/types';

/**
 * El borrador de reglas en el navegador: estado editable, borrador que se envia y
 * conteo de cambios. Es logica pura —sin React— para que se pueda probar sola.
 *
 * El servidor no confia en nada de esto: vuelve a validar y a simular al publicar.
 * Lo que decide aqui es solo que se muestra mientras se edita.
 */

export const DRAFT_KEY = 'modscore.rulesDraft';

/** Los numeros se editan como texto: un campo a medio escribir no es un numero todavia. */
export interface BandEdit {
  min: string;
  max: string;
  score: string;
  label: string;
}

export interface MetricEdit {
  weight: string;
  bands: BandEdit[] | null;
  cases: Record<string, string> | null;
  categories: Record<string, string> | null;
}

export interface LeverEdit {
  enabled: boolean;
  defaultParameter: string;
}

export interface PathEdit {
  levers: string[];
  weeks: string[];
}

export interface Editor {
  /** La version sobre la que se preparo. Publicar exige que siga siendo la vigente. */
  basedOn: string;
  /** Cuando el contenido viene de restaurar una version anterior. */
  restoredFrom: string | null;
  metrics: Record<string, MetricEdit>;
  levers: Record<string, LeverEdit>;
  healthyScore: string;
  userBase: string;
  paths: Record<string, PathEdit>;
  reason: string;
}

export const text = (value: number) => String(value);

export function mapValues<T, U>(
  record: Readonly<Record<string, T>>,
  map: (value: T, key: string) => U,
): Record<string, U> {
  return Object.fromEntries(Object.entries(record).map(([key, value]) => [key, map(value, key)]));
}

export function editorFrom(document: RulesDocument, basedOn: string, restoredFrom: string | null = null): Editor {
  return {
    basedOn,
    restoredFrom,
    metrics: Object.fromEntries(
      document.metrics.map((metric) => [
        metric.key,
        {
          weight: text(metric.weight),
          bands: metric.bands
            ? metric.bands.map((b) => ({ min: text(b.min), max: text(b.max), score: text(b.score), label: b.label }))
            : null,
          cases: metric.cases ? mapValues(metric.cases, text) : null,
          categories: metric.categoryScores ? mapValues(metric.categoryScores, text) : null,
        },
      ]),
    ),
    levers: Object.fromEntries(
      document.levers.map((lever) => [
        lever.key,
        { enabled: lever.enabled, defaultParameter: lever.defaultParameter === null ? '' : text(lever.defaultParameter) },
      ]),
    ),
    healthyScore: text(document.roadmap.healthyScore),
    userBase: text(document.roadmap.significantUserBase),
    paths: Object.fromEntries(
      document.roadmap.roadmaps.map((path) => [
        path.key,
        { levers: [...path.levers], weeks: path.milestones.map((m) => text(m.weeks)) },
      ]),
    ),
    reason: '',
  };
}

/** Un numero finito, o null si el texto no lo es todavia. */
export function parse(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') return null;
  const number = Number(trimmed);
  return Number.isFinite(number) ? number : null;
}

/**
 * El borrador que se envia y los campos que no son un numero. La estructura sale del
 * documento vigente, no del estado: un estado guardado de otra version no puede
 * inventar metricas.
 */
export function draftFrom(editor: Editor, document: RulesDocument): { draft: RulesDraft; invalid: Set<string> } {
  const invalid = new Set<string>();

  const number = (id: string, value: string | undefined) => {
    const parsed = parse(value ?? '');
    if (parsed === null) invalid.add(id);
    return parsed ?? 0;
  };

  const whole = (id: string, value: string | undefined) => {
    const parsed = parse(value ?? '');
    if (parsed === null || !Number.isInteger(parsed)) invalid.add(id);
    return parsed ?? 0;
  };

  const draft: RulesDraft = {
    metrics: document.metrics.map((metric) => {
      const edit = editor.metrics[metric.key];

      return {
        key: metric.key,
        weight: number(`${metric.key}.weight`, edit?.weight),
        bands: edit?.bands
          ? edit.bands.map((band, i) => ({
              min: number(`${metric.key}.band${i}.min`, band.min),
              max: number(`${metric.key}.band${i}.max`, band.max),
              score: number(`${metric.key}.band${i}.score`, band.score),
              label: band.label,
            }))
          : null,
        cases: edit?.cases ? mapValues(edit.cases, (value, key) => number(`${metric.key}.case.${key}`, value)) : null,
        categoryScores: edit?.categories
          ? mapValues(edit.categories, (value, key) => number(`${metric.key}.category.${key}`, value))
          : null,
      };
    }),
    levers: document.levers.map((lever) => {
      const edit = editor.levers[lever.key];

      return {
        key: lever.key,
        enabled: edit?.enabled ?? lever.enabled,
        defaultParameter: lever.isParameterized ? whole(`${lever.key}.parameter`, edit?.defaultParameter) : null,
      };
    }),
    roadmap: {
      healthyScore: number('healthyScore', editor.healthyScore),
      significantUserBase: whole('userBase', editor.userBase),
      roadmaps: document.roadmap.roadmaps.map((path) => {
        const edit = editor.paths[path.key];

        return {
          key: path.key,
          levers: edit?.levers ?? [...path.levers],
          milestoneWeeks: path.milestones.map((_, i) => whole(`${path.key}.week${i}`, edit?.weeks[i])),
        };
      }),
    },
  };

  return { draft, invalid };
}

/** Cada valor editable del borrador, con una clave estable: contar cambios es comparar esto. */
export function flatten(draft: RulesDraft): Map<string, string> {
  const values = new Map<string, string>();

  for (const metric of draft.metrics) {
    values.set(`${metric.key}.weight`, text(metric.weight));
    if (metric.bands) values.set(`${metric.key}.bands`, JSON.stringify(metric.bands));
    for (const [key, value] of Object.entries(metric.cases ?? {})) values.set(`${metric.key}.case.${key}`, text(value));
    for (const [key, value] of Object.entries(metric.categoryScores ?? {})) {
      values.set(`${metric.key}.category.${key}`, text(value));
    }
  }

  for (const lever of draft.levers) {
    values.set(`${lever.key}.enabled`, String(lever.enabled));
    values.set(`${lever.key}.parameter`, String(lever.defaultParameter));
  }

  values.set('healthyScore', text(draft.roadmap.healthyScore));
  values.set('userBase', text(draft.roadmap.significantUserBase));

  for (const path of draft.roadmap.roadmaps) {
    values.set(`${path.key}.levers`, [...path.levers].sort().join(','));
    path.milestoneWeeks.forEach((weeks, i) => values.set(`${path.key}.week${i}`, text(weeks)));
  }

  return values;
}

/** Cuantos valores del borrador difieren de la base. El orden de las palancas de un roadmap no cuenta. */
export function countChanges(draft: RulesDraft, base: RulesDraft): number {
  const before = flatten(base);
  let changes = 0;

  for (const [key, value] of flatten(draft)) {
    if (before.get(key) !== value) changes++;
  }

  return changes;
}

/** Storage minimo que usa el borrador; en pruebas se reemplaza. */
export type DraftStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;

function sessionStore(): DraftStorage | null {
  try {
    return typeof sessionStorage === 'undefined' ? null : sessionStorage;
  } catch {
    return null;
  }
}

export function loadEditor(storage: DraftStorage | null = sessionStore()): Editor | null {
  try {
    const raw = storage?.getItem(DRAFT_KEY);
    return raw ? (JSON.parse(raw) as Editor) : null;
  } catch {
    return null;
  }
}

export function saveEditor(editor: Editor | null, storage: DraftStorage | null = sessionStore()) {
  try {
    if (editor) storage?.setItem(DRAFT_KEY, JSON.stringify(editor));
    else storage?.removeItem(DRAFT_KEY);
  } catch {
    // Almacenamiento bloqueado: el borrador no sobrevive a recargar, nada mas.
  }
}
