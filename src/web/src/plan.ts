import type { ActionPlanView, ActionStepView, LeverSelection } from './api/types';

/**
 * El future plan del lado del navegador, sin React para poder probarlo: cómo se lleva al
 * what-if y cómo se nombra a quien ejecuta cada paso.
 *
 * El plan llega calculado de la capa System. Abrirlo en el what-if manda exactamente sus
 * palancas y cantidades: el escenario da el mismo score que el plan (2026-10-21).
 */

/** Tope de palancas que se aceptan desde un enlace: las del catálogo son menos de veinte. */
const MAX_LEVERS = 20;

/** Tope de la cantidad de una palanca de productos, igual que el de la búsqueda del plan. */
const MAX_PARAMETER = 100_000;

const LEVER_KEY = /^[a-zA-Z]+(\.[a-zA-Z]+)*$/;

/**
 * El parámetro `target` de un enlace entre el What-If Lab y el future plan. Llega en la URL:
 * solo vale un número de 20 a 100 con hasta dos decimales; cualquier otra cosa es null y la
 * pantalla usa el objetivo del catálogo.
 */
export function parseTargetQuery(value: string | null): number | null {
  if (!value || !/^\d{1,3}(\.\d{1,2})?$/.test(value)) return null;

  const target = Number(value);
  return target >= 20 && target <= 100 ? target : null;
}

/** El future plan de una aplicación; con un objetivo, el plan hacia ese objetivo. */
export function futurePlanPath(eaiCode: string, target?: number | null): string {
  const path = `/applications/${encodeURIComponent(eaiCode)}/roadmap`;
  return target === null || target === undefined ? path : `${path}?target=${target}`;
}

/** El parámetro `plan` del what-if: `sca.remediateCritical,archValue.modernize:37`. */
export function planToQuery(steps: readonly Pick<ActionStepView, 'leverKey' | 'parameter'>[]): string {
  return steps.map((step) => (step.parameter ? `${step.leverKey}:${step.parameter}` : step.leverKey)).join(',');
}

/**
 * Lee el parámetro `plan` de un enlace. Llega en la URL, así que se valida por forma: una
 * clave que no es de palanca, una cantidad que no es un entero positivo o una lista larga
 * se descartan; que la palanca exista lo comprueba la capa System.
 */
export function parsePlanQuery(value: string | null): LeverSelection[] {
  if (!value) return [];

  const levers: LeverSelection[] = [];
  const seen = new Set<string>();

  for (const part of value.split(',').slice(0, MAX_LEVERS)) {
    const [key = '', rawParameter, ...rest] = part.trim().split(':');
    if (rest.length > 0 || !LEVER_KEY.test(key) || seen.has(key)) continue;

    if (rawParameter === undefined) {
      levers.push({ key });
    } else {
      const parameter = Number(rawParameter);
      if (!Number.isInteger(parameter) || parameter < 1 || parameter > MAX_PARAMETER) continue;
      levers.push({ key, parameter });
    }

    seen.add(key);
  }

  return levers;
}

const OWNERS: Record<string, string> = {
  security: 'Security',
  infrastructure: 'Infrastructure',
  development: 'Development team',
  architecture: 'Enterprise architecture',
  operations: 'Operations',
};

/** Quién ejecuta un paso, en palabras de quien lo lee. */
export function ownerLabel(owner: string): string {
  return OWNERS[owner] ?? owner;
}

/** La frase que resume el plan. */
export function planSummary(plan: ActionPlanView): string {
  const target = Number.isInteger(plan.target) ? String(plan.target) : formatScore(plan.target);
  const from = formatScore(plan.baselineScore);
  const to = formatScore(plan.projectedScore);

  if (plan.isAlreadyMet) {
    return `The score of ${from} already reaches the target of ${target}: there is nothing to plan.`;
  }

  const steps = plan.steps.length === 1 ? 'This step takes' : `These ${plan.steps.length} steps take`;

  return plan.isReachable
    ? `${steps} the score from ${from} to ${to}, reaching the target of ${target}. It is the same plan the What-If Lab finds for ${target}.`
    : `The target of ${target} cannot be reached with the levers available. ${steps} the score from ${from} to ${to}, as close as it gets (the ceiling with every lever at its maximum is ${formatScore(plan.ceilingScore)}).`;
}

const scoreFormat = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

function formatScore(value: number): string {
  return scoreFormat.format(value);
}
