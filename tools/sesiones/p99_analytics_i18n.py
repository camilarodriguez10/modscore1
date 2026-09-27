from patchlib import patch

patch(r"src\web\src\i18n\analytics.ts", [
("""  'resilience',
  'insights',
] as const;""",
"""  'resilience',
  'insights',
  'code',
] as const;"""),
("""  insights: { name: 'Insights screen', question: 'The six questions answered group by group' },
};""",
"""  insights: { name: 'Insights screen', question: 'The six questions answered group by group' },
  code: { name: 'Code', question: 'Which components are past or near their end of life, and where is the code risk?' },
};"""),
("""  'insights.duplicateDefault': {
    label: 'Default concentration threshold',
    help: 'Applications from which a match is a concentration, when the link does not say. The screen offers 2 to 5.',
  },
};""",
"""  'insights.duplicateDefault': {
    label: 'Default concentration threshold',
    help: 'Applications from which a match is a concentration, when the link does not say. The screen offers 2 to 5.',
  },
  'code.approachingMonths': {
    label: 'Ending soon within',
    help: 'A component whose end of life falls within this many months counts as ending soon.',
  },
  'code.outdatedMajorsBehind': {
    label: 'Outdated library from (major versions behind)',
    help: 'A library this many major versions or more behind its latest release is outdated.',
  },
  'code.staleMonths': {
    label: 'Stale repository after',
    help: 'A repository with no commits on its default branch for this many months, or archived, is stale.',
  },
  'code.points.eolComponent': { label: 'Risk index: per component past end of life', help: 'Runtime, framework, OS image or database past its end of security support.' },
  'code.points.approachingComponent': { label: 'Risk index: per component ending soon', help: 'Its end of life falls within the window above.' },
  'code.points.criticalVulnerability': { label: 'Risk index: per critical advisory', help: 'Open critical advisories on a dependency.' },
  'code.points.highVulnerability': { label: 'Risk index: per high advisory', help: 'Open high advisories on a dependency.' },
  'code.points.deprecatedLibrary': { label: 'Risk index: per deprecated library', help: 'Marked deprecated by its author in the registry.' },
  'code.points.outdatedLibrary': { label: 'Risk index: per outdated library', help: 'Behind its latest release by the major versions above.' },
  'code.points.staleRepository': { label: 'Risk index: per stale repository', help: 'No recent commits, or archived.' },
  'code.moderateFrom': { label: 'Moderate code risk from', help: 'Code risk index: a sum of points, with no cap.' },
  'code.highFrom': { label: 'High code risk from', help: 'Code risk index: a sum of points, with no cap.' },
  'code.criticalFrom': { label: 'Critical code risk from', help: 'Code risk index: a sum of points, with no cap.' },
  'code.topLibraries': { label: 'Libraries listed', help: 'How many libraries with findings the Code screen lists.' },
};"""),
("""  'resilience.operational': 'The operational cut-offs must go down: moderate > high > critical.',
};""",
"""  'resilience.operational': 'The operational cut-offs must go down: moderate > high > critical.',
  'code.levels': 'The code risk levels must go up: moderate < high < critical.',
};"""),
("""    case 'hours':
      return `${number(value, 0)} h`;
    case 'points':""",
"""    case 'hours':
      return `${number(value, 0)} h`;
    case 'months':
      return `${number(value, 0)} ${value === 1 ? 'month' : 'months'}`;
    case 'points':"""),
("""    case 'hours':
      return 'hours';
    case 'points':""",
"""    case 'hours':
      return 'hours';
    case 'months':
      return 'months';
    case 'points':"""),
("""        missing: ['End-of-support and end-of-life dates (TLM) would add an EOS/EOL indicator.'],""",
"""        missing: [
          'End-of-support and end-of-life dates for every inventoried component (TLM) would add an EOS/EOL indicator. The Code screen shows them for what the repositories declare.',
        ],"""),
])

