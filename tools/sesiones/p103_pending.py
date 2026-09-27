from patchlib import patch

patch(r"docs\PENDING-ITEMS.md", [
# IDs
("""- **ID** — stable identifier to use in meetings, tickets and commits. `SR` scoring rules, `PA` platform/architecture, `SEC` security, `TD` technical debt, `PF` product follow-up, `OP` operations.""",
 """- **ID** — stable identifier to use in meetings, tickets and commits. `SR` scoring rules, `PA` platform/architecture, `SEC` security, `TD` technical debt, `PF` product follow-up, `OP` operations, `DQ` data, `DD` dashboard definitions, `GH` Code screen and GitHub Enterprise integration."""),
# Done
("""- **Fixed on 2026-09-18:** the Insights risk index counted Kenna **below 50** as weak vulnerability management.""",
 """- **Code screen** (2026-09-26, ADR 0026): `/code`, *Component obsolescence & risk*. What each application's repositories declare —runtimes, frameworks, app servers, OS images, databases and libraries— checked against **end-of-life dates from endoflife.date** (a versioned snapshot: 22 products, 273 release cycles) and open advisories; libraries that need attention; where the code contradicts the CMT Currency Score; and a code risk index per application with its detail. Its 14 parameters are a new `code` area of the definitions, edited from the portal. **Repositories are sample data** generated from the portfolio until GitHub Enterprise is connected (GH-01 … GH-09); the screen says so, and the sample provider refuses to start outside Development.
- **Fixed on 2026-09-18:** the Insights risk index counted Kenna **below 50** as weak vulnerability management."""),
# Tests
("""  - 770 .NET tests: 270 engine, 178 contracts, 322 API.
  - 58 SPA unit tests.
  - 40 end-to-end scenarios: 19 flows and 21 accessibility checks with axe, in light and dark. Microsoft Edge locally, Chromium in CI.""",
 """  - 828 .NET tests: 309 engine, 183 contracts, 336 API.
  - 63 SPA unit tests.
  - 49 end-to-end scenarios: 24 flows and 25 accessibility checks with axe, in light and dark. Microsoft Edge locally, Chromium in CI."""),
# What needs the team
("""| Operations | OP-01 … OP-05 | No. The first push was done on 2026-09-15; OP-04 (branch protection and repository settings) is P1 |""",
 """| Operations | OP-01 … OP-05 | No. The first push was done on 2026-09-15; OP-04 (branch protection and repository settings) is P1 |
| Code screen and GitHub Enterprise integration | GH-01 … GH-09 | No. The screen runs on sample data in Development only; GH-01 and GH-02 unblock real repositories |"""),
# Review order: insert as 12 and renumber
("""| 12 | SR-09 · SR-11 · SR-14 · SR-15 · PA-04 · PA-06 … PA-09 · DQ-01 · DQ-03 … DQ-07 · OP-05 (P2) | Smaller rules, scaling and data items | See each item | Several | — |
| 13 | DD-07 · DD-08 · SR-08 · SR-13 · TD-* · PF-* (P3) | Tech Debt KPI; a separate role for definitions; the rest | Five KPIs (today) · a sixth for tech debt. One role (today) · a definitions-only role | Product; security | — |""",
 """| 12 | GH-01 · GH-02 · GH-03 (P2) | Connecting the Code screen to GitHub Enterprise: the GitHub App, the repository → application mapping, and how the collector reaches endoflife.date and the package registries | A read-only GitHub App (*Metadata*, *Contents*, *Dependabot alerts*, *Custom properties*) installed on the chosen organizations · a personal access token (not recommended). Mapping: a repository custom property `eai-code` · topics `eai-<code>` · a catalog file. Egress: through the internal mirrors if they exist · direct to endoflife.date and the registries | GitHub Enterprise admins + security + platform; application owners for the mapping | Real repositories on the Code screen |
| 13 | SR-09 · SR-11 · SR-14 · SR-15 · PA-04 · PA-06 … PA-09 · DQ-01 · DQ-03 … DQ-07 · GH-04 … GH-09 · OP-05 (P2) | Smaller rules, scaling, data and Code-screen items | See each item | Several | — |
| 14 | DD-07 · DD-08 · SR-08 · SR-13 · TD-* · PF-* (P3) | Tech Debt KPI; a separate role for definitions; the rest | Five KPIs (today) · a sixth for tech debt. One role (today) · a definitions-only role | Product; security | — |"""),
# At a glance: DQ-05 status and GH rows
("""| DQ-05 | End-of-life and end-of-support dates of components | Data | P2 | RPT / architecture repository owner | Decision needed |""",
 """| DQ-05 | End-of-life and end-of-support dates of components | Data | P2 | RPT / architecture repository owner | Partly addressed (2026-09-26): the Code screen dates what repositories declare; the inventory side is open |"""),
("""| OP-05 | Portable package distribution checklist | Operations | P2 | Whoever distributes it | Open |""",
 """| OP-05 | Portable package distribution checklist | Operations | P2 | Whoever distributes it | Open |
| GH-01 | GitHub App on GitHub Enterprise: permissions, organizations, version | Code screen | P2 | GitHub Enterprise admins + security | Decision needed |
| GH-02 | Mapping repositories to applications (EAI code) | Code screen | P2 | Enterprise architecture + application owners | Decision needed |
| GH-03 | Where the collector runs and how it reaches the internet | Code screen / platform | P2 | Platform + security | Decision needed |
| GH-04 | Source of security advisories: Dependabot, OSV.dev or both | Code screen / security | P2 | Application security | Decision needed |
| GH-05 | Container images: what runs versus what is declared | Code screen / security | P3 | DevSecOps | Open |
| GH-06 | End-of-life catalog refresh and extended-support exceptions | Code screen | P2 | Enterprise architecture + vendor management | Decision needed |
| GH-07 | Code risk index points and levels | Code screen | P2 | Enterprise architecture + application security | Decision needed (defaults implemented) |
| GH-08 | Applications outside GitHub (COBOL, ABAP) | Code screen / data | P3 | Enterprise architecture | Decision needed |
| GH-09 | What happens when the code contradicts the CMT Currency Score | Code screen / process | P3 | CMT owner + enterprise architecture | Decision needed |"""),
# DQ-05 context
("""- **What it changes:** obsolescence gains a time axis, and the priority list could bring forward what expires soon.
- **Effort:** M.

### DQ-06""",
 """- **What it changes:** obsolescence gains a time axis, and the priority list could bring forward what expires soon.
- **Partly addressed on 2026-09-26:** the Code screen dates every component the repositories declare, with endoflife.date as the source (ADR 0026). What the inventory declares —components that do not show up in any repository— still has no dates.
- **Effort:** M.

### DQ-06"""),
# New section before Operations
("""---

## 6. Operations""",
 """---

## 5quater. Code screen and GitHub Enterprise integration

The Code screen (`/code`, PLAN §8.11, [ADR 0026](adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md))
answers which components are past or near their end of life, which libraries need
attention, where the code contradicts the CMT Currency Score and which applications carry
the most code risk. **Today its repositories are sample data**, generated from the
portfolio in Development only; the end-of-life dates are real, from an endoflife.date
snapshot. The items below turn it into real data. The architecture is decided: a separate
`code-collector` that runs as a scheduled job with a read-only GitHub App, reads metadata,
the dependency-graph SBOM, Dependabot alerts and a closed list of manifest files, never
clones or runs code, and publishes the inventory to the System layer, which owns storage
and calculation.

### GH-01 — GitHub App on GitHub Enterprise: permissions, organizations, version
- **Priority:** P2 · **Owner:** GitHub Enterprise admins + security · **Status:** Decision needed
- **Context:** The collector needs to read repositories without a personal token tied to a person. A GitHub App gives short-lived installation tokens (one hour) and fine-grained, read-only permissions.
- **Question:** which organizations it is installed on; approval of the permissions *Metadata: read*, *Contents: read* (only the listed manifest files are fetched), *Dependabot alerts: read* and *Custom properties: read*; and whether the GitHub Enterprise version in use exposes the dependency-graph SBOM export and the Dependabot alerts API, with the dependency graph and Dependabot enabled.
- **Recommendation:** a GitHub App installed on a pilot organization first; its private key in the secrets manager, delivered as a file like the service token.
- **Acceptance criteria:** the collector lists the pilot organization's repositories and reads an SBOM with an installation token; no write permission is granted.
- **Effort:** S (approval) + M (collector).

### GH-02 — Mapping repositories to applications (EAI code)
- **Priority:** P2 · **Owner:** enterprise architecture + application owners · **Status:** Decision needed
- **Context:** Every number on the screen belongs to an application, and the portfolio scope is applied per application (ADR 0012). A repository without a mapping cannot be counted, and a wrong mapping shows one team's risk under another team's application.
- **Options:** a repository custom property `eai-code` set by the organization (recommended: typed, auditable, and readable with one permission) · topics `eai-<code>` (anyone with admin on the repository can change them) · a catalog file in a central repository.
- **Open points:** monorepos that hold several applications (a list of codes in the property); shared libraries that belong to no application (not mapped, listed apart); who keeps the property up to date.
- **Acceptance criteria:** at least 90 % of the pilot organization's active repositories are mapped; unmapped repositories appear in a list visible only to full-portfolio users.
- **Effort:** S–M, mostly organizational.

### GH-03 — Where the collector runs and how it reaches the internet
- **Priority:** P2 · **Owner:** platform + security · **Status:** Decision needed
- **Context:** Besides GitHub Enterprise, the collector reads endoflife.date (end-of-life dates) and the package registries (latest version, deprecation). The cluster may not have outbound internet access.
- **Options:** through internal mirrors or proxies (an artifact repository for the registries; a mirrored copy of the endoflife.date data) · direct egress to those hosts with a network policy allow-list.
- **Also decide:** the schedule (daily is enough: an end of life does not change overnight, and advisories arrive with the SBOM), and where the job runs (same cluster as the System layer, its own namespace, no ingress).
- **Acceptance criteria:** the job runs on schedule with egress limited to the listed hosts, and a failed run keeps the previous inventory.
- **Effort:** S–M.

### GH-04 — Source of security advisories: Dependabot, OSV.dev or both
- **Priority:** P2 · **Owner:** application security · **Status:** Decision needed
- **Context:** Dependabot alerts come with GitHub (the GitHub Advisory Database) but depend on the GitHub Enterprise licensing and on Dependabot being enabled per repository. OSV.dev is open and covers the same ecosystems, queried by package and version from the SBOM. Veracode SCA already feeds the Modernization Score; the Code screen must not be read as a second count of the same flaws.
- **Recommendation:** Dependabot where it is enabled, OSV.dev as the fallback for repositories without it; the screen says which source each alert came from.
- **Effort:** S–M.

### GH-05 — Container images: what runs versus what is declared
- **Priority:** P3 · **Owner:** DevSecOps · **Status:** Open
- **Context:** Manifests declare intent. The image that runs can differ: a base image rebuilt, a runtime installed in a `RUN` line, a vendored dependency. The CI already runs Trivy on this project's images.
- **Work:** scan the images in the registry with Syft or Trivy, map image → repository → application, and show a component as declared, running, or both.
- **Effort:** M–L.

### GH-06 — End-of-life catalog refresh and extended-support exceptions
- **Priority:** P2 · **Owner:** enterprise architecture + vendor management · **Status:** Decision needed
- **Context:** The catalog is a snapshot of endoflife.date taken on 2026-09-26 (`contracts/eol-catalog-v1.json`). Vendors extend dates, and some contracts buy extended support (for example Oracle Java SE 8 until 2030). The screen shows paid extended support but does not count it as supported, because the catalog does not know the contracts. Products missing from endoflife.date (IBM WebSphere, for example) show as not assessable.
- **Question:** the refresh cadence once the collector exists (recommended: every run, validated, keeping the previous snapshot if the new one does not validate); and whether contracts become exceptions per product and cycle —with an owner, a reason and an expiry date— edited from the portal like the definitions.
- **Effort:** S for the refresh; M for exceptions.

### GH-07 — Code risk index points and levels
- **Priority:** P2 · **Owner:** enterprise architecture + application security · **Status:** Decision needed (defaults implemented)
- **Context:** The index adds points per finding: 10 per component past its end of life, 4 per component ending within 12 months, 5 per critical advisory, 2 per high advisory, 3 per deprecated library, 1 per library two or more major versions behind and 5 per repository with no commits in 12 months or archived. Levels: moderate 10, high 25, critical 50. It is a sum with no cap, so an application with many repositories accumulates more. All 14 values are editable in *Rules → Portfolio analytics* (area `code`).
- **On the sample data:** 186 of 250 applications have repositories; 96 have at least one component past its end of life and 58 are at high or critical code risk.
- **Question:** confirm or change the points and levels; decide whether a critical application should weigh more (today it does not), and whether the index should be normalized by number of repositories.
- **Effort:** S if only values change (a publication); S–M for a multiplier or a normalization (code).

### GH-08 — Applications outside GitHub (COBOL, ABAP)
- **Priority:** P3 · **Owner:** enterprise architecture · **Status:** Decision needed
- **Context:** Mainframe (COBOL) and SAP (ABAP) code usually does not live in GitHub. On the sample data, 64 of 250 applications have no repository. The screen shows them as "no repositories, no level": not low risk, not visible.
- **Options:** accept the gap and report it · add another source for their platform versions (the mainframe inventory, SAP release data) as components with a lifecycle.
- **Effort:** S (accept) · M per new source.

### GH-09 — What happens when the code contradicts the CMT Currency Score
- **Priority:** P3 · **Owner:** CMT owner + enterprise architecture · **Status:** Decision needed
- **Context:** The screen names two disagreements: *hidden obsolescence* (the Currency Score says current, the code declares components past their end of life) and *inventory behind the code* (the Currency Score says legacy, the code declares nothing past its end of life). On the sample data there are 65 of the first.
- **Question:** is there a process to send these to the CMT owners to correct the inventory? Should the code evidence ever influence the score? (Today it does not: the BRD defines the score.)
- **Effort:** S (a report or export) · process work.

---

## 6. Operations"""),
# Section 9 tests table
("""| Engine (golden cases, mapping table, what-if, roadmap, rules editor, comparison) | 200 | `dotnet test tests/Modernization.Scoring.Engine.Tests` |
| Source contracts (Dummy, File, Database; rules stores) | 178 | `dotnet test tests/Modernization.Sources.ContractTests` |
| API end-to-end through both layers and security | 288 | `dotnet test tests/Modernization.Api.Tests` |
| SPA unit (Vitest) | 35 | `npm --prefix src/web test` |
| SPA end-to-end (Playwright, Edge locally, Chromium in CI) | 6 | `npm --prefix src/web run test:e2e` |
| SPA accessibility (axe, five screens in light and dark) | 10 | Same command; `e2e/accessibility.spec.ts` |
| SPA end-to-end against the portable package | 6 | With `start.ps1 -NoBrowser` running, from `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |""",
 """| Engine (golden cases, mapping table, what-if, roadmap, rules editor, comparison, dashboard, insights, end-of-life catalog, code analyzer) | 309 | `dotnet test tests/Modernization.Scoring.Engine.Tests` |
| Source contracts (Dummy, File, Database; rules stores; sample repositories) | 183 | `dotnet test tests/Modernization.Sources.ContractTests` |
| API end-to-end through both layers and security | 336 | `dotnet test tests/Modernization.Api.Tests` |
| SPA unit (Vitest) | 63 | `npm --prefix src/web test` |
| SPA end-to-end (Playwright, Edge locally, Chromium in CI) | 24 | `npm --prefix src/web run test:e2e` |
| SPA accessibility (axe, the daily screens and the Code detail, in light and dark) | 25 | Same command; `e2e/accessibility.spec.ts` |
| SPA end-to-end against the portable package | 49 | With `start.ps1 -NoBrowser` running, from `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |"""),
# Phase plan
("""| 17 | Infrastructure findings (last, by decision) | H-16 workload identity, H-17 image signing and immutable logs, H-20 session management, H-21 upload antivirus | SEC-15…18 |""",
 """| 17 | Infrastructure findings (last, by decision) | H-16 workload identity, H-17 image signing and immutable logs, H-20 session management, H-21 upload antivirus | SEC-15…18 |
| C2 | Code screen on real repositories (parallel track) | `code-collector` with a read-only GitHub App, repository → application mapping, egress, advisory source, catalog refresh; the screen (C1) already exists on sample data | GH-01…GH-09 |"""),
])
