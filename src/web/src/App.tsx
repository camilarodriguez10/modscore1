import { Suspense, lazy } from 'react';
import { NavLink, Navigate, Outlet, Route, Routes, useLocation, useParams } from 'react-router';
import { useLogin, useSession } from './api/hooks';
import { ActiveFilters, FilterBar, useFilterMemory, useFilters } from './filters';
import { PortfolioPage } from './routes/PortfolioPage';
import { OverviewPage } from './routes/OverviewPage';
import { ExplainPage } from './routes/ExplainPage';
import { WhatIfPage } from './routes/WhatIfPage';
import { RoadmapPage } from './routes/RoadmapPage';
import { PortfolioRoadmapPage } from './routes/PortfolioRoadmapPage';
import { InsightsPage } from './routes/InsightsPage';
import { CodePage } from './routes/CodePage';
import { DashboardPage } from './routes/DashboardPage';
import { DefinitionsPage } from './routes/DefinitionsPage';
import { DataPage } from './routes/DataPage';
import { Spinner } from './components/primitives';
import { RulesChangedNotice } from './components/RulesChangedNotice';

// Las pantallas de administracion solo las abre quien administra reglas: se cargan bajo
// demanda y no pesan en el bundle de quien solo consulta.
const RulesPage = lazy(() => import('./routes/RulesPage').then((module) => ({ default: module.RulesPage })));
const RulesComparePage = lazy(() =>
  import('./routes/RulesComparePage').then((module) => ({ default: module.RulesComparePage })),
);

/** Las pantallas donde el filtro actua. En el resto se muestra, pero sin controles. */
const FILTERED_ROUTES = new Set(['/', '/portfolio', '/insights', '/code', '/roadmap']);

/** Pantallas sobre las reglas, no sobre un recorte del portafolio: el filtro no se muestra. */
const UNFILTERED_ROUTES = ['/admin', '/definitions'];

export function App() {
  return (
    <Routes>
      <Route element={<Shell />}>
        {/* El dashboard es la pagina de inicio desde el 2026-09-18 (ADR 0025). */}
        <Route index element={<DashboardPage />} />
        <Route path="portfolio" element={<PortfolioPage />} />
        <Route path="definitions" element={<DefinitionsPage />} />
        <Route path="applications/:eaiCode" element={<OverviewPage />} />
        <Route path="applications/:eaiCode/calculation" element={<ExplainPage />} />
        <Route path="applications/:eaiCode/what-if" element={<WhatIfPage />} />
        <Route path="applications/:eaiCode/roadmap" element={<RoadmapPage />} />
        <Route path="dashboard" element={<LegacyDashboard />} />
        <Route path="insights" element={<InsightsPage />} />
        <Route path="code" element={<CodePage />} />
        <Route path="roadmap" element={<PortfolioRoadmapPage />} />
        <Route path="data" element={<DataPage />} />
        <Route
          path="admin/rules"
          element={
            <Suspense fallback={<Spinner label="Loading the rules editor…" />}>
              <RulesPage />
            </Suspense>
          }
        />
        <Route
          path="admin/rules/compare"
          element={
            <Suspense fallback={<Spinner label="Loading the comparison…" />}>
              <RulesComparePage />
            </Suspense>
          }
        />

        {/* Las rutas en espanol de las fases anteriores siguen resolviendo: un enlace
            guardado o compartido entonces no se rompe (ADR 0017). */}
        <Route path="aplicaciones/:eaiCode" element={<LegacyApplication suffix="" />} />
        <Route path="aplicaciones/:eaiCode/calculo" element={<LegacyApplication suffix="/calculation" />} />
        <Route path="aplicaciones/:eaiCode/what-if" element={<LegacyApplication suffix="/what-if" />} />
        <Route path="aplicaciones/:eaiCode/roadmap" element={<LegacyApplication suffix="/roadmap" />} />
        <Route path="datos" element={<LegacyData />} />
        <Route path="cargas" element={<Navigate to="/data" replace />} />
        <Route path="linaje" element={<Navigate to="/data?tab=lineage" replace />} />
      </Route>
    </Routes>
  );
}

/** `/dashboard` fue la direccion del dashboard antes de ser la pagina de inicio. */
function LegacyDashboard() {
  const { search } = useLocation();

  return <Navigate to={`/${search}`} replace />;
}

function LegacyApplication({ suffix }: { suffix: string }) {
  const { eaiCode = '' } = useParams();
  const { search } = useLocation();

  return <Navigate to={`/applications/${encodeURIComponent(eaiCode)}${suffix}${search}`} replace />;
}

function LegacyData() {
  const { search } = useLocation();
  const params = new URLSearchParams(search);
  if (params.get('tab') === 'linaje') params.set('tab', 'lineage');
  const query = params.toString();

  return <Navigate to={`/data${query ? `?${query}` : ''}`} replace />;
}

