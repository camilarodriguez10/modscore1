import { useEffect } from 'react';
import { Link, useLocation } from 'react-router';
import { useCode, useDashboard, useSession } from '../api/hooks';
import type { AnalyticsDocument, CodeView, DashboardView } from '../api/types';
import { AreaStatus } from '../components/Explanation';
import { Label, QueryError, Spinner, fmt } from '../components/primitives';
import {
  areaName,
  codeExplanation,
  dashboardExplanation,
  formatParameter,
  insightsExplanation,
  parameterHelp,
  parameterLabel,
  type Explanation,
} from '../i18n/analytics';
import { countOf } from '../i18n/notices';

/**
 * Diccionario de indicadores: como se responde cada pregunta, con que datos, con que
 * valores vigentes, quien responde por la definicion, cada cuanto se calcula y cuanto
 * del portafolio tiene el dato (paso 3 de la propuesta EA Data & Insights, ADR 0025).
 *
 * Se lee sobre todo el portafolio que el usuario puede ver, sin los filtros de la barra:
 * una definicion no cambia con el recorte, y la cobertura que importa es la de todo.
 */
export function DefinitionsPage() {
  const { data, isPending, error } = useDashboard({});
  // La cobertura de la pantalla Code sale de su propia vista: repositorios, no cargas.
  const code = useCode({});
  const session = useSession();
  const { hash } = useLocation();

  // El enlace "See every definition" de cada panel apunta a su seccion.
  useEffect(() => {
    if (!data || !hash) return;
    document.getElementById(decodeURIComponent(hash.slice(1)))?.scrollIntoView({ block: 'start' });
  }, [data, hash]);

  if (isPending) return <Spinner label="Reading the definitions…" />;
  if (error) return <QueryError error={error} title="The definitions could not be loaded" />;

  const definitions = data.definitions;
  const canEdit = session.data?.permissions.canAdministerRules ?? false;

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <Label>Definitions</Label>
        <h1 className="text-[22px] leading-tight font-semibold">How the six questions are answered</h1>
        <p className="max-w-[85ch] text-[13px] text-ink-2">
          Every indicator and panel of the dashboard and the Insights screen, with the data it considers, how it is
          calculated with the values in force, who owns the definition and how much of the portfolio has the data.
          These definitions travel with rules <span className="font-mono text-[12px]">{data.rulesetVersion}</span>
          {canEdit ? (
            <>
              {' '}
              and are edited in{' '}
              <Link to="/admin/rules?tab=analytics" className="text-accent underline underline-offset-2">
                Rules → Portfolio analytics
              </Link>
              .
            </>
          ) : (
            <>; a rules administrator edits them, and every change is published as a new rules version.</>
          )}
        </p>
      </header>

      <nav aria-label="Definitions" className="flex flex-wrap gap-1.5">
        {SECTIONS.map((section) => (
          <a
            key={section.id}
            href={`#${section.id}`}
            className="rounded-sm border border-rule px-2.5 py-1 text-[12.5px] text-ink-2 hover:bg-surface-2"
          >
            {section.title}
          </a>
        ))}
      </nav>

      {SECTIONS.map((section) => (
        <Definition key={section.id} section={section} view={data} code={code.data} definitions={definitions} />
      ))}
    </div>
  );
}

interface Section {
  id: string;
  title: string;
  /** Area de definiciones cuyos parametros se listan; null si sigue el modelo de score. */
  area: string | null;
  explain: (definitions: AnalyticsDocument, view: DashboardView) => Explanation;
  coverage: (view: DashboardView, code: CodeView | undefined) => string;
  /** Cada cuanto se recalcula, si no es con cada carga. */
  frequency?: string;
}

const total = (view: DashboardView) => view.dashboard.applications.length;

const share = (part: number, whole: number) => (whole === 0 ? '0' : fmt((part / whole) * 100, 0));

