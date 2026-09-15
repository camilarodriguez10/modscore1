import { useCallback, useEffect, useMemo, useRef } from 'react';
import { Link, useLocation, useNavigationType, useSearchParams } from 'react-router';
import { usePortfolio } from './api/hooks';
import type { PortfolioFilters } from './api/types';
import { Label } from './components/primitives';
import { bandCode, bandLabel } from './i18n/notices';

/**
 * Filtros globales.
 *
 * La URL es la fuente de verdad. No hay un estado de filtro escondido en cada
 * pantalla: `?country=Mexico&lob=Individual` viaja en cada navegacion, sobrevive a
 * un refresco y se puede pegar en un chat. La sesion recuerda el ultimo filtro solo
 * para volver a entrar donde uno lo dejo.
 *
 * Un filtro **nunca amplia** lo que se ve. El servidor recorta primero por el
 * alcance del usuario y despues aplica el filtro sobre lo que quedo, asi que no hay
 * combinacion de parametros en esta barra que muestre algo que el alcance no
 * permita (ADR 0012).
 */

export const FILTER_KEYS = [
  'country',
  'lob',
  'cio',
  'disposition',
  'roadmap',
  'band',
  'critical',
] as const;

export type FilterKey = (typeof FILTER_KEYS)[number];
export type Filters = Partial<Record<FilterKey, string>>;

/** Como se llama cada filtro en pantalla. */
export const FILTER_LABELS: Record<FilterKey, string> = {
  country: 'Country',
  lob: 'LOB',
  cio: 'CIO',
  disposition: 'Disposition',
  roadmap: 'Roadmap',
  band: 'Score band',
  critical: 'Criticality',
};

const STORAGE_KEY = 'modscore.filters';

function isFilterKey(value: string): value is FilterKey {
  return (FILTER_KEYS as readonly string[]).includes(value);
}

/** Como se lee el valor de un filtro: el tramo llega como codigo. */
export function filterValueLabel(key: FilterKey, value: string): string {
  if (key === 'critical') return 'critical only';
  if (key === 'band') return bandLabel(value);
  return value;
}

export function readFilters(params: URLSearchParams): Filters {
  const filters: Filters = {};

  for (const key of FILTER_KEYS) {
    const value = params.get(key);
    if (value) filters[key] = key === 'band' ? bandCode(value) : value;
  }

  return filters;
}

/** La query string de los filtros activos, para colgarla de un enlace. */
export function filterSearch(filters: Filters): string {
  const params = new URLSearchParams();

  for (const key of FILTER_KEYS) {
    const value = filters[key];
    if (value) params.set(key, value);
  }

  const query = params.toString();
  return query ? `?${query}` : '';
}

/** Traduce los filtros de la URL a los que entiende el API. */
export function toApiFilters(
  filters: Filters,
  page = 1,
  pageSize = 25,
): PortfolioFilters {
  return {
    country: filters.country,
    lob: filters.lob,
    cio: filters.cio,
    disposition: filters.disposition,
    roadmap: filters.roadmap,
    band: filters.band,
    criticalOnly: filters.critical === 'true' ? true : undefined,
    page,
    pageSize,
  };
}

function remember(search: string) {
  try {
    sessionStorage.setItem(STORAGE_KEY, search.replace(/^\?/, ''));
  } catch {
    // Ventana privada o almacenamiento bloqueado: el filtro simplemente no se
    // recuerda. No es un fallo del que haya que avisar.
  }
}

