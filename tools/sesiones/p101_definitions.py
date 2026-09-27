from patchlib import patch

patch(r"src\web\src\routes\DefinitionsPage.tsx", [
("""import { useDashboard, useSession } from '../api/hooks';
import type { AnalyticsDocument, DashboardView } from '../api/types';""",
"""import { useCode, useDashboard, useSession } from '../api/hooks';
import type { AnalyticsDocument, CodeView, DashboardView } from '../api/types';"""),
("""import {
  areaName,
  dashboardExplanation,""",
"""import {
  areaName,
  codeExplanation,
  dashboardExplanation,"""),
("""  const { data, isPending, error } = useDashboard({});
  const session = useSession();""",
"""  const { data, isPending, error } = useDashboard({});
  // La cobertura de la pantalla Code sale de su propia vista: repositorios, no cargas.
  const code = useCode({});
  const session = useSession();"""),
("""      {SECTIONS.map((section) => (
        <Definition key={section.id} section={section} view={data} definitions={definitions} />
      ))}""",
"""      {SECTIONS.map((section) => (
        <Definition key={section.id} section={section} view={data} code={code.data} definitions={definitions} />
      ))}"""),
("""  explain: (definitions: AnalyticsDocument, view: DashboardView) => Explanation;
  coverage: (view: DashboardView) => string;
}""",
"""  explain: (definitions: AnalyticsDocument, view: DashboardView) => Explanation;
  coverage: (view: DashboardView, code: CodeView | undefined) => string;
  /** Cada cuanto se recalcula, si no es con cada carga. */
  frequency?: string;
}"""),
("""    coverage: (view) => `Computed over the same ${countOf(total(view), 'application')}, group by group.`,
  },
];""",
"""    coverage: (view) => `Computed over the same ${countOf(total(view), 'application')}, group by group.`,
  },
  {
    id: 'code',
    title: 'Code · Component obsolescence & risk',
    area: 'code',
    explain: (d) => {
      const parts = [0, 1, 2, 3].map((panel) => codeExplanation(panel, d));
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
];"""),
("""function Definition({
  section,
  view,
  definitions,
}: {
  section: Section;
  view: DashboardView;
  definitions: AnalyticsDocument;
}) {""",
"""function Definition({
  section,
  view,
  code,
  definitions,
}: {
  section: Section;
  view: DashboardView;
  code: CodeView | undefined;
  definitions: AnalyticsDocument;
}) {"""),
("""            <dt className="text-ink-2">Frequency</dt>
            <dd>With every upload and every published rules version.</dd>
            <dt className="text-ink-2">Data quality</dt>
            <dd>{section.coverage(view)}</dd>""",
"""            <dt className="text-ink-2">Frequency</dt>
            <dd>{section.frequency ?? 'With every upload and every published rules version.'}</dd>
            <dt className="text-ink-2">Data quality</dt>
            <dd>{section.coverage(view, code)}</dd>"""),
])