const SECTIONS: readonly Section[] = [
  {
    id: 'currency',
    title: 'Indicators and Currency Score',
    area: 'currency',
    explain: (d) => ({ ...dashboardExplanation(0, d), area: 'currency' }),
    coverage: (view) => {
      const currency = view.dashboard.kpis.find((k) => k.key === 'currency');
      const withData = currency?.base ?? 0;
      return `${countOf(withData, 'application')} of ${total(view)} (${share(withData, total(view))} %) have a Currency Score from CMT.`;
    },
  },
  {
    id: 'health',
    title: '1 · How healthy is the portfolio?',
    area: null,
    explain: (d) => dashboardExplanation(1, d),
    coverage: (view) =>
      `On average, ${fmt(view.dashboard.health.dataCoverage, 0)} % of the model’s weight had data, over ${countOf(total(view), 'application')}.`,
  },
  {
    id: 'prioritization',
    title: '2 · Which applications should be modernized first?',
    area: 'prioritization',
    explain: (d) => dashboardExplanation(2, d),
    coverage: (view) => {
      const inMatrix = view.dashboard.prioritization.points.length;
      return `${countOf(inMatrix, 'application')} of ${total(view)} (${share(inMatrix, total(view))} %) have a roadmap that raises their score and enter the matrix.`;
    },
  },
  {
    id: 'complexity',
    title: '3 · Where should we reduce complexity?',
    area: 'complexity',
    explain: (d) => dashboardExplanation(3, d),
    coverage: (view) => {
      const measured = view.dashboard.complexity.dimensions.filter((dimension) => dimension.status !== 'needs-data');
      return `${measured.length} of ${view.dashboard.complexity.dimensions.length} dimensions are measured with today’s sources.`;
    },
  },
  {
    id: 'risk',
    title: '4 · Where is technology risk concentrated?',
    area: 'risk',
    explain: (d) => dashboardExplanation(4, d),
    coverage: (view) => {
      const withRisk = view.dashboard.applications.filter((a) => a.riskScore !== null).length;
      return `${countOf(withRisk, 'application')} of ${total(view)} (${share(withRisk, total(view))} %) have a Risk Score.`;
    },
  },
  {
    id: 'obsolescence',
    title: '5 · What is driving obsolescence?',
    area: 'obsolescence',
    explain: (d) => dashboardExplanation(5, d),
    coverage: (view) => {
      const withCurrency = view.dashboard.obsolescence.withCurrency;
      return `${countOf(withCurrency, 'application')} of ${total(view)} (${share(withCurrency, total(view))} %) have a Currency Score; lost points use every metric with data.`;
    },
  },
  {
    id: 'resilience',
    title: '6 · Where are the resilience gaps?',
    area: 'resilience',
    explain: (d) => dashboardExplanation(6, d),
    coverage: (view) => {
      const withData = view.dashboard.resilience.withData;
      return `${countOf(withData, 'application')} of ${total(view)} (${share(withData, total(view))} %) have recovery or operational data.`;
    },
  },
  {
    id: 'insights',
    title: 'Insights screen',
    area: 'insights',
    explain: (d) => {
      const parts = [2, 3, 4, 6].map((question) => insightsExplanation(question, d, 0));
      return {
        area: 'insights',
        considered: parts.flatMap((part) => part.considered),
        rules: parts.flatMap((part) => part.rules),
        missing: parts.flatMap((part) => part.missing),
      };
    },
    coverage: (view) => `Computed over the same ${countOf(total(view), 'application')}, group by group.`,
  },
  {
    id: 'code',
    title: 'Code · Component obsolescence & risk',
    area: 'code',
    explain: (d) => {
      const parts = [0, 1, 4, 6, 2, 3].map((panel) => codeExplanation(panel, d));
      return {
        area: 'code',
        considered: [...new Set(parts.flatMap((part) => part.considered))],
        rules: parts.flatMap((part) => part.rules),
        missing: [...new Set(parts.flatMap((part) => part.missing))],
      };
    },
    coverage: (_view, code) => {
      if (!code) return 'Reading the repository inventory…';
      const { kpis } = code.analysis;
      const source = code.source.connected
        ? 'from GitHub Enterprise'
        : code.source.provider === 'Sample'
          ? 'sample data until GitHub Enterprise is connected'
          : 'no repository inventory yet';
      return `${countOf(kpis.applicationsWithRepositories, 'application')} of ${kpis.applications} (${share(kpis.applicationsWithRepositories, kpis.applications)} %) have repositories (${source}). ${kpis.unknownComponents} of ${countOf(kpis.components, 'component')} are not in the end-of-life catalog.`;
    },
    frequency: 'With every repository scan and every published rules version. End-of-life dates change when the catalog snapshot is refreshed.',
  },
];

function Definition({
  section,
  view,
  code,
  definitions,
}: {
  section: Section;
  view: DashboardView;
  code: CodeView | undefined;
  definitions: AnalyticsDocument;
}) {
  const explanation = section.explain(definitions, view);
  const parameters = section.area ? definitions.parameters.filter((p) => p.area === section.area) : [];

  return (
    <section
      id={section.id}
      aria-labelledby={`${section.id}-title`}
      className="scroll-mt-4 rounded-md border border-rule bg-surface"
    >
      <header className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-b border-rule px-4 py-3">
        <h2 id={`${section.id}-title`} className="text-[15px] leading-snug font-semibold">
          {section.title}
        </h2>
        {section.area ? (
          <AreaStatus area={section.area} definitions={definitions} />
        ) : (
          <span className="text-[11.5px] text-ink-2">Follows the score model</span>
        )}
      </header>

      <div className="grid gap-5 px-4 py-4 text-[12.5px] leading-relaxed lg:grid-cols-[3fr_2fr]">
        <div className="flex flex-col gap-4">
          <List title="Definition and formula" items={explanation.rules} />
          <List title="Data considered" items={explanation.considered} />
          {explanation.missing.length > 0 && <List title="Not measured yet" items={explanation.missing} muted />}
        </div>

        <div className="flex flex-col gap-4">
          {parameters.length > 0 && (
            <div>
              <Label>Values in force{section.area ? ` · ${areaName(section.area)}` : ''}</Label>
              <table className="mt-1.5 w-full text-left text-[12.5px]">
                <thead className="sr-only">
                  <tr>
                    <th scope="col">Parameter</th>
                    <th scope="col">Value</th>
                  </tr>
                </thead>
                <tbody>
                  {parameters.map((parameter) => (
                    <tr key={parameter.key} className="border-t border-rule align-top">
                      <th scope="row" className="py-1.5 pr-3 font-normal">
                        {parameterLabel(parameter.key)}
                        <span className="block text-[11.5px] text-ink-2">{parameterHelp(parameter.key)}</span>
                      </th>
                      <td className="tnum py-1.5 text-right font-mono font-semibold whitespace-nowrap">
                        {formatParameter(parameter)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1.5">
            <dt className="text-ink-2">Frequency</dt>
            <dd>{section.frequency ?? 'With every upload and every published rules version.'}</dd>
            <dt className="text-ink-2">Data quality</dt>
            <dd>{section.coverage(view, code)}</dd>
          </dl>
        </div>
      </div>
    </section>
  );
}

function List({ title, items, muted = false }: { title: string; items: readonly string[]; muted?: boolean }) {
  if (items.length === 0) return null;

  return (
    <div>
      <Label>{title}</Label>
      <ul className={`mt-1.5 flex list-disc flex-col gap-1 pl-4 ${muted ? 'text-ink-2' : ''}`}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}
