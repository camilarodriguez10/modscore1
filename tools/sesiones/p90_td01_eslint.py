from patchlib import patch

patch(r"docs\PENDING-ITEMS.md", [
("""  | xunit.runner.visualstudio | 3.x | 4.x |

- **Work:**""",
"""  | xunit.runner.visualstudio | 3.x | 4.x |
  | eslint (dev only) | 9.39.5 | 10.11.0 |

- **ESLint 9 is end of life (seen on 2026-09-21).** `npm ci` prints
  `npm warn deprecated eslint@9.39.5: This version is no longer supported`. It is a warning,
  not a vulnerability (`npm audit` is clean), and ESLint only runs in development and CI: it is
  not part of the site the Channel serves. The difference with the rows above is that an
  unsupported major will not get a patch if a vulnerability appears. Moving to ESLint 10 is
  feasible today —`typescript-eslint` 8.70 and `eslint-plugin-react-hooks` 7 accept it, and
  `@eslint/js` 10 exists— and would be a dedicated change with `npm run lint` green before and
  after.
- **Work:**"""),
])
