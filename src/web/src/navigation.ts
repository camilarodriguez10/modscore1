import type { SessionFeaturesView } from './api/types';

/**
 * Las pestañas del sitio y cuáles se ofrecen, sin React para poder probarlo.
 *
 * Desde el 2026-10-21 Insights ya no existe (respondía lo mismo que el dashboard) y Code y
 * Roadmap se encienden desde la capa Channel (`Features:Code`, `Features:PortfolioRoadmap`):
 * apagadas, no se ofrecen y su ruta vuelve al dashboard.
 */

export interface NavTab {
  to: string;
  label: string;
  /** Si el filtro de la barra viaja con el enlace. */
  carriesFilter: boolean;
}

/** Las pantallas donde el filtro actúa. En el resto se muestra, pero sin controles. */
export const FILTERED_ROUTES: ReadonlySet<string> = new Set(['/', '/portfolio', '/code', '/roadmap']);

/** Pantallas sobre las reglas, no sobre un recorte del portafolio: el filtro no se muestra. */
export const UNFILTERED_ROUTES: readonly string[] = ['/admin', '/definitions'];

export function visibleTabs(features: SessionFeaturesView, canAdministerRules: boolean): NavTab[] {
  return [
    { to: '/', label: 'Dashboard', carriesFilter: true },
    { to: '/portfolio', label: 'Portfolio', carriesFilter: true },
    ...(features.code ? [{ to: '/code', label: 'Code', carriesFilter: true }] : []),
    ...(features.portfolioRoadmap ? [{ to: '/roadmap', label: 'Roadmap', carriesFilter: true }] : []),
    { to: '/data', label: 'Data', carriesFilter: true },
    { to: '/definitions', label: 'Definitions', carriesFilter: false },
    // Solo a quien administra reglas. El servidor lo exige igual: esto solo evita ofrecer
    // una pantalla que respondería 403.
    ...(canAdministerRules ? [{ to: '/admin/rules', label: 'Rules', carriesFilter: false }] : []),
  ];
}

/** Las pestañas que muestra una sesión anterior al 2026-10-21, que no trae `features`. */
export const NO_FEATURES: SessionFeaturesView = { code: false, portfolioRoadmap: false };