function recall(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

export function useFilters() {
  const [params, setParams] = useSearchParams();
  const filters = useMemo(() => readFilters(params), [params]);

  const apply = useCallback(
    (next: Filters) => {
      const updated = new URLSearchParams(params);

      for (const key of FILTER_KEYS) {
        const value = next[key];
        if (value) updated.set(key, value);
        else updated.delete(key);
      }

      // Cambiar el filtro cambia el conjunto: seguir en la pagina 7 de un
      // resultado que ahora tiene dos paginas es quedarse mirando el vacio.
      updated.delete('page');

      // La memoria se actualiza en el mismo acto, antes de navegar: si el usuario
      // quita un filtro, la siguiente navegacion no tiene nada que restituir.
      remember(filterSearch(readFilters(updated)));
      setParams(updated);
    },
    [params, setParams],
  );

  const set = useCallback(
    (key: FilterKey, value: string | undefined) => apply({ ...filters, [key]: value || undefined }),
    [apply, filters],
  );

  /** Activar el filtro que ya esta puesto lo quita: un clic, dos direcciones. */
  const toggle = useCallback(
    (key: FilterKey, value: string) => set(key, filters[key] === value ? undefined : value),
    [filters, set],
  );

  const clear = useCallback(() => apply({}), [apply]);

  const active = useMemo(
    () => FILTER_KEYS.filter((key) => filters[key]).map((key) => ({ key, value: filters[key]! })),
    [filters],
  );

  return { filters, set, toggle, clear, active, search: filterSearch(filters) };
}

/** La pagina actual, tambien en la URL: un enlace compartido abre donde estaba. */
export function usePage() {
  const [params, setParams] = useSearchParams();
  const page = Math.max(1, Number.parseInt(params.get('page') ?? '1', 10) || 1);

  const setPage = useCallback(
    (next: number) => {
      const updated = new URLSearchParams(params);
      if (next <= 1) updated.delete('page');
      else updated.set('page', String(next));
      setParams(updated);
    },
    [params, setParams],
  );

  return [page, setPage] as const;
}

/**
 * Recuerda el ultimo filtro durante la sesion y lo restituye cuando una navegacion
 * llega sin el. Va en el armazon, que es quien ve todas las navegaciones.
 *
 * - Un enlace que no lleva el filtro —la pestana de un detalle, una redireccion—
 *   no lo pierde: se restituye.
 * - Quitar un filtro desde la barra borra la memoria en el mismo acto, asi que
 *   restituir nunca resucita lo que el usuario acaba de quitar.
 * - El boton atras si lo quita: volver a una URL sin filtro es una decision del
 *   usuario, no un enlace que lo olvido.
 */
export function useFilterMemory() {
  const [params, setParams] = useSearchParams();
  const navigation = useNavigationType();
  const { pathname } = useLocation();
  const pageLoad = useRef(true);
  const hasFilters = FILTER_KEYS.some((key) => params.has(key));
  const search = filterSearch(readFilters(params));

  useEffect(() => {
    const isPageLoad = pageLoad.current;
    pageLoad.current = false;

    // La administracion no filtra: restituir el filtro ahi solo ensuciaria la URL.
    if (pathname.startsWith('/admin')) return;

    if (hasFilters) {
      // Un enlace de la Fase 10 trae el tramo por su nombre: la URL pasa al
      // codigo, para que lo que se comparta desde aqui ya salga en ingles.
      const band = params.get('band');
      if (band && bandCode(band) !== band) {
        const updated = new URLSearchParams(params);
        updated.set('band', bandCode(band));
        setParams(updated, { replace: true });
      }

      remember(search);
      return;
    }

    if (!isPageLoad && navigation === 'POP') {
      remember('');
      return;
    }

    const saved = recall();
    if (!saved) return;

    const updated = new URLSearchParams(params);
    let changed = false;

    for (const [key, value] of new URLSearchParams(saved)) {
      if (isFilterKey(key) && value) {
        updated.set(key, value);
        changed = true;
      }
    }

    if (changed) setParams(updated, { replace: true });
  }, [hasFilters, search, navigation, pathname, params, setParams]);
}

/**
 * Los valores que puede tomar cada filtro. Salen del portafolio **sin filtrar**, a
 * proposito: si las opciones se recalcularan con el filtro puesto, elegir Chile
 * dejaria la lista de paises con un solo pais y no habria como volver.
 */
export function useFilterOptions() {
  const query = usePortfolio({ page: 1, pageSize: 1 }, { staleTime: 5 * 60 * 1000 });

  return useMemo(() => {
    // La clave vacia es "sin asignar": no es un valor por el que se pueda filtrar.
    const keys = (groups: readonly { key: string }[] | undefined) =>
      [...new Set((groups ?? []).map((g) => g.key).filter(Boolean))].sort((a, b) =>
        a.localeCompare(b, 'en'),
      );

    return {
      country: keys(query.data?.byCountry),
      lob: keys(query.data?.byLob),
      cio: keys(query.data?.byCio),
      disposition: keys(query.data?.byDisposition),
      roadmap: keys(query.data?.byRoadmap),
      band: (query.data?.distribution ?? []).map((b) => b.band),
    };
  }, [query.data]);
}

// ---------------------------------------------------------------- la barra

/**
 * La barra de filtros. Va en el armazon, no en cada pantalla: el pedido era que un
 * filtro elegido en cualquier vista sobreviva al cambio de menu, y eso solo se
 * cumple si el control vive por encima de las pantallas.
 */
export function FilterBar() {
  const { filters, set, clear, active } = useFilters();
  const options = useFilterOptions();

  return (
    <section
      aria-label="Portfolio filters"
      className="flex flex-col gap-2.5 rounded-md border border-rule bg-surface px-4 py-3"
    >
      <div className="flex flex-wrap items-end gap-3">
        <Select filterKey="country" value={filters.country ?? ''} options={options.country} onChange={set} />
        <Select filterKey="lob" value={filters.lob ?? ''} options={options.lob} onChange={set} />
        <Select filterKey="cio" value={filters.cio ?? ''} options={options.cio} onChange={set} />
        <Select
          filterKey="disposition"
          value={filters.disposition ?? ''}
          options={options.disposition}
          onChange={set}
        />
        <Select filterKey="roadmap" value={filters.roadmap ?? ''} options={options.roadmap} onChange={set} />
        <Select filterKey="band" value={filters.band ?? ''} options={options.band} onChange={set} />

        <label className="flex items-center gap-2 pb-1.5 text-[13px] text-ink-2">
          <input
            type="checkbox"
            checked={filters.critical === 'true'}
            onChange={(event) => set('critical', event.target.checked ? 'true' : undefined)}
            className="size-3.5 accent-[var(--app-accent)]"
          />
          Critical only
        </label>

        {active.length > 0 && (
          <button
            type="button"
            onClick={clear}
            className="ml-auto rounded-sm border border-rule px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2"
          >
            Clear {active.length}
          </button>
        )}
      </div>

      {active.length > 0 && (
        <ul className="flex flex-wrap items-center gap-1.5 border-t border-rule pt-2.5">
          {active.map(({ key, value }) => (
            <li key={key}>
              <button
                type="button"
                onClick={() => set(key, undefined)}
                title={`Remove the ${FILTER_LABELS[key]} filter`}
                className="flex items-center gap-1.5 rounded-sm px-2 py-0.5 text-[12px] font-medium"
                style={{ background: 'var(--app-accent-wash)', color: 'var(--app-accent)' }}
              >
                <span className="font-mono text-[10px] tracking-wide uppercase opacity-70">
                  {FILTER_LABELS[key]}
                </span>
                {filterValueLabel(key, value)}
                <span aria-hidden="true">×</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * Los filtros activos, sin controles para cambiarlos. Va en las pantallas que no
 * filtran —el detalle de una aplicacion, la de datos— para que el contexto siga a
 * la vista y se pueda volver al portafolio tal como se dejo.
 */
export function ActiveFilters() {
  const { active, search } = useFilters();

  if (active.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5 rounded-md border border-rule bg-surface-2 px-3 py-2">
      <Label>Active filter</Label>

      <ul className="flex flex-wrap items-center gap-1.5">
        {active.map(({ key, value }) => (
          <li
            key={key}
            className="rounded-sm px-2 py-0.5 text-[12px] font-medium"
            style={{ background: 'var(--app-accent-wash)', color: 'var(--app-accent)' }}
          >
            <span className="font-mono text-[10px] tracking-wide uppercase opacity-70">
              {FILTER_LABELS[key]}
            </span>{' '}
            {filterValueLabel(key, value)}
          </li>
        ))}
      </ul>

      <Link
        to={{ pathname: '/', search }}
        className="ml-auto text-[12.5px] text-accent underline-offset-2 hover:underline"
      >
        View the filtered portfolio
      </Link>
    </div>
  );
}

function Select({
  filterKey,
  value,
  options,
  onChange,
}: {
  filterKey: FilterKey;
  value: string;
  options: readonly string[];
  onChange: (key: FilterKey, value: string | undefined) => void;
}) {
  return (
    <label className="flex flex-col gap-1">
      <Label>{FILTER_LABELS[filterKey]}</Label>
      <select
        value={value}
        onChange={(event) => onChange(filterKey, event.target.value || undefined)}
        className="min-w-[8rem] rounded-sm border border-rule bg-surface px-2 py-1.5 text-[13px] text-ink"
      >
        <option value="">All</option>
        {options.map((option) => (
          <option key={option} value={option}>
            {filterValueLabel(filterKey, option)}
          </option>
        ))}
      </select>
    </label>
  );
}