function Shell() {
  const session = useSession();
  const login = useLogin();

  if (session.isPending) {
    return (
      <main className="grid min-h-full place-items-center">
        <Spinner label="Checking your session…" />
      </main>
    );
  }

  // Un 401 no es un error del que recuperarse: es la sesion que falta.
  if (!session.data?.isAuthenticated) {
    return (
      <main className="grid min-h-full place-items-center px-6">
        <div className="w-full max-w-sm rounded-md border border-rule bg-surface px-6 py-7 text-center">
          <p className="font-mono text-[11px] font-semibold tracking-[0.13em] text-accent uppercase">
            Modernization Score
          </p>
          <h1 className="mt-2 text-[19px] font-semibold">Sign in to continue</h1>
          <p className="mt-1.5 text-[13px] text-ink-2">
            Your session is kept on the server. The browser never receives a token.
          </p>
          <button
            type="button"
            onClick={() => login.mutate()}
            disabled={login.isPending}
            className="mt-5 w-full rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:opacity-60"
          >
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </button>
        </div>
      </main>
    );
  }

  // Autenticado pero sin permisos: se dice una vez y con claridad, en lugar de
  // mostrar pantallas que responderian 403 una por una.
  if (!session.data.permissions.canRead) {
    return (
      <main className="grid min-h-full place-items-center px-6">
        <div className="w-full max-w-sm rounded-md border border-rule bg-surface px-6 py-7 text-center">
          <p className="font-mono text-[11px] font-semibold tracking-[0.13em] text-accent uppercase">
            Modernization Score
          </p>
          <h1 className="mt-2 text-[19px] font-semibold">Your user does not have access</h1>
          <p className="mt-1.5 text-[13px] text-ink-2">
            Your session is valid, but it has neither the read role nor an assigned portfolio
            scope. Ask the team that manages access.
          </p>
        </div>
      </main>
    );
  }

  return (
    <Workspace
      name={session.data.name}
      canAdministerRules={session.data.permissions.canAdministerRules}
    />
  );
}

/**
 * El armazon con los filtros dentro. Van aqui y no en cada pantalla porque el
 * requisito es que un filtro elegido en cualquier vista sobreviva al cambio de
 * menu: eso solo se cumple si el control vive por encima de las pantallas y la URL
 * es la fuente de verdad.
 */
function Workspace({ name, canAdministerRules }: { name: string; canAdministerRules: boolean }) {
  const { pathname } = useLocation();
  const { search } = useFilters();

  // En el armazon y no por pantalla: es quien ve todas las navegaciones.
  useFilterMemory();

  return (
    <div className="flex min-h-full flex-col">
      <header className="border-b border-rule bg-surface">
        <div className="mx-auto flex max-w-[1240px] flex-wrap items-center gap-x-6 gap-y-2 px-6 py-3">
          <span className="font-mono text-[12px] font-semibold tracking-[0.12em] text-accent uppercase">
            Modernization Score
          </span>

          {/* Cada enlace lleva el filtro consigo: cambiar de menu no lo pierde. */}
          <nav className="flex gap-1">
            <Tab to="/" search={search}>
              Dashboard
            </Tab>
            <Tab to="/portfolio" search={search}>
              Portfolio
            </Tab>
            <Tab to="/insights" search={search}>
              Insights
            </Tab>
            <Tab to="/code" search={search}>
              Code
            </Tab>
            <Tab to="/roadmap" search={search}>
              Roadmap
            </Tab>
            <Tab to="/data" search={search}>
              Data
            </Tab>
            <Tab to="/definitions" search="">
              Definitions
            </Tab>
            {/* Solo a quien administra reglas. El servidor lo exige igual: esto solo
                evita ofrecer una pantalla que responderia 403. */}
            {canAdministerRules && (
              <Tab to="/admin/rules" search="">
                Rules
              </Tab>
            )}
          </nav>

          <span className="ml-auto font-mono text-[11.5px] text-ink-3">{name}</span>
        </div>
      </header>

      <main className="mx-auto flex w-full max-w-[1240px] flex-1 flex-col gap-4 px-6 py-6">
        <RulesChangedNotice canAdministerRules={canAdministerRules} />

        {/* Las reglas son de todo el portafolio: un filtro ahi no acota nada. */}
        {FILTERED_ROUTES.has(pathname) ? (
          <FilterBar />
        ) : UNFILTERED_ROUTES.some((route) => pathname.startsWith(route)) ? null : (
          <ActiveFilters />
        )}
        <Outlet />
      </main>

      <footer className="border-t border-rule px-6 py-3">
        <p className="mx-auto max-w-[1240px] font-mono text-[11px] text-ink-3">
          Excludes SaaS, cancelled and decommissioned applications. Only Common Service and Core
          Business Applications.
        </p>
      </footer>
    </div>
  );
}

function Tab({
  to,
  search,
  children,
}: {
  to: string;
  search: string;
  children: React.ReactNode;
}) {
  return (
    <NavLink
      to={{ pathname: to, search }}
      end={to === '/'}
      className={({ isActive }) =>
        `rounded-sm px-2.5 py-1 text-[13px] ${
          isActive ? 'bg-accent-wash font-medium text-accent' : 'text-ink-2 hover:bg-surface-2'
        }`
      }
    >
      {children}
    </NavLink>
  );
}