# Las explicaciones de la pantalla Code, al final del archivo.
from patchlib import ROOT
p = ROOT / r"src\web\src\i18n\analytics.ts"
t = p.read_text(encoding="utf-8", newline="")
nl = "\r\n" if "\r\n" in t else "\n"
block = """
// ------------------------------------------------------------- pantalla Code

const CODE_SOURCES =
  'Each application’s repositories in GitHub Enterprise, matched by the repository property that names its EAI code.';
const CODE_MANIFESTS =
  'From each repository, only metadata: the runtimes, frameworks, OS images and databases its manifests declare (global.json, .nvmrc, pom.xml, package.json, Dockerfile…), and its dependency graph with open security advisories. No source code is read or stored.';
const CODE_CATALOG =
  'End-of-life dates from the endoflife.date snapshot that ships with the release.';
const CODE_PENDING =
  'Live repositories: until the GitHub Enterprise integration is in place, repositories, versions and advisories are sample data generated from the portfolio (ADR 0026).';

/**
 * La explicacion de un panel de la pantalla Code: 0 el indice y los indicadores, 1 el
 * ciclo de vida, 2 las bibliotecas, 3 las discrepancias con CMT.
 */
export function codeExplanation(panel: number, definitions: AnalyticsDocument): Explanation {
  const d = reader(definitions);
  const months = d.text('code.approachingMonths');

  switch (panel) {
    case 1:
      return {
        area: 'code',
        considered: [CODE_MANIFESTS, CODE_CATALOG],
        rules: [
          'A version belongs to the longest cycle that starts it: 3.10.2 is Python 3.10, 1.8.0_392 is Java 8.',
          `Past end of life: the cycle’s free security support ended on or before today, or the cycle is discontinued without a date. Ending soon: it ends within ${months}. Support ended: active support is over and security fixes continue. Supported: none of the above.`,
          'Paid extended support (Oracle, Spring, Microsoft) is shown next to the date but does not count as supported.',
          'Each component counts once per repository: the same Node.js 14 in three repositories is three upgrades.',
        ],
        missing: ['Products outside the catalog, such as IBM WebSphere, show as not assessable until they are added.'],
      };
    case 2:
      return {
        area: 'code',
        considered: ['The dependency graph of each repository: npm, NuGet, Maven, PyPI and Go modules, direct and transitive.'],
        rules: [
          `Outdated: ${d.plain('code.outdatedMajorsBehind')} or more major versions behind the latest release in its registry. Deprecated: marked so by its author.`,
          'Advisories: open critical and high advisories on the version in use, the same data Dependabot shows.',
          `Listed: the ${d.plain('code.topLibraries')} libraries with findings, most critical advisories first, then deprecated, then the most widespread.`,
        ],
        missing: ['License risk and how deep a library sits in the dependency tree.'],
      };
    case 3:
      return {
        area: 'code',
        considered: [
          'Currency Score: the score CMT delivers (Raw_Tech_Score, 0–100), declared for the application in the inventory.',
          CODE_MANIFESTS,
        ],
        rules: [
          `Hidden obsolescence: the Currency Score is ${d.plain('currency.legacyBelow')} or more — current by the inventory — but the code declares at least one component past its end of life.`,
          'Inventory behind the code: the Currency Score says legacy, yet no declared component is past its end of life. Either the application was modernized and CMT has not caught up, or its repositories do not declare the old stack.',
          'Applications without a Currency Score or without repositories are not compared.',
        ],
        missing: [],
      };
    default:
      return {
        area: 'code',
        considered: [CODE_SOURCES, CODE_MANIFESTS, CODE_CATALOG],
        rules: [
          `Code risk index = the sum over the application’s repositories of ${d.text('code.points.eolComponent')} per component past its end of life, ${d.text('code.points.approachingComponent')} per component ending within ${months}, ${d.text('code.points.criticalVulnerability')} per critical advisory, ${d.text('code.points.highVulnerability')} per high advisory, ${d.text('code.points.deprecatedLibrary')} per deprecated library, ${d.text('code.points.outdatedLibrary')} per outdated library and ${d.text('code.points.staleRepository')} per stale repository (no commits in ${d.text('code.staleMonths')}, or archived).`,
          `Levels: moderate from ${d.plain('code.moderateFrom')}, high from ${d.plain('code.highFrom')}, critical from ${d.plain('code.criticalFrom')}. An application without repositories has no level: it is not low risk, it is not visible.`,
          'A deprecated library counts once, as deprecated, even if it is also outdated.',
          'The index does not change the Modernization Score: the score follows the BRD rules.',
        ],
        missing: [CODE_PENDING, 'Mainframe (COBOL) and SAP (ABAP) code, which does not live in GitHub.'],
      };
  }
}
"""
t = t.rstrip("\r\n") + nl + block.replace("\n", nl)
p.write_text(t, encoding="utf-8", newline="")
print("ok codeExplanation")
