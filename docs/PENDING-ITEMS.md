# Modernization Score — Pending Items for Team Review

| | |
|---|---|
| **Date** | 2026-09-14 |
| **Scope** | Everything still open after phases 00–12 and the review-and-remediation phase 13 |
| **Audience** | Product owner, business owners of the score (BRD), architecture, security, platform/operations, data owners (EAI, CMT, MTS, RPT, SCA, DevOps, OCP) |
| **Language** | English on purpose, for a mixed team. The rest of the project documentation is in Spanish; links point to it |
| **How to read** | Section 0 is the one-page summary. Every item below repeats its full context so it can be discussed on its own, even if that makes the document redundant |

---

## How to use this document

Each item has the same fields:

- **ID** — stable identifier to use in meetings, tickets and commits. `SR` scoring rules, `PA` platform/architecture, `SEC` security, `TD` technical debt, `PF` product follow-up, `OP` operations, `DQ` data, `DD` dashboard definitions, `GH` Code screen and GitHub Enterprise integration.
- **Priority**
  - **P0** — blocks any production use.
  - **P1** — needed before a pilot with real users and data.
  - **P2** — needed before scaling beyond one replica or one region.
  - **P3** — improvement; can wait.
- **Suggested owner** — who can answer or do it. It is a suggestion, not an assignment.
- **Status** — `Decision needed`, `Open`, `Deferred (by decision)`, `Accepted risk`, `Done (for traceability)`.
- **Context** — what exists today and why the item exists.
- **Evidence** — files, tests, captures or measurements that support the context.
- **Question / work** — the exact decision or task.
- **Options** and **Recommendation** when there is a choice.
- **Acceptance criteria** — how we will know it is closed.
- **Dependencies**, **Effort** (rough: S ≤ 2 days, M ≤ 1 week, L ≤ 3 weeks, XL > 3 weeks) and **Risk if not done**.

Source-of-truth rule agreed on 2026-09-14: **when documents disagree about how the score is calculated, the BRD and dashboard captures win.** The captures are indexed in [docs/brd/README.md](brd/README.md) and are not stored in git (internal documentation).

---

## 0. Executive summary

### What is already done

- Score engine reproducing the documented cases exactly: the **2026-10-07 report capture = 66.12** with the built-in rules, and **EAI 14381 = 79.67** (BRD worked example) and **EAI 10969 = 79.61** (earlier dashboard capture) with the BRD bands for OSS and Raw Tech. Since 2026-10-07 those two metrics use their raw value, as the report does (ADR 0032): with the built-in rules the two cases give 80.70 and 80.26.
- Two-layer API-led backend (.NET 10: Channel BFF + System), React 19 SPA in English, three interchangeable data adapters (Dummy, File, Database/SQLite) with shared contract tests.
- What-if engine, roadmap/future plan (MAPS), history of uploads, global URL filters, lineage.
- Rules administration from the portal (phase 12): draft, full-portfolio impact simulation, immutable published versions, step-up reauthentication, audit.
- **Phase 13 (this review)**:
  - BRD rule "null or zero values are excluded" applied where the BRD tables do not score zero.
  - Rules comparison view, per application and metric by metric, including an unpublished draft.
  - Rule changes marked on the portfolio trend.
  - CSV exports that follow the active filters, plus an in-app notice when new rules are published.
  - Robustness when the rules store fails, and step-up that is independent of the identity provider (ready for APIM).
  - Performance improvements, measured before and after.
  - SPA unit tests and end-to-end tests.
  - Contract test between the C# view models and the TypeScript types.
  - Git repository with backup scripts. The portable package that existed from 2026-09-14 was removed on 2026-09-27 by decision: the application runs from the code, with command lines for cmd and PowerShell.
- **Executive dashboard** (2026-09-18, phases D1, D2 and D3): the home page (`/`), *Portfolio Health & Risk Intelligence*, built from the *EA Data & Insights – MVP Proposal*. Five KPIs, one panel per portfolio question and one shared detail table. Five more filters on every screen: CRE, Region, App Status, Primary Capability and EAI Code.
- **Definitions editable from the portal** (ADR 0025): every threshold, level, list length and weight behind the six questions —37 parameters in seven areas, each with a status (provisional or agreed) and an owner— travels with the rules versions and is edited in *Rules → Portfolio analytics*, with the same draft, simulation, step-up, audit and publication as a score weight. The simulation shows how the dashboard indicators move. Every panel explains its calculation with the values in force, and the *Definitions* page is the KPI dictionary.
- **Decided on 2026-09-18:** the Currency Score is the score CMT delivers (`Raw_Tech_Score`), and the dashboard is the home page.
- **Code screen** (2026-09-26, ADR 0026): `/code`, *Component obsolescence & risk*. What each application's repositories declare —runtimes, frameworks, app servers, OS images, databases and libraries— checked against **end-of-life dates from endoflife.date** (a versioned snapshot: 22 products, 273 release cycles) and open advisories; libraries that need attention; where the code contradicts the CMT Currency Score; and a code risk index per application with its detail. Its 15 parameters are a new `code` area of the definitions, edited from the portal. **Repositories are sample data** generated from the portfolio until GitHub Enterprise is connected (GH-01 … GH-09); the screen says so, and the sample provider refuses to start outside Development.
- **Code screen over time** (2026-09-27): the outlook today and in one, two and three years if nothing changes (components past end of life, share of code still supported, code risk), an improvement plan with a target release and an urgency per action, and score, Currency Score and Risk Score per upload. `data/timeline` holds the same dummy portfolio in four quarterly uploads, and the Dummy mode now rewinds the same applications instead of generating a different portfolio per quarter.
- **Column mapping before uploading** (2026-09-28, ADR 0027): *Review column mapping* reads only the header row of each file in the browser and shows, for every field of the database, which column of the file will load it (same name, known alias, as in the last upload, or not in the file), its type and the metrics that use it. The person confirms or picks another column; the System validates it, stores the file with canonical column names and **records the mapping with the upload** (manifest in the file adapter, a `ColumnMaps` column in the database adapter), so the next review proposes it. *Data → Mapping* shows the same per source without choosing files.
- **Why rows were rejected** (2026-09-28): the upload result groups rejected rows by problem —column, expected type, the first value, how many rows and the first row numbers— counting every bad column of a row, with a hint when the value gives the cause away (`N/A` placeholders, decimals in whole-number columns, dot thousands separators, shifted columns). Before, the screen only showed how many rows were left out. Also fixed: a number with both separators (`1.234,5`) was read as 1.2345 without warning; the last separator is now the decimal one.
- **Reading tolerates real exports** (2026-10-02, ADR 0028), after the organization shared its EAI master header, exported from Power BI ("Suma de Kenna_Score", both "MFA" and "Does this Application Utilize MFA?", a Year/Quarter/Month/Day hierarchy, no APP_ID). An unreadable value in an **optional** field now leaves that field without data and keeps the row, reported as *Values loaded as empty*; before, it rejected the whole row, which in the EAI master is the whole application. No-value markers (`N/A`, `-`, `(En blanco)`…) read as empty; numbers in any regional format (an integer accepts zero decimals: "650,00" is 650, before it silently became 65000); `VERDADERO`/`FALSO`; identifiers normalized ("14,381" → "14381"); the exact field name wins over an alias and summary prefixes ("Suma de", "Sum of", "Promedio de"…) are recognized; a repeated EAI keeps the last row and reports the others; without APP_ID in the master, tech debt is linked by the EAI code, because the organization confirmed that its APP_ID is the same code as the EAI.
- **Renamed Product Strategy export** (2026-10-07): `EAI_APP_ID`, `DCMSN_SFTWR_CNT`, `INVST_SFTWR_CNT`, `MGRT_SFTWR_CNT` and `TLRN_SFTWR_CNT` are recognized as aliases of the existing fields; `Record Create Timestamp` is not used. The previous names still work.
- **Excel workbooks and several files per source** (2026-10-07, ADR 0034): uploads accept `.xlsx` (the first visible sheet, read without new dependencies and with explicit limits against crafted ZIP and XML files) and several files of one source, such as APIM in one file per region, which are loaded together. Each file keeps its own verdict and row numbers; an exact copy is rejected because it would count its rows twice.
- **Automatic plans to 90** (2026-10-07, ADR 0033): applications without a MAPS roadmap get a plan with the levers that take them to 90, so question 2 of the dashboard is filled with exports that lack the `Roadmap` column. Applications marked *Eliminate* get none.
- **Fixed on 2026-09-18:** the Insights risk index counted Kenna **below 50** as weak vulnerability management. Kenna runs from 0 to 1000 and higher is worse, so it counted the best applications. It now counts Kenna ≥ 670 (the BRD's Critical/High band), and the threshold is a parameter.
- **Tests**:
  - 1,050 .NET tests: 349 engine, 342 contracts, 359 API.
  - 95 SPA unit tests.
  - 62 end-to-end scenarios: 35 flows and 27 accessibility checks with axe, in light and dark. Microsoft Edge locally, Chromium in CI.
  - Zero vulnerable packages, and `npm audit` is clean.

### What needs the team

| Group | Items | Blocking production? |
|---|---|---|
| Scoring rules (business) | SR-01 … SR-15 | SR-01, SR-03, SR-07 must be confirmed before scores are published to the organization (P0/P1). [scoring-decisions.md](scoring-decisions.md) is the same content written for the conversation with the business owner |
| Platform and architecture | PA-01 … PA-09 | PA-01 (target and database engine) and PA-02 (authentication through APIM) are P0 |
| Security | SEC-01 … SEC-24 | SEC-19 and SEC-20 are P0 (SEC-21 was decided on 2026-09-15); ASVS partials are P1; H-16/H-17/H-20/H-21 deferred to the last phase by decision |
| Technical debt | TD-01 … TD-15 | None blocks. TD-05 done and TD-04 automated on 2026-09-15; TD-06, TD-08 and TD-09 closed on 2026-09-16 |
| Product follow-ups | PF-01 … PF-08 | No |
| Data that would sharpen the portfolio answers | DQ-01 … DQ-07 | No. The six questions are already answered with today's sources; these make two of the answers real instead of indicative |
| Executive dashboard definitions | DD-01 … DD-08 | Not for production. Since 2026-09-18 each answer is a publication from the portal, not a deployment: whoever decides can mark the area as agreed the same day. DD-02 and DD-03 drive the headline KPIs and the matrix |
| Operations | OP-01 … OP-05 | No. The first push was done on 2026-09-15; OP-04 (branch protection and repository settings) is P1 |
| Code screen and GitHub Enterprise integration | GH-01 … GH-09 | No. The screen runs on sample data in Development only; GH-01 and GH-02 unblock real repositories |

### Review order (2026-09-18)

What to take to whom, in this order. The first answer listed is the recommended one, and
it is what the platform does today unless the row says otherwise.

| # | Items | The doubt | Possible answers (recommended first) | Who answers | Blocks |
|---|---|---|---|---|---|
| 1 | SR-01 · SR-03 · SR-07 (P0) | How composites are computed; Tech Debt weight 5 or 10; whether a zero is excluded like a null | Composites: count-weighted average (today) · per-count BRD weights · always the pre-computed value. Tech Debt: keep 10 (confirm with one capture of an application with Tech Debt data) · 5, moving 5 points to another Architecture metric. Zero: current default, excluded except where the BRD table scores it · excluded everywhere · never excluded | Business owner of the score + Power BI model owner | Publishing scores to the organization |
| 2 | PA-01 (P0) | Deployment target and database engine | AKS with the engine the organization already runs (PostgreSQL or SQL Server) and managed identity · another cluster | Platform + architecture | Any production deployment |
| 3 | PA-02 · PA-03 · PA-05 (P0/P1) | APIM as issuer, and how a user's portfolio scope arrives | APIM issues the service token and the scope comes in a claim (code ready) · scope derived from groups with a group→scope map (code ready) | Identity + platform | Production authentication |
| 4 | SEC-19 · SEC-20 (P0) | Step-up against a real identity provider; ZAP against staging | Run both in the first test environment (runbooks ready) · accept the risk until then | Identity + DevSecOps | Production |
| 5 | DD-02 · DD-03 (P1) | Risk Score base and levels; criticality, opportunity and the candidate quadrant | Risk: the risk pillar inverted, mockup levels 40/60/80 (today) · a composite of SCA, Kenna and currency with its own weights. Criticality: critical + broad (today) · a business tier (DQ-03). Opportunity: roadmap uplift, or an automatic plan to 90 without a MAPS roadmap (today) · uplift ÷ effort once DQ-02 exists | Enterprise architecture + security; business owner | Showing the dashboard to leadership as final. Levels are a portal publication; a new formula is code |
| 6 | SR-04 · SR-05 · SR-06 · SR-10 · SR-12 (P1) | Override rule; SCA presence or count; gaps between BRD bands; containerized "2 + 2 points"; LOB/CIO aggregation | Override: define the rule before building it. SCA: presence, plus the list of scanned applications. Bands: contiguous, published from the portal. Containerized: obsolete text, remove it from the BRD. Aggregation: average of application scores (today) | Business owner; application security | Parity with the Power BI dashboard |
| 7 | TD-13 · TD-04 (P1) | Second-person security review of phases 09–13; manual visual walkthrough | Schedule both now; the walkthrough should include the dashboard, *Definitions* and *Portfolio analytics* | Security; UX | Nothing technical; confidence |
| 8 | OP-01 · OP-02 · OP-03 · OP-04 (P1) | Runbooks, backups, upload retention, repository settings | Retention: keep the last N uploads (`Uploads:RetainVersions`) · a time-based policy from compliance. Repository: branch protection and required checks | Operations; compliance; repository owner | Operating in production |
| 9 | SEC-24 (P1) | Export policy | Keep auditing (event 4213) and add a SIEM rule · add a label or watermark · deny export to some roles | Security + data owners | — |
| 10 | DQ-02 (P1) | Effort or cost per application | A T-shirt size S/M/L per application · person-weeks · budget | Application owners + finance | Turning *what to modernize first* from indicative into real |
| 11 | DD-01 · DD-04 · DD-05 · DD-06 (P2) | Legacy threshold X; complexity thresholds; resilience cut-offs; band names | X = 60 (today) · another value. Complexity: raise the thresholds when real data arrives (they saturate on dummy data). Resilience: 24/48 h and 80/60/40 (today) · the Stability Scoring concepts. Bands: the model's everywhere (today) · the mockup's everywhere | CMT owner; enterprise architecture; operations; business owner | Nothing; each one is a portal publication |
| 12 | GH-01 · GH-02 · GH-03 (P2) | Connecting the Code screen to GitHub Enterprise: the GitHub App, the repository → application mapping, and how the collector reaches endoflife.date and the package registries | A read-only GitHub App (*Metadata*, *Contents*, *Dependabot alerts*, *Custom properties*) installed on the chosen organizations · a personal access token (not recommended). Mapping: a repository custom property `eai-code` · topics `eai-<code>` · a catalog file. Egress: through the internal mirrors if they exist · direct to endoflife.date and the registries | GitHub Enterprise admins + security + platform; application owners for the mapping | Real repositories on the Code screen |
| 13 | SR-09 · SR-11 · SR-14 · SR-15 · PA-04 · PA-06 … PA-09 · DQ-01 · DQ-03 … DQ-07 · GH-04 … GH-09 · OP-05 (P2) | Smaller rules, scaling, data and Code-screen items | See each item | Several | — |
| 14 | DD-07 · DD-08 · SR-08 · SR-13 · TD-* · PF-* (P3) | Tech Debt KPI; a separate role for definitions; the rest | Five KPIs (today) · a sixth for tech debt. One role (today) · a definitions-only role | Product; security | — |

### All items at a glance

| ID | Title | Category | Priority | Suggested owner | Status |
|---|---|---|---|---|---|
| SR-01 | Composite metrics strategy (RFC-01) | Scoring rules | P0 | Business owner of the score + Power BI model owner | Decision needed |
| SR-02 | Currency Score definition (RFC-02) | Scoring rules | P2 | CMT owner | Decided (2026-09-18): CMT `Raw_Tech_Score` |
| SR-03 | Tech Debt weight 5 vs 10 (RFC-03) | Scoring rules | P0 | Business owner of the score | Decision needed |
| SR-04 | Override rule semantics (RFC-04) | Scoring rules | P1 | Business owner of the score | Decision needed |
| SR-05 | SCA presence vs count, scanned vs not scanned (RFC-05) | Scoring rules | P1 | Application security (SCA) | Decision needed |
| SR-06 | Gaps between BRD bands (RFC-08) | Scoring rules | P1 | Business owner + Power BI model owner | Decision needed |
| SR-07 | "Null or zero values are excluded" (RFC-13) | Scoring rules | P0 | Business owner of the score | Decision needed (default implemented) |
| SR-08 | DevSecOps `0/NULL-3 → 20` scores a null (RFC-14) | Scoring rules | P3 | DevOps maturity owner | Decision needed |
| SR-09 | `Consolidate` disposition not in the BRD table (RFC-15) | Scoring rules | P2 | Enterprise architecture (MAPS) | Decision needed |
| SR-10 | Containerized "added to product strategy (2 pts) and architecture value (2 pts)" (RFC-16) | Scoring rules | P1 | Business owner of the score | Decision needed |
| SR-11 | Rows with EAI Code 0 in the OSS file (RFC-17) | Scoring rules / data quality | P2 | RPT data owner | Decision needed |
| SR-12 | Aggregation level: per application vs LOB/CIO | Scoring rules | P1 | Business owner of the score | Decision needed |
| SR-13 | Obsolete scoring example EAI 9768 in the BRD | Documentation | P3 | BRD author | Open |
| SR-14 | Meaning of the "Monitoring" column in MONITOREO | Scoring rules / data | P2 | Observability owner | Decision needed |
| SR-15 | MFA with unknown user count assumed "with users" | Scoring rules | P2 | Application security | Decision needed |
| PA-01 | Deployment target and database engine (RFC-07) | Platform | P0 | Platform/architecture | Decision needed |
| PA-02 | Authentication through Azure API Management with OAuth (ADR 0020) | Platform / security | P0 | Platform + identity + security | Option decided on 2026-09-16 (C with A); code ready, waiting for APIM |
| PA-03 | Portfolio scope claim (RFC-10) | Platform / security | P0 | Identity | Code ready: claim, or a group-to-scope map; identity has to confirm which |
| PA-04 | Shared session cache (RFC-11) | Platform | P2 | Platform | Open |
| PA-05 | Workload identity Channel → System (RFC-12, H-16) | Platform / security | P1 | Platform + security | Code ready (client credentials + JWT validation); waiting for the issuer |
| PA-06 | Horizontal scaling constraints | Platform | P2 | Platform | Open |
| PA-07 | Secrets manager integration verified | Platform / security | P1 | Platform | Open |
| PA-08 | Observability and SIEM rules | Platform / security | P1 | Operations + security | Open |
| PA-09 | Notifications to responsible parties when rules are published | Platform / product | P2 | Product + platform | Decision needed |
| SEC-01 … SEC-14 | ASVS L2 partial requirements | Security | P1 | Security + platform | SEC-06, SEC-07, SEC-12 done; SEC-14 accepted with compensating controls; the rest need a test environment |
| SEC-15 | ASVS 3.3.4 — see and close own sessions (H-20) | Security | P3 | Development | Deferred (by decision) |
| SEC-16 | ASVS 7.3.3 — tamper-proof logs (H-17) | Security | P2 | Operations | Deferred (by decision) |
| SEC-17 | ASVS 12.4.2 — antivirus on uploads (H-21) | Security | P3 | Development + platform | Deferred (by decision) |
| SEC-18 | H-16 static shared service token | Security | P2 | Platform | Deferred (by decision) |
| SEC-19 | Step-up against a real identity provider not verified | Security | P0 | Identity + development | Open |
| SEC-20 | Security gates never run outside CI (DAST, Trivy, CodeQL, Checkov, gitleaks, image build) | Security | P0 | DevSecOps | In progress (CI green; ZAP pending) |
| SEC-21 | Repository visibility, BRD captures and commit metadata | Security | P0 | Repository owner | Decided (2026-09-15) |
| SEC-22 | Portable package uses development authentication | Security | P1 | Everyone who distributes it | Closed (2026-09-27): package removed |
| SEC-23 | Rules timeline shows the publication reason to every reader | Security / privacy | P3 | Rules administrators | Accepted risk |
| SEC-24 | CSV exports and data exfiltration | Security | P1 | Security + data owners | Decision needed |
| TD-01 | Outdated packages kept on purpose | Technical debt | P3 | Development | Accepted (policy) |
| TD-02 | React 19 features promised in the plan but not used | Technical debt | P3 | Development | Decision needed |
| TD-03 | OpenAPI-generated client replaced by a contract test | Technical debt | P3 | Development | Decision needed |
| TD-04 | Visual QA and accessibility audit of the new screens | Quality | P1 | UX + development | Automated part done (2026-09-15); manual walkthrough open |
| TD-05 | End-to-end tests in CI | Quality | P1 | DevSecOps | Done (2026-09-15) |
| TD-06 | Simulation latency under concurrency | Performance | P1 | Development | Done (2026-09-16): criterion met on 2 cores without a code change; re-measure on the real pod |
| TD-07 | History performance with the database adapter | Performance | P2 | Development | Open |
| TD-08 | Load test coverage of Channel, exports and comparison | Performance | P2 | Development | Done (2026-09-16) |
| TD-09 | Language sweep does not cover the new endpoints | Quality | P1 | Development | Done (2026-09-16) |
| TD-10 | CSV of a draft's impact | Product / debt | P3 | Development | Open |
| TD-11 | Mixed line endings in the repository | Hygiene | P3 | Development | Decision needed |
| TD-12 | Lock files and runtime-specific publish | Supply chain | P3 | Development | Moot since 2026-09-27 (portable build removed) |
| TD-13 | Security review of phases 09–13 by a second person | Security | P1 | Security | Open |
| TD-14 | Size of the rules editor screen | Maintainability | P3 | Development | Open |
| TD-15 | Warm-up service and caches with many versions | Performance | P3 | Development | Open |
| TD-16 | Column mapping of any upload in the lineage, and a first-time review | Product / data | P3 | Development | Open |
| TD-17 | An upload reads each file twice | Performance | P3 | Development | Open |
| PF-01 | Save and share what-if scenarios | Product | P3 | Product | Open |
| PF-02 | Override rule UI and audit | Product | P2 | Product | Blocked by SR-04 |
| PF-03 | Currency Score KPI | Product | P3 | Product | Done (2026-09-18) |
| PF-04 | Excel (XLSX) exports | Product | P3 | Product | Decision needed |
| PF-05 | Compare rules for a group of applications (LOB, country) | Product | P3 | Product | Open |
| PF-06 | Rules change history per application | Product | P3 | Product | Open |
| PF-07 | KPI dictionary and portal editing of the dashboard definitions (dashboard phase D3) | Product | P2 | Product + enterprise architecture | Done (2026-09-18) |
| PF-08 | EA Value Evidence Register | Product | P3 | Enterprise architecture | Decision needed |
| DQ-01 | Application class column, as the business words the question | Data | P2 | EAI master owner + enterprise architecture | Decision needed |
| DQ-02 | Estimated cost or effort per application | Data / product | P1 | Application owners + finance | Decision needed |
| DQ-03 | Business value of an application (revenue, volume, tier) | Data / product | P2 | Business owners | Decision needed |
| DQ-04 | Application dependencies and integrations | Data | P2 | Enterprise architecture | Decision needed |
| DQ-05 | End-of-life and end-of-support dates of components | Data | P2 | RPT / architecture repository owner | Partly addressed (2026-09-26): the Code screen dates what repositories declare; the inventory side is open |
| DQ-06 | Incident and availability history | Data | P2 | Operations | Decision needed |
| DQ-07 | Hosting and migration status beyond the cloud flags | Data | P3 | Platform | Decision needed |
| DD-01 | The legacy threshold X (the Currency Score is decided) | Dashboard definitions | P2 | CMT owner + enterprise architecture | Decision needed (X only) |
| DD-02 | Risk Score formula and risk levels | Dashboard definitions | P1 | Enterprise architecture + security | Decision needed |
| DD-03 | Criticality, opportunity and the candidate quadrant | Dashboard definitions | P1 | Business owner of the score + enterprise architecture | Decision needed |
| DD-04 | Complexity dimensions and their thresholds | Dashboard definitions | P2 | Enterprise architecture | Decision needed |
| DD-05 | Resilience levels (Stability Scoring concepts) | Dashboard definitions | P2 | Operations + resilience owner | Decision needed |
| DD-06 | Health band names and cut-offs: mockup vs model | Dashboard definitions | P2 | Business owner of the score | Decision needed |
| DD-07 | Tech Debt KPI (the home page is decided) | Dashboard definitions | P3 | Product | Decision needed (KPI only) |
| DD-08 | Who may change the definitions: the rules administrator, or a separate role | Dashboard definitions | P3 | Security + enterprise architecture | Decision needed |
| DD-09 | Portfolio population: which applications the score and the dashboard leave out | Dashboard definitions / data | P1 | Business owner of the score + EAI data owner | Decision needed |
| OP-01 | Runbook walkthrough with operations | Operations | P1 | Operations | Open |
| OP-02 | Backups of the data volume and the rules store | Operations | P1 | Operations | Open |
| OP-03 | Upload retention policy | Operations / compliance | P1 | Data owners + compliance | Decision needed |
| OP-04 | GitHub repository settings (first push done) | Operations | P1 | Repository owner | Open |
| OP-05 | Portable package distribution checklist | Operations | P2 | Whoever distributes it | Closed (2026-09-27): package removed |
| GH-01 | GitHub App on GitHub Enterprise: permissions, organizations, version | Code screen | P2 | GitHub Enterprise admins + security | Decision needed |
| GH-02 | Mapping repositories to applications (EAI code) | Code screen | P2 | Enterprise architecture + application owners | Decision needed |
| GH-03 | Where the collector runs and how it reaches the internet | Code screen / platform | P2 | Platform + security | Decision needed |
| GH-04 | Source of security advisories: Dependabot, OSV.dev or both | Code screen / security | P2 | Application security | Decision needed |
| GH-05 | Container images: what runs versus what is declared | Code screen / security | P3 | DevSecOps | Open |
| GH-06 | End-of-life catalog refresh and extended-support exceptions | Code screen | P2 | Enterprise architecture + vendor management | Decision needed |
| GH-07 | Code risk index points and levels | Code screen | P2 | Enterprise architecture + application security | Decision needed (defaults implemented) |
| GH-08 | Applications outside GitHub (COBOL, ABAP) | Code screen / data | P3 | Enterprise architecture | Decision needed |
| GH-09 | What happens when the code contradicts the CMT Currency Score | Code screen / process | P3 | CMT owner + enterprise architecture | Decision needed |

---

## 1. Scoring rules — business decisions

The score formula is `Score = 20 + (Numerator ÷ Denominator) × 80`, computed over flat metrics. Metrics without data leave both the numerator and the denominator. Ruleset **v1.3** has **18 metrics** and three pillars: Modernization Risk Vectors 50, Architecture & Maturity 40, Stability 10. Every rule below is data in `contracts/ruleset-v1.3.json` and can be changed from the Rules screen without redeploying, except where noted.

### SR-01 — Composite metrics strategy (Architectural Value and Product Strategy) · RFC-01

- **Priority:** P0 · **Owner:** business owner of the score + owner of the Power BI model · **Status:** Decision needed
- **Context:**
  - The **BRD weights table** gives a weight to each count separately:
    - Architectural Value: Modern 5, Obsolete 1, Heritage 2, Not Approved 3 (sum 11).
    - Product Strategy: Invest 4, Tolerate 1, Eliminate 2, Migrate 3 (sum 10).
  - The **v1.3 dashboard** shows each composite as a single metric of weight 10.
  - The **BRD worked example for EAI 14381** uses the dashboard form:
    - its Architecture denominator is 25 = RAW Tech 5 + Architectural Value 10 + Product Strategy 10;
    - Architectural Value is 97.50 (15 Modern + 1 Heritage);
    - Product Strategy is 70.00 (2 Invest, 1 Tolerate, 1 Eliminate).
  - Both values are reproduced by a **count-weighted average** of the category scores (Modern/Invest 100, Heritage/Tolerate 60, Obsolete/Not approved/Eliminate/Migrate 20).
  - EAI 10969 on the dashboard shows composites of 75.72 and 88.37. No small product count can produce these, so the dashboard receives the composite already calculated.
- **What is implemented:**
  - Count-weighted average.
  - When the source delivers the composite already calculated, that value wins over the counts.
  - Both composites have weight 10.
- **Evidence:** `contracts/ruleset-v1.3.json` (`archValue`, `productStrategy`), `tests/Modernization.Scoring.Engine.Tests/GoldenFixtures.cs`, captures `image_67128833` (weights table end), `image_67130113` (worked example), `image_67145217` and `image_67184897` (category scores).
- **Question:** Is the composite in the Power BI model the count-weighted average of category scores? Do CMT and MTS deliver the composite pre-calculated, the counts, or both?
- **Options:**
  1. Confirm the count-weighted average (current).
  2. Per-count weights as in the BRD table, which would change the Architecture pillar weights and every score with composites.
  3. Always use the pre-calculated composite and ignore counts.
- **Recommendation:** Option 1. Ask the Power BI owner for the DAX measure of both composites and one more real application with counts visible on the dashboard, to add it as a third golden test.
- **Acceptance criteria:**
  - The DAX measure is documented in the ruleset note.
  - A third golden test with real counts reproduces the dashboard to two decimals.
- **Dependencies:** access to the Power BI model. **Effort:** S (once data is available).
- **Risk if not done:** composites are 20 of 100 weight points. A wrong strategy moves most scores in the Architecture pillar.

### SR-02 — Currency Score definition · RFC-02

- **Priority:** P2 · **Owner:** CMT (Currency Model) owner · **Status:** Decided on 2026-09-18
- **Decision:** the Currency Score is the score CMT delivers: `Raw_Tech_Score` from the Currency Model Detail Report (0–100, computed per component from its end-of-life date). It is shown as a KPI and drives *Legacy Platforms* on the dashboard, outside the Modernization Score formula (where RAW Tech Score keeps its own weight). A 0 is no data. See ADR 0025.
- **Still to confirm:** that no other CMT column is meant. If there is one, it becomes a source column; the definition does not change.
- **Context:**
  - The v1.3 dashboard shows a "Currency Score" (94.66 in one capture) that the BRD does not define.
  - It is not part of the Modernization Score and is not implemented.
- **Evidence:** dashboard captures; PLAN §1.5.
- **Question:** What is the Currency Score, where does it come from and must the platform show it?
- **Recommendation:** Model it as an independent KPI from CMT (percentage of current components) outside the Modernization Score. Do this only if the business wants it on the platform.
- **Acceptance criteria:** documented definition, and a sample file with the source field.
- **Effort:** S–M. **Risk if not done:** the platform differs visually from the dashboard; no effect on the score.

### SR-03 — Tech Debt weight: 5 (BRD) vs 10 (dashboard) · RFC-03

- **Priority:** P0 · **Owner:** business owner of the score · **Status:** Decision needed
- **Context:**
  - The BRD weights table says **5** (capture `image_67128833`); the v1.3 dashboard says **10**.
  - The implementation uses **10**, the published dashboard weight.
  - **Neither golden case has Tech Debt data** (14381 and 10969 both exclude it), so the choice is not verified numerically. An earlier ruleset note said "10 reproduces the published values"; that was imprecise and was corrected on 2026-09-14.
- **Evidence:** `contracts/ruleset-v1.3.json` (`techDebt` note), `GoldenFixtures.cs` (`TechDebtScore = null` in both).
- **Question:** Which weight is correct?
- **Options:** keep 10 (dashboard); change to 5 (BRD). Changing to 5 frees 5 points that must go to another Architecture metric so the pillar still totals 40.
- **Recommendation:** Get one dashboard capture of an application **with** Tech Debt data showing its Architecture pillar numerator and denominator. That settles it numerically.
- **Acceptance criteria:** golden test for that application passes with the chosen weight.
- **Effort:** S. **Risk if not done:** every application with Tech Debt data may be off by a few points in Architecture.

### SR-04 — Override rule semantics · RFC-04

- **Priority:** P1 · **Owner:** business owner of the score · **Status:** Decision needed
- **Context:**
  - The dashboard shows two values per pillar in some cases ("Override rule").
  - The domain model has `ScoreOverride` (value, reason, applied by, applied at), and `EffectiveScore` uses it.
  - There is no screen or API to create overrides, and no source provides them.
- **Question:** When does an override apply? Who can apply it? Is it per pillar or global? Does it expire?
- **Recommendation:** Define the rule first. Implementation would follow the rules-administration pattern: own role, reason, audit and step-up.
- **Acceptance criteria:** documented rule; screen and API with audit; test that `EffectiveScore` uses the override and the breakdown still shows the calculated score.
- **Dependencies:** PF-02. **Effort:** M. **Risk if not done:** the platform cannot reproduce dashboard values that use overrides.

### SR-05 — SCA: presence vs count, and scanned vs not scanned · RFC-05

- **Priority:** P1 · **Owner:** application security (Veracode SCA) · **Status:** Decision needed
- **Context:**
  - The BRD maps SCA flaws by severity: High/Critical → 20, Medium → 20, Low → 60, None → 100. Medium and Low are "removed from score" (weight 0).
  - The implementation evaluates **presence** of at least one open flaw per severity, not the number of flaws.
  - The file `veracode_sca_open_flaws` has one row per flaw, so an application with no open flaws and an application never scanned look the same. ADR 0003 decides:
    - an application present in the file (for example with closed flaws) counts as scanned;
    - an absent application has no SCA data and is excluded, instead of getting the best possible score.
- **Evidence:** [ADR 0003](adr/0003-ausencia-de-dato-frente-a-valor-cero.md), capture `image_67187201`, `MetricExtractors.FromScaSeverity`.
- **Question:** Is presence the right reading? Is there a list of scanned applications?
- **Recommendation:** Keep presence. Ask for the list of scanned applications; if it exists, replace the heuristic with that fact.
- **Acceptance criteria:** confirmation in writing, and a source contract for the scanned list if it exists.
- **Effort:** S–M. **Risk if not done:** unscanned applications either lose SCA weight (today) or would be rewarded (if reversed).

### SR-06 — Gaps between BRD bands · RFC-08

- **Priority:** P1 · **Owner:** business owner + Power BI model owner · **Status:** Decision needed
- **Context:**
  - Operational Score bands are 0–59.9, 60–79.99 and 80–100. A value like 59.95 or 79.995 has no band.
  - Tech Debt bands are 0, 0.1–2.4 and 2.5–5. A value like 0.05 or 2.45 has no band.
  - The engine fails per application with an actionable error instead of guessing. That application leaves aggregates, and its detail shows the problem.
  - Real Tech Debt values come in steps of 0.25, so they never fall in a gap. Operational Score could.
- **Evidence:** [ADR 0004](adr/0004-huecos-entre-bandas-del-brd.md), `MappingRuleTests.ValueOutsideEveryBand_FailsLoudlyInsteadOfScoringWrong`.
- **Question:** Does Power BI use contiguous bands, for example `< 60` instead of `0–59.9`?
- **Recommendation:** Confirm the DAX conditions and publish a rules version with contiguous bands (no redeploy needed).
- **Acceptance criteria:** no value in the documented ranges lacks a band; test with 59.95 and 2.45.
- **Effort:** S. **Risk if not done:** an application with such a value disappears from aggregates until the data changes.

### SR-07 — "Null or zero values are excluded from scoring" · RFC-13

- **Priority:** P0 · **Owner:** business owner of the score · **Status:** Decision needed (default implemented on 2026-09-14)
- **Context:**
  - The BRD section "ALL Sections" (capture `image_67142913`) says: *"Null or zero values are excluded from scoring. All resulting scores are normalized within a 20 to 100 range, and null values are not included in the normalization calculation."*
  - Until phase 12 the engine excluded only null; a zero fell into its band. Kenna 0 scored 100; OSS, RAW Tech and Operational 0 scored 20.
  - Several BRD tables name zero explicitly as a data point: Tech Debt `0 → 100`, SCA `None → 100`, MFA `No (with no users) → 100`, DevSecOps `0/NULL–3 → 20`.
- **What is implemented** ([ADR 0019](adr/0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md)):
  - The specific table wins over the general rule.
  - Each metric declares `zeroIsNoData` in the ruleset: `true` for `kenna`, `oss`, `rawTech` and `operational`, `false` elsewhere.
  - A zero excluded this way shows its own reason on the calculation screen ("reported 0, which the BRD excludes from scoring like a missing value").
  - The validator rejects the flag on non-numeric metrics.
- **Impact measured:**
  - The golden cases do not change.
  - In the dummy data one application with RAW Tech Score 0 changes its score.
  - With real data, applications with Kenna 0 lose the best host-risk score and their denominator drops by 8.
- **Evidence:** `MappingRuleTests.ZeroIsExcludedLikeNull_WhereTheBrdTableDoesNotScoreIt`, `ZeroStillScores_WhereTheBrdTableNamesIt`, `ZeroExclusion_ReducesTheDenominatorInsteadOfScoringTheFloor`.
- **Question:** Does the business read "zero values are excluded" the same way? In particular: in the Kenna source, is 0 a real "no risk" value or "not scanned"?
- **Options:**
  1. Keep the current default.
  2. Exclude zero everywhere, including Tech Debt 0 → no data. This contradicts the Tech Debt table.
  3. Never exclude zero, as before phase 13.
- **Recommendation:** Option 1. If Power BI scores Kenna or OSS zero, publish a rules version with the flag set to `false`; no redeploy is needed.
- **Acceptance criteria:** written confirmation; if different, a published rules version and an updated ADR 0019.
- **Effort:** S. **Risk if not done:** scores of applications with zero values differ from the dashboard.

### SR-08 — DevSecOps `0/NULL–3 → 20` gives a score to a null · RFC-14

- **Priority:** P3 · **Owner:** DevOps maturity assessment owner · **Status:** Decision needed
- **Context:**
  - The BRD table (capture `image_67188993`) maps `0/NULL–3` to 20, so a missing value would score 20.
  - The metric is "Removed from calculation" (weight 0). Today a null is excluded like any other metric, so there is no effect.
- **Question:** If DevSecOps regains weight, should a null score 20 (penalize missing assessments) or be excluded?
- **Recommendation:** Decide only when the metric regains weight. Scoring a null would need a new ruleset option (`nullScore`), which is a small code change.
- **Acceptance criteria:** decision recorded in the ruleset note. **Effort:** S. **Risk if not done:** none today.

### SR-09 — `Consolidate` disposition not in the BRD score table · RFC-15

- **Priority:** P2 · **Owner:** enterprise architecture (MAPS model) · **Status:** Decision needed
- **Context:**
  - The BRD disposition table (capture `image_67180033`) lists Maintain (Patch) 60, Rehost 60, Maintain (Evolve) 100, Refactor 20, Reimagine 20 and Retire 20.
  - The MAPS model in `contracts/roadmap-v1.json` adds `Consolidate` under "Eliminate". It is mapped to 20 by assumption.
  - The metric has weight 0 ("Currently removed from calculation"); it only drives the future plan and coherence alerts.
- **Question:** Is `Consolidate` a valid roadmap? Which score does it map to?
- **Recommendation:** Confirm `Consolidate` and its mapping with enterprise architecture.
- **Acceptance criteria:** confirmed mapping in the ruleset note. **Effort:** S. **Risk if not done:** none on the score today; roadmap views may show a disposition the business does not use.

### SR-10 — Containerized "added to product strategy (2 pts) and architecture value (2 pts)" · RFC-16

- **Priority:** P1 · **Owner:** business owner of the score · **Status:** Decision needed
- **Context:**
  - The BRD weights table shows Containerized with weight 4 struck through.
  - The Containerized section (capture `image_67199233`) says: *"This score is added to product strategy (2 pts) and architecture value (2 pts)"*. `Yes → 100`; `No → No Score (NULL)`.
  - With Containerized, the BRD Architecture table sums to 40: RAW 5 + API 5 + Containerized 4 + Architectural Value 11 + Product Strategy 10 + Tech Debt 5.
  - The dashboard uses RAW 5, API 5, Architectural Value 10, Product Strategy 10 and Tech Debt 10 (also 40). The BRD worked example confirms the composites at 10.
  - Implemented: Containerized has weight 0 and "No" does not score.
- **Question:** Is the "2 + 2 points" text obsolete? Or does a containerized application add points inside the composites, for example a bonus to their value?
- **Recommendation:** Treat the text as a leftover from an older version. Confirm and remove it from the BRD.
- **Acceptance criteria:** BRD updated or rule defined. **Effort:** S (confirmation) to M (if a bonus must be modeled). **Risk if not done:** containerized applications may be underscored compared with the intended model.

### SR-11 — Rows with EAI Code 0 in `OSS_Score_by_EAI` · RFC-17

- **Priority:** P2 · **Owner:** RPT (Risk Profile Tool) data owner · **Status:** Decision needed
- **Context:**
  - The real file (capture `image_67214593`) contains a row with `EAI Code = 0` and OSS Score 30.
  - It is not an application. Today it matches nothing and is silently ignored; lineage does not report it as a rejected or orphan row.
- **Question:** Should such rows be reported in lineage as orphan rows, or be fixed at the source?
- **Recommendation:** Report orphan rows (EAI not in the master) in lineage for every source, with a count. It is a small change in the assembler and lineage.
- **Acceptance criteria:** lineage shows orphan rows per source; test with EAI 0. **Effort:** S. **Risk if not done:** data quality problems stay invisible.

### SR-12 — Aggregation level: per application vs LOB/CIO

- **Priority:** P1 · **Owner:** business owner of the score · **Status:** Decision needed
- **Context:**
  - The engine scores each application.
  - Portfolio views average application scores per LOB, country, CIO, disposition and roadmap.
  - The dashboard shows aggregates like "Suma de Kenna_Score", which suggests some measures are aggregated before scoring.
- **Question:** Is the LOB/CIO score the average of application scores (implemented), or is it computed from aggregated raw values?
- **Recommendation:** Keep the average of application scores. It is explainable and consistent with the per-application breakdown.
- **Acceptance criteria:** confirmation; if different, a documented aggregation rule and a test. **Effort:** S–M. **Risk if not done:** LOB averages may differ from the dashboard.

### SR-13 — Obsolete scoring example (EAI 9768) in the BRD

- **Priority:** P3 · **Owner:** BRD author · **Status:** Open
- **Context:** Captures `image_67165185` and `image_67168769` show an older scoring example for EAI 9768 with a scheme that does not match v1.3. The platform ignores it and uses EAI 14381.
- **Work:** Mark the example as superseded in the BRD.
- **Acceptance criteria:** BRD updated. **Effort:** S. **Risk if not done:** new readers may use the wrong example.

### SR-14 — Meaning of the "Monitoring" column in MONITOREO

- **Priority:** P2 · **Owner:** observability owner · **Status:** Decision needed
- **Context:**
  - The file (capture `image_67214081`) has four boolean columns: Agent, APM, Monitoring and Synthetics.
  - In the sample, "Monitoring" is TRUE whenever any of the other three is TRUE.
  - The rule is "a single enabled capability (Elastic Agent, Synthetics or APM) is enough to score 100". The engine considers any of the four.
- **Question:** Is "Monitoring" a derived OR of the others, or an independent capability?
- **Recommendation:** If it is derived, keep it as it is (no score difference). If it is independent, confirm it counts toward 100.
- **Acceptance criteria:** confirmation. **Effort:** S. **Risk if not done:** low.

### SR-15 — MFA with unknown user count

- **Priority:** P2 · **Owner:** application security · **Status:** Decision needed
- **Context:**
  - MFA scoring depends on users: "No (with users)" → 20, "No (with no users)" → 100.
  - When the user count is unknown, the engine assumes "with users", the conservative security assumption. The screen says the count is unknown.
- **Question:** Is the conservative assumption right?
- **Acceptance criteria:** confirmation. **Effort:** S. **Risk if not done:** applications without user counts may be penalized.

---

## 2. Platform and architecture

### PA-01 — Deployment target and database engine · RFC-07

- **Priority:** P0 · **Owner:** platform/architecture · **Status:** Decision needed
- **Context:**
  - Manifests target Kubernetes (AKS assumed from BRD mentions): `deploy/k8s`, distroless non-root images (Azure Linux 3.0; Ubuntu chiseled until 2026-10-02), Pod Security Admission restricted, default-deny NetworkPolicy and service-mesh mTLS.
  - The database adapter is engine-agnostic and verified against SQLite with migrations. An unverified engine fails explicitly at startup.
- **Question:** AKS or other? SQL Server or PostgreSQL? Managed identity for the database?
- **Work after decision:**
  - Add the EF Core provider package (vulnerability-free) and a migration set for the engine.
  - Run the database contract suite against it.
  - Add a deployment migration job.
- **Acceptance criteria:** contract tests green against the chosen engine; the reference application's 80.70 read from it; migration job documented.
- **Effort:** M. **Risk if not done:** no production deployment.

### PA-02 — Authentication through Azure API Management with OAuth · ADR 0020

- **Priority:** P0 · **Owner:** platform + identity + security · **Status:** Decision needed
- **Context:**
  - Today Channel authenticates users with OIDC (Authorization Code + PKCE) and keeps a server-side session (BFF). The browser never holds tokens.
  - The organization will centralize API authentication in APIM with OAuth.
  - Phase 13 isolated everything provider-specific:
    - `IReauthenticationFlow` has development and OIDC implementations;
    - `StepUp` is the only reader of `auth_time` and the only writer of the `modscore_reauth_at` claim;
    - `ChannelAuthentication` is the only place that registers schemes.
  - Every reauthentication closes the previous session first.
- **Options** (from ADR 0020):
  - **A.** APIM in front of the BFF, OAuth to the IdP. The BFF keeps its own session. Minimal change: a new `IReauthenticationFlow`.
  - **B.** APIM validates a JWT issued by the IdP and forwards it to the BFF. The BFF stops doing the authorization code flow; it validates the JWT and issues its cookie. The SPA still has no tokens.
  - **C.** APIM between Channel and System, replacing the static service token. It closes H-16 and RFC-12 and is compatible with A or B.
- **Recommendation:** C plus A. Keep the BFF session model, which is the strongest option for a SPA, and use APIM for service-to-service authentication.
- **Work:**
  - Decide the option.
  - Obtain the APIM product and policies.
  - Implement the new reauthentication flow.
  - Validate `auth_time` or an equivalent freshness claim.
  - Replace `ServiceTokenAuthenticationHandler` validation (option C).
  - Update the threat model boundaries ② and ⑥.
- **Acceptance criteria:**
  - A reauthentication test against the real APIM/IdP in a test environment.
  - The service token removed from configuration.
  - ASVS 2.10.1 and 3.7.1 verified in that environment.
- **Dependencies:** PA-03, PA-05, SEC-19. **Effort:** L. **Risk if not done:** the platform cannot be exposed under the corporate authentication model.

### PA-03 — Portfolio scope claim · RFC-10

- **Priority:** P0 · **Owner:** identity · **Status:** Decision needed
- **Context:**
  - Authorization by LOB, country or CIO reads the claim `portfolio_scope` (name configurable) with values `*`, `lob:…`, `country:…` or `cio:…`.
  - Without the claim the user sees nothing.
  - The scope is enforced in the System layer, on the data ([ADR 0012](adr/0012-el-alcance-se-aplica-sobre-los-datos.md)).
- **Question:** Will the IdP or APIM emit that claim, or must it be derived from groups? Which groups map to which scopes?
- **Acceptance criteria:** documented mapping; a test user per scope type in the test environment; authorization tests against real tokens.
- **Effort:** S–M. **Risk if not done:** nobody sees data, or everybody sees everything if configured carelessly.

### PA-04 — Shared session cache · RFC-11

- **Priority:** P2 · **Owner:** platform · **Status:** Open
- **Context:**
  - Sessions live on the server, in an in-memory distributed cache ([ADR 0011](adr/0011-la-sesion-vive-en-el-servidor.md)).
  - Channel must run as one replica until a shared cache (Redis) is registered.
  - Rate limiting is also in memory, per replica.
- **Work:**
  - Provision Redis with TLS and authentication, and register `IDistributedCache`.
  - Persist Data Protection keys in a shared, protected location.
  - Decide whether rate limiting needs a distributed store.
- **Acceptance criteria:** two Channel replicas behind the ingress, sessions valid across both, logout effective on both.
- **Dependencies:** PA-01. **Effort:** M. **Risk if not done:** Channel cannot scale horizontally.

### PA-05 — Workload identity from Channel to System · RFC-12 / H-16

- **Priority:** P1 (P2 if option C of PA-02 is chosen, which covers it) · **Owner:** platform + security · **Status:** Open
- **Context:** Channel calls System with a static shared service token (rotation without downtime with primary and secondary tokens) plus mesh mTLS. Short-lived workload tokens depend on the cluster platform.
- **Options:** managed identity / workload identity federation; APIM (PA-02 C); mesh-issued identities with authorization policies.
- **Acceptance criteria:** no static secret between layers; token lifetime ≤ 1 hour; rotation test.
- **Effort:** M. **Risk if not done:** a leaked token allows calling System directly until rotated (mitigated by NetworkPolicy and mTLS).

### PA-06 — Horizontal scaling constraints

- **Priority:** P2 · **Owner:** platform · **Status:** Open
- **Context (what limits scale today):**
  - Manifests use `replicas: 1`.
  - The File adapter needs a shared volume. RWO PVCs only allow one node; publications rely on an atomic directory move on the same volume.
  - Rules refresh every 15 s per replica (`Rules:RefreshSeconds`).
  - The warm-up service computes history per replica.
  - Sessions and rate limiting are in memory (PA-04).
- **Work:**
  - Choose the Database adapter for multiple replicas (recommended).
  - Or use an RWX volume for File.
  - Load test with two System replicas and two Channel replicas.
- **Acceptance criteria:** documented topology; load test with two replicas of each layer; a publication seen by all replicas within the refresh window.
- **Effort:** M. **Risk if not done:** single points of failure.

### PA-07 — Secrets manager integration verified

- **Priority:** P1 · **Owner:** platform · **Status:** Open
- **Context:**
  - Both layers read secrets as key-per-file from `Secrets:Directory`, the Key Vault CSI driver model.
  - Deployment guards stop the process if development tokens reach a non-development environment.
  - Never verified against a real vault (ASVS 6.4.1 partial).
- **Acceptance criteria:** secrets mounted from the vault in a test environment; rotation runbook executed once.
- **Effort:** S–M. **Risk if not done:** secrets end up in environment variables or configuration.

### PA-08 — Observability and SIEM rules

- **Priority:** P1 · **Owner:** operations + security · **Status:** Open
- **Context:** Security events have stable EventIds in the `Modernization.Audit` category. Operational events use the same approach.

  | EventId | Meaning |
  |---|---|
  | 4001–4004 | Session started, ended, expired, step-up completed |
  | 4101–4104 | Access denied, CSRF rejected, rate limited, step-up required |
  | 4201–4203 | Upload published, upload rejected, scope denied in System |
  | 4211–4213 | Rules published, rules rejected, data exported |
  | 5301–5303 | Rules activated, unreadable rules version, rules store unavailable |
  | 5401–5402 | Portfolio warm-up done (debug), warm-up failed |

- **Work:**
  - Route the audit category to an immutable destination (H-17 / ASVS 7.3.3).
  - Create SIEM alerts, for example repeated 4101/4102/4103 per user, any 4211, 4213 bursts and 5303.
  - Wire OpenTelemetry exporters.
- **Acceptance criteria:** alerts fire in a test environment for each rule. **Effort:** M. **Risk if not done:** incidents go unnoticed.

### PA-09 — Notifications to responsible parties when rules are published

- **Priority:** P2 · **Owner:** product + platform · **Status:** Decision needed
- **Context:**
  - Phase 13 shows an **in-app notice** to every reader for 14 days after a rules version is published. It gives the version, date, number of changes and reason; administrators get a link to the comparison. The notice is dismissible per browser.
  - There is no email, Teams or other outbound notification.
- **Question:** Who must be notified (CIOs, AD directors, application owners)? Through which channel? With what content?
- **Security considerations:**
  - Outbound integrations need their own identity and secret.
  - Messages must not include scores of applications outside the recipient's scope.
  - Recipients come from a trusted directory, not from rule text.
- **Options:** Microsoft Graph (email/Teams) from System on event 4211; an event bus consumed by a notification service; SIEM/automation platform.
- **Acceptance criteria:** the chosen recipients receive a notification within N minutes of a publication, with a link to the comparison, respecting scope.
- **Effort:** M. **Risk if not done:** people learn about rule changes only when they open the platform.

---

## 3. Security

Current ASVS 4.0.3 Level 2 status ([docs/security/asvs-l2.md](security/asvs-l2.md)): **103 pass · 14 partial · 3 pending · 10 identity-provider or not applicable.** Findings are in [docs/security/hallazgos.md](security/hallazgos.md). By decision of 2026-09-14, the ASVS partial items are planned for phase 14, and the infrastructure findings **H-16, H-17, H-20 and H-21 go to the last phase** of the plan.

### SEC-01 … SEC-14 — ASVS L2 partial requirements (phase 14)

All are **P1**, owner **security + platform**, status **Open (phase 14)**. "Partial" means that the control is implemented in code or declared in infrastructure but can only be verified in a real deployment.

| ID | ASVS | Requirement | Current state | What is missing | Verification | Effort |
|---|---|---|---|---|---|---|
| SEC-01 | 1.6.2 | Cryptographic keys protected | Data Protection keys on their own volume (`DataProtection:KeysDirectory`) | Encryption at rest of that volume; key protection with the vault (`ProtectKeysWithAzureKeyVault` or equivalent) | Inspect the volume and key ring in the test environment | S |
| SEC-02 | 1.9.2 | Encrypted communication between components | Linkerd mTLS declared in `deploy/k8s/namespace.yaml` | Verification in a real cluster | `linkerd viz edges` or mesh dashboard shows mTLS for Channel → System | S |
| SEC-03 | 2.10.1 | Integration secrets are not static | Shared token with seamless rotation and mTLS; since phase 15 the credential is pluggable and the System layer validates a short-lived JWT, with a `Both` mode for the cutover | The issuer: APIM or the IdP. Closing it means `ServiceToken:Mode=Jwt` and removing the shared secret | No static secret in configuration; `JwtServiceTokenValidatorTests` already covers the validation | M |
| SEC-04 | 4.3.1 | MFA on administrative interfaces | Uploads need `ingestion.write`; rules need `ruleset.admin` + step-up within 10 minutes | MFA enforcement for both roles is an IdP/APIM conditional access policy | Sign-in with each role requires MFA | S |
| SEC-05 | 6.4.1 | Secrets manager | CSI key-per-file mount declared | Verification against the real vault (PA-07) | Secrets read from the vault; none in env vars | S |
| SEC-06 | 7.1.3 | Security events logged | **Done (2026-09-15).** Every `/api` response that rejects the input (400, 413, 415, 422) logs event **4214** with the path and the status code, from a middleware in the Channel layer, so no endpoint can forget. The values that were sent are never logged | — | `ValidationAuditTests` | S |
| SEC-07 | 8.3.4 | Retention policy for sensitive data | **Done (2026-09-15).** `Uploads:RetainVersions` keeps the N most recent uploads in the file adapter too; 0 keeps all. It only deletes directories with a manifest, so the rules store and the staging area are safe | How many to keep is a business decision (OP-03) | `FileUploadRetentionTests` | S |
| SEC-08 | 9.1.2 | Only strong cipher suites | Ingress configuration | Verification in the real ingress | TLS scan (for example `testssl.sh`) | S |
| SEC-09 | 9.1.3 | Only TLS 1.2 or higher | Ingress configuration | Same | Same | S |
| SEC-10 | 9.2.2 | Encryption on internal connections | Mesh policy `all-authenticated` | Verification in cluster; database and Redis with TLS | Connection strings require TLS; mesh verification | S |
| SEC-11 | 11.1.8 | Configurable alerts | Stable EventIds | SIEM rules (PA-08) | Alerts fire in test | M |
| SEC-12 | 13.2.2 | JSON schema validation | **Decided (2026-09-15).** Strict typed binding stays: `BoundedJson` (128 KB, no unknown members, nullable annotations and required parameters respected, depth 16) plus the `Ruleset.Validate` invariants. Adding a schema library would mean one more dependency and two sources of truth | — | [ADR 0021](adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md) | S |
| SEC-13 | 14.1.5 | Configuration integrity | Checkov in CI | Image signing and admission verification (H-17) | Signed images, admission policy rejects unsigned | M |
| SEC-14 | 14.2.6 | Isolation of third-party components | **Accepted (2026-09-15)** as a design limit: neither .NET nor Node sandboxes a single library. Compensating controls: minimal locked dependency tree, `npm ci --ignore-scripts`, NuGetAudit, `npm audit`, OSV and Trivy on every push, chiseled images with no shell, non-root, read-only filesystem and dropped capabilities, NetworkPolicy, and a strict CSP with no third-party resources | — | `asvs-l2.md` 14.2.6 | S |

### SEC-15 — ASVS 3.3.4: users can see and close their active sessions · H-20

- **Priority:** P3 · **Owner:** development · **Status:** Deferred to the last phase (decision of 2026-09-14)
- **Context:** Sessions are server-side tickets, so listing and revoking them is feasible. There is no screen for it. The risk is low: absolute session lifetime is 12 hours, idle lifetime 30 minutes, and logout removes the ticket.
- **Work:** index tickets per subject in the cache; add a "Sessions" panel; revoke endpoint with CSRF.
- **Acceptance criteria:** a user sees their sessions with creation time and can revoke others; test.
- **Effort:** M.

### SEC-16 — ASVS 7.3.3: logs protected against tampering · H-17

- **Priority:** P2 · **Owner:** operations · **Status:** Deferred to the last phase
- **Context:** The audit category is separate and ready to be routed; the immutable destination (WORM storage or SIEM) is infrastructure.
- **Acceptance criteria:** audit events stored in immutable storage with retention; tamper test documented.
- **Effort:** M.

### SEC-17 — ASVS 12.4.2: antivirus on uploads · H-21

- **Priority:** P3 · **Owner:** development + platform · **Status:** Deferred to the last phase
- **Context:**
  - Uploads are CSV, parsed as text, never served back or executed.
  - Size limits: 128 MB per file (64 MB until 2026-10-05, ADR 0030), 256 MB total, 500,000 rows per file.
  - Files are recognized by header and written under canonical names ([ADR 0010](adr/0010-la-carga-de-archivos-como-superficie-de-ataque.md), [ADR 0015](adr/0015-el-archivo-se-reconoce-por-su-encabezado.md)).
  - Risk is low.
- **Work:** scan in staging before publishing (for example with a cluster antivirus service or Defender for Storage).
- **Acceptance criteria:** an EICAR test file is rejected with a notice; audit event.
- **Effort:** M.

### SEC-18 — H-16: static shared service token

- **Priority:** P2 · **Owner:** platform · **Status:** Deferred to the last phase (partial mitigation in place)
- **Context:** See PA-05. Mitigations: rotation without downtime, mTLS, NetworkPolicy default-deny, System not reachable from the ingress.
- **Acceptance criteria:** as PA-05. **Effort:** M.

### SEC-19 — Step-up reauthentication against a real identity provider not verified

- **Priority:** P0 · **Owner:** identity + development · **Status:** Open
- **Context:**
  - Publishing rules requires an authentication within the last 10 minutes.
  - With OIDC, Channel requests `max_age=0` and `prompt=login`. It only stamps the reauthentication if the returned token has an `auth_time` less than 5 minutes old; any claim with the stamp's name coming from the IdP is discarded.
  - Phase 13 closes the previous session before redirecting to the IdP; the orphaned-session gap is fixed.
  - Locally only the development scheme runs, so the OIDC path is covered by unit tests (`ReauthenticationFlowTests`) but not by a real IdP.
  - With APIM (PA-02) the flow will change.
- **Acceptance criteria:**
  - In a test environment, publishing rules redirects to the IdP, forces credentials and returns to `/admin/rules` with the draft intact.
  - A token without a fresh `auth_time` does not grant publication.
  - The previous session ticket is gone.
- **Dependencies:** PA-02. **Effort:** S (test) to M (APIM flow).
- **Risk if not done:** a misconfigured IdP could ignore `max_age`. The `auth_time` check is the defense, but it is unverified.

### SEC-20 — Security gates never run outside CI

- **Priority:** P0 · **Owner:** DevSecOps · **Status:** In progress. CI has been green since run #5 (2026-09-15); ZAP is still pending
- **Context:**
  - `.github/workflows/ci.yml` runs tests, vulnerable packages, CodeQL, Trivy, Checkov and SBOM (CycloneDX). `seguridad.yml` runs Gitleaks and OSV-Scanner on every push. Actions are pinned by SHA. DAST with OWASP ZAP is planned for staging and is not in the workflows yet.
  - `main` was first pushed on 2026-09-15, which triggered the workflow. First run (commit `d4870d9`), as seen in the Actions tab:
    - passed: Backend .NET, secret scanning (Gitleaks) and OSV-Scanner;
    - failed: SPA, on ESLint (a literal BOM in `e2e/support.ts`, fixed in `3b038b5`);
    - failed: both CodeQL jobs. Probable cause: a private repository without GitHub Code Security has no code scanning, so uploading the SARIF fails. The workflow now runs the analysis with `upload: never`, keeps the SARIF as an artifact and fails on high-severity results (level `error` or `security-severity` ≥ 7);
    - skipped: images and infrastructure, because the job depends on the SPA job.
  - Second run (commit `8e4639c`):
    - passed: Backend .NET, SPA, secret scanning and OSV-Scanner.
    - Images and infrastructure ran for the first time. Both images built and both Trivy steps passed, but Checkov failed on CKV_DOCKER_2. That run still had the old Dockerfiles; the fix in `a68d2bc` reached CI later.
    - Both CodeQL jobs analyzed every file (145 C#; 35 TypeScript, 3 JavaScript, 2 HTML, 1 Actions workflow) and saved the SARIF. `analyze` still failed with "Resource not accessible by integration" on the "get a workflow run" API: in a private repository the action reads the workflow run even when it does not upload. Fixed by adding `actions: read` to the job permissions.
  - Locally we ran tests, ESLint, `dotnet list package --vulnerable`, `npm audit` and a locked-mode restore.
  - **Local Docker verification (2026-09-15)**, with the same tool versions as CI:
    - Gitleaks v8.30.1 over the full history: no leaks (4 commits).
    - Both images build: `system-api` 243 MB, `channel-api` 197 MB, non-root user.
    - Trivy: 0 fixable HIGH/CRITICAL vulnerabilities in either image (Ubuntu 24.04 chiseled, .NET 10.0.12, application dependencies).
    - **2026-10-02:** the Ubuntu chiseled base brought openssl 3.0.13-0ubuntu3.15 with CVE-2026-84782 (HIGH, fixed in 3.0.13-0ubuntu3.16) and Microsoft had not republished the image (still the 2026-09-04 build). Both runtimes moved to `aspnet:10.0-azurelinux3.0-distroless`: same posture (no shell, no package manager, user 1654, invariant globalization), same .NET 10.0.12, openssl 3.3.7. Trivy 0.74.0 locally: 0 vulnerabilities of any severity in both images; Checkov clean; both start read-only and answer `/health`.
    - Checkov 3.3.17 first failed 4 checks, so the CI job would have failed:
      - CKV_K8S_40 on both Deployments: UID 1654 is below 10000. Fixed: `runAsUser`, `runAsGroup` and `fsGroup` are now 10001. Both images ran as UID 10001 with a read-only root filesystem, and `/health` and the SPA answered.
      - CKV_DOCKER_2 on both Dockerfiles: no `HEALTHCHECK`. Suppressed inline with a reason: chiseled images have no shell or curl, and Kubernetes uses its `/health` probes instead.
      - After the fixes: Kubernetes 188 passed, 0 failed; Dockerfile 128 passed, 0 failed, 2 skipped.
    - `aquasecurity/trivy-action` v0.36.0 (pinned SHA `ed142fd`, not affected by the March 2026 compromise) installs **Trivy v0.70.0** by default. That version is affected by GHSA-mcj4-mphf-j9ff (High), GHSA-q3fv-x8vg-qqm4 and GHSA-8rc5-4fr6-64pw, all fixed by v0.72.0. The workflow now pins `version: v0.74.0`, the same version used locally.
    - `.dockerignore` now excludes `artifacts/`, `*.zip`, test results and `*.tsbuildinfo`.
  - Run #5 (commit `55cf36f`): **all 7 jobs green** in 9 min 54 s, with 3 artifacts (backend SBOM and the two CodeQL SARIF files).
    - CodeQL: no high-severity results in either language.
    - Trivy v0.74.0: no fixable HIGH/CRITICAL vulnerabilities.
    - Checkov: green.
    - Durations: CodeQL C# 8 min 41 s, CodeQL JS/TS 4 min 38 s, Backend 2 min 58 s, images and infrastructure 2 min 45 s, SPA 22 s, OSV 23 s, secrets 14 s.
- **Work:**
  - Run OWASP ZAP against a staging deployment (needs PA-01).
  - Done: CI speed-up of 2026-09-15. Run #6 (`148c6ce`) went from 9 min 54 s to **7 min 3 s**, all green.
    - Job times: CodeQL C# 6 min 58 s (was 8 min 41 s), CodeQL JS/TS 4 min 25 s, Backend 2 min 40 s (was 2 min 58 s), images 2 min 43 s (now in parallel), SPA 24 s.
    - CodeQL C# still sets the total. Most of it is probably CodeQL itself (setup and queries), since the build takes 13 s locally.
    - Changes, none of which removes a control:
    - Gitleaks and OSV moved to `seguridad.yml`, which runs on every push without path filters.
    - `ci.yml` skips docs-only changes (`**/*.md`, `docs/`, `tools/sesiones/`, `artifacts/`, `data/history/`, local databases, `.claude/`).
    - CodeQL C# builds only the deployed code: `ChannelApi` pulls in the ten `src/` projects. Tests and tools are left out, and so are the Roslyn analyzers, which the Backend job already enforces. Source generators still run.
    - NuGet cache in `setup-dotnet`, keyed on the lock files.
    - The images job no longer waits for Backend and SPA.
  - Fix whatever the scanners find.
  - Build and scan both images.
  - Run ZAP against a staging deployment.
- **Acceptance criteria:** pipeline green with all gates; SBOM archived; images scanned with zero High/Critical; ZAP baseline without High findings.
- **Effort:** M. **Risk if not done:** unknown vulnerabilities in images, secrets or infrastructure code.

### SEC-21 — Repository visibility, BRD captures and commit metadata

- **Priority:** P0 · **Owner:** repository owner · **Status:** Decided (2026-09-15)
- **Context:**
  - The GitHub repository `camilarodriguez10/modscore1` holds the code, the scoring model and the security architecture.
  - The 34 BRD and dashboard captures (about 121 MB) are **excluded by `.gitignore`** and blocked by the backup scripts, because they are internal business documentation. They live in `docs/brd/capturas/` locally, and `docs/brd/README.md` indexes them without their content.
  - Commits are authored with the repository-local identity; the email becomes public in commit metadata if the repository is public.
- **Decisions (2026-09-15):**
  - The repository is **private**.
  - The captures and every image stay **out** of the repository; corporate storage keeps them.
  - Commits keep the account email, not the GitHub no-reply address.
  - Everything else created during the project **is versioned**:
    - the local run state (`data/history/`, `data/modernization.db*`, dummy data only);
    - the helper scripts of the working sessions (`tools/sesiones/`);
    - a copy of the published plan (`docs/plan/`);
    - `.claude/launch.json`.
  - Build outputs, test results and secrets stay out.
- **Acceptance criteria:** decisions recorded in [docs/runbooks/respaldo-en-git.md](runbooks/respaldo-en-git.md) (done); repository settings applied (OP-04).
- **Effort:** S.

### SEC-22 — The portable package uses development authentication

- **Closed on 2026-09-27:** the portable package and `tools/portable` were removed. The same rule still applies to running from the code: the development scheme only starts in `Development`, and the commands bind to `localhost`. The text below is kept for traceability.
- **Priority:** P1 · **Owner:** everyone who distributes the package · **Status:** Closed (was: accepted risk with rules)
- **Context:**
  - `tools/portable` builds a self-contained package to test the application on another Windows machine before publishing.
  - It runs in the Development environment with the development authentication scheme: anyone who reaches the port is an administrator.
  - Mitigations:
    - both layers listen only on `127.0.0.1`;
    - a random service token is generated per run and passed as a file in a temporary folder that is deleted on exit;
    - same strict CSP;
    - no source maps and no debug symbols;
    - dummy data by default.
  - Binaries are not signed, so SmartScreen may warn.
- **Rules:**
  - Never with real data.
  - Never exposed on a network, and never behind a reverse proxy.
  - Share the SHA-256 printed by the build with the package.
- **Acceptance criteria:** rules acknowledged by the team. **Effort:** none.

### SEC-23 — The rules timeline shows the publication reason to every reader

- **Priority:** P3 · **Owner:** rules administrators · **Status:** Accepted risk
- **Context:** `GET /api/rules/timeline` is available to every reader, to mark the trend and show the in-app notice. It returns version, date, based-on version, number of changes and the free-text **reason**. It does **not** return who published (a test asserts it).
- **Guidance:** reasons must not contain names, application scores or confidential context. The screen says the reason is stored and audited.
- **Effort:** none.

### SEC-24 — CSV exports and data exfiltration

- **Priority:** P1 · **Owner:** security + data owners · **Status:** Decision needed
- **Context (phase 13):**
  - **Endpoints:**
    - Portfolio (`/api/portfolio/export.csv`) and roadmap (`/api/roadmap/export.csv`) follow the active filters, inside the user's scope.
    - Rules comparison (`/api/admin/rules/compare/export.csv`) is administrators only.
  - **Controls:**
    - Its own rate limit: 10 per minute per user.
    - Audit event 4213 with user, export kind and row count.
    - CSV formula-injection guard (tested).
    - Row cap of 50,000.
    - Attachment disposition, `no-store` and `nosniff`.
  - Scores and SCA-derived risk per application are sensitive business information.
- **Questions:**
  - Should exports carry a classification label or watermark?
  - Must some roles be denied export?
  - Is 10 per minute right?
  - Should exports of the full portfolio notify security?
- **Acceptance criteria:** decisions recorded; configuration adjusted; SIEM rule on 4213 (PA-08).
- **Effort:** S.

---

## 4. Technical debt and quality

### TD-01 — Outdated packages kept on purpose

- **Priority:** P3 · **Owner:** development · **Status:** Accepted (policy of 2026-09-14: keep outdated packages that have no known vulnerabilities)
- **Context:** Zero known vulnerabilities (`dotnet list package --vulnerable --include-transitive` and `npm audit`). Outdated at the time of the review:

  | Package | In use | Available |
  |---|---|---|
  | Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.3 | 10.0.12 |
  | Microsoft.AspNetCore.OpenApi | 10.0.3 | 10.0.12 |
  | Microsoft.AspNetCore.Mvc.Testing | 10.0.3 | 10.0.12 |
  | Microsoft.Extensions.Http.Resilience | 9.10 | 10.10 |
  | Microsoft.NET.Test.Sdk | 17.x | 18.x |
  | coverlet.collector | 6.x | 10.x |
  | xunit.runner.visualstudio | 3.x | 4.x |
  | eslint (dev only) | 9.39.5 | 10.11.0 |

- **ESLint 9 is end of life (seen on 2026-09-21).** `npm ci` prints
  `npm warn deprecated eslint@9.39.5: This version is no longer supported`. It is a warning,
  not a vulnerability (`npm audit` is clean), and ESLint only runs in development and CI: it is
  not part of the site the Channel serves. The difference with the rows above is that an
  unsupported major will not get a patch if a vulnerability appears. Moving to ESLint 10 is
  feasible today —`typescript-eslint` 8.70 and `eslint-plugin-react-hooks` 7 accept it, and
  `@eslint/js` 10 exists— and would be a dedicated change with `npm run lint` green before and
  after.
- **Work:**
  - Renovate is configured (`renovate.json`) to auto-merge patches with a green suite once the repository is on GitHub.
  - Review major versions (Test SDK 18, coverlet 10, xUnit runner 4) in a dedicated change.
  - An update becomes mandatory the day a vulnerability is published.
- **Acceptance criteria:** Renovate running; gates block vulnerable versions. **Effort:** S.

### TD-02 — React 19 features promised in the plan but not used

- **Priority:** P3 · **Owner:** development · **Status:** Decision needed
- **Context:**
  - The plan (§6.1) promised Actions with `useActionState` for forms, `use()` with Suspense for streaming, and the React Compiler.
  - None is used.
  - The SPA does use React 19 with TanStack Query, and since phase 13 lazy loading with `Suspense` for administration screens.
  - The plan was corrected to describe the real stack.
- **Options:** adopt the React Compiler (a build plugin: evaluate its supply chain and vulnerabilities first); keep TanStack Query mutations instead of Actions; drop the promise.
- **Recommendation:** Drop the promise. Revisit the compiler if profiling shows re-render cost.
- **Effort:** S (decision) to M (compiler). **Risk if not done:** none.

### TD-03 — OpenAPI-generated client replaced by a contract test

- **Priority:** P3 · **Owner:** development · **Status:** Decision needed
- **Context:**
  - The plan promised a client generated from the Channel OpenAPI.
  - TypeScript types are maintained by hand in `src/web/src/api/types.ts`.
  - Since phase 13, `ViewModelContractTests` fails if any Channel view model or shared contract and its TypeScript interface disagree in either direction.
  - OpenAPI is only published in Development.
- **Recommendation:** Keep the contract test. Generation would add a tool to the supply chain for the same guarantee.
- **Effort:** none. **Risk if not done:** none while the test exists.

### TD-04 — Visual QA and accessibility audit of the new screens

- **Priority:** P1 · **Owner:** UX + development · **Status:** Automated part done (2026-09-15); the manual walkthrough is still open
- **Context:**
  - Screens are **Rules** (phase 12), **Compare rules**, **trend markers**, the **rules notice** and **Download CSV** buttons (phase 13).
  - Functional behavior is covered by end-to-end tests.
- **Done (2026-09-15):** `@axe-core/playwright` 4.13.0 (no vulnerabilities) scans five screens in light and dark inside the e2e suite and in CI, failing on serious or critical violations. It found two real problems, both fixed:
  - muted text at 4.42:1 on white and 3.89:1 on the page background, below the 4.5:1 minimum. `--app-ink-3` is now `#5f6c73` in light and `#8b989e` in dark;
  - status colours used as text (down to 1.65:1). They stay as marks and text now uses `--app-*-text` variants that meet AA on both the surface and their own wash;
  - three decorative bars carried `aria-label` on a `div` with no role: they declare `role="img"`.
- **Work left:** keyboard-only walkthrough, screen reader pass and review at 1280, 1024 and 768 px. A person has to do those.
- **Acceptance criteria:** zero serious or critical axe violations (met, and guarded by CI); walkthrough notes resolved. **Effort:** S left.

### TD-05 — End-to-end tests in CI

- **Priority:** P1 · **Owner:** DevSecOps · **Status:** Done (2026-09-15)
- **Context:**
  - Sixteen Playwright scenarios (6 flows and 10 accessibility checks) run locally with the installed Microsoft Edge (`channel: 'msedge'`), so no browser is downloaded.
  - They start System, Channel and Vite, or reuse running ones; they can also target an already running site with `MODSCORE_E2E_BASE_URL`.
  - Run time is about 45 s after builds.
- **Done:** the `SPA de punta a punta` job in `ci.yml` installs .NET 10, Node 24 and Playwright's Chromium (`MODSCORE_E2E_CHANNEL=chromium`), builds both layers, runs the suite and uploads traces when it fails.
- **Work left:** make the job a required status check when branch protection is configured (OP-04).
- **Acceptance criteria:** e2e job required on pull requests. **Effort:** S left.

### TD-06 — Simulation latency under concurrency

- **Priority:** P1 · **Owner:** development · **Status:** Open
- **Context:**
  - Measured with Release builds, 30 uploads, 200+ applications, 8 concurrent clients and 10 s per scenario. On an idle machine, rules simulation p50 went from **117 ms to 47 ms** (p95 66 ms) and throughput from 66 to 168 requests/s after phase 13: the active side is cached and the draft side runs in parallel.
  - A run taken while the machine was busy writing files showed p50 204 ms. The same code measured idle gave 47 ms: latency is sensitive to CPU contention.
  - Parallel scoring inside requests competes with other requests for thread-pool threads.
  - Simulation is rate-limited to 10 per minute per user, so real concurrency is low.
- **Work:** measure on target hardware with realistic concurrency; if needed, bound `MaxDegreeOfParallelism` in `RulesImpactSimulator.ScoreAll` and in the plan summary.
- **Acceptance criteria:** p95 under 500 ms with 8 concurrent simulations on target hardware. **Effort:** S.
- **Resolution (2026-09-16) — measured, and the proposed fix was discarded:**
  - The target hardware does not exist yet (PA-01), so the System layer was pinned to **2 cores** with `DOTNET_PROCESSOR_COUNT=2` and processor affinity, the shape of a small pod. The load tool ran on the other cores so it did not measure itself. Release build, generated portfolio of 200 applications, 20 s per scenario, single runs.
  - Two measurements each: 8 concurrent simulations alone, and 8 concurrent score reads **while** 8 simulations run — the contention this item was worried about.

    | System layer | Simulations alone p50 / p95 / p99 | Reads during simulations p95 / p99 |
    |---|---|---|
    | 16 cores, current code | 56 / 107 / 134 ms | 1.6 / 13.2 ms |
    | 2 cores, current code | 156 / **329** / 490 ms | 1.1 / 14.5 ms |
    | 2 cores, parallelism bounded to 1 | 161 / 432 / 609 ms | 1.2 / 16.8 ms |
    | 16 cores, bounded to 4 | 55 / 102 / 118 ms | 2.4 / 13.3 ms |

  - **The criterion is met without changing code:** p95 329 ms on 2 cores. Reads stay near 1 ms at p95 in every case, so parallel scoring does not starve other requests.
  - **Bounding the parallelism did not help** — on 2 cores it made simulations slower and left reads unchanged — so it was reverted rather than kept as an unproven "defensive" change.
  - `tools/Modernization.LoadTest` now accepts `--scenario` to run one scenario, and `medir-en-dos-nucleos.ps1` reproduces the whole measurement. **Run it again on the real pod once PA-01 is decided**; that is the only open part.

### TD-07 — History performance with the database adapter

- **Priority:** P2 · **Owner:** development · **Status:** Open
- **Context:**
  - With the File adapter and 30 uploads, the first portfolio history request went from **1,655 ms to 584 ms** with parallel per-upload computation, and to **8 ms** with the background warm-up.
  - Not measured with the Database adapter and many versions (per-version queries).
- **Acceptance criteria:** load test with 30+ versions on the chosen engine (PA-01). **Effort:** S.

### TD-08 — Load test coverage

- **Priority:** P2 · **Owner:** development · **Status:** Open
- **Context:**
  - `tools/Modernization.LoadTest` covers System scores, a single score, what-if, portfolio history, rules listing and rules simulation.
  - It reports first-request latency.
  - Channel scenarios run only when Channel is started with high rate limits.
  - No scenarios for exports or comparison.
- **Work:** add Channel export and comparison scenarios; document the rate-limit settings.
- **Effort:** S.
- **Resolution (2026-09-16):**
  - New Channel scenarios: the six portfolio questions (`/api/insights`), both CSV downloads (portfolio, and roadmap by country), and the rules comparison with its CSV.
  - `--scenario <text>` runs only the matching scenarios.
  - Rate limits documented in the tool header. **100,000 requests per minute is not enough:** a cached read serves tens of thousands of requests per second, empties the window in the first scenario, and every later scenario measures 429s. The first run of this item made exactly that mistake. The documented values are 100,000,000 requests, 10,000,000 exports and 10,000,000 rules simulations per minute, and the 429 column must read zero for a result to count.
  - First measurement (Release, generated portfolio of 200 applications, 16 cores, 8 concurrent users, 10 s per scenario, single run; 0 errors and 0 429 in every scenario):

    | Channel scenario | req/s | p50 | p95 | p99 |
    |---|---:|---:|---:|---:|
    | `GET /api/portfolio` (page of 25) | 93 | 88 ms | 119 ms | 154 ms |
    | `GET /api/applications/{eai}/explain` | 9,702 | 0.8 ms | 1.1 ms | 1.7 ms |
    | `GET /api/ingestion` | 11,530 | 0.7 ms | 0.9 ms | 1.5 ms |
    | `GET /api/insights` | 123 | 64 ms | 89 ms | 98 ms |
    | `GET /api/portfolio/export.csv` | 132 | 59 ms | 83 ms | 97 ms |
    | `GET /api/roadmap/export.csv` by country | 171 | 45 ms | 67 ms | 78 ms |
    | `GET /api/admin/rules/compare` | 160 | 49 ms | 69 ms | 81 ms |
    | `GET /api/admin/rules/compare/export.csv` | 158 | 50 ms | 70 ms | 84 ms |

  - **Observation, not an item yet:** the portfolio page is the slowest read, slower than the six questions or a full export, although it returns 25 rows. It is still well within an interactive budget; worth a look if the real portfolio is much larger than 200 applications.

### TD-09 — Language sweep does not cover the new endpoints

- **Priority:** P1 · **Owner:** development · **Status:** Open
- **Context:** Phase 11's `LanguageContractTests` crawls Channel responses and fails on Spanish text. The comparison, timeline and simulation-with-application responses were added later and are not in the crawl. The CSV headers are English, but the rule notes embedded in comparisons come from the ruleset (English).
- **Work:** add the new endpoints and the CSV bodies to the sweep.
- **Acceptance criteria:** sweep covers every Channel GET endpoint. **Effort:** S.
- **Resolution (2026-09-16):**
  - The sweep now covers **every Channel endpoint**: rules administration, a published version, the comparison (whole portfolio and one application), the timeline, the six portfolio questions, the draft simulation (whole portfolio and one application), step-up with a foreign destination and logout — plus the error paths of each.
  - **CSV bodies** are swept too: the portfolio export (with and without filters), the roadmap export for all seven groupings, and the rules comparison export. Columns written by the person who uploaded or published (label, reason) are skipped, exactly as the JSON sweep skips those properties.
  - The happy paths must answer 200 and the CSVs `text/csv`: a sweep that received 403 on everything would pass without reading anything.
  - Two tests check the CSV collector itself — it finds Spanish in a header and in a quoted field, and ignores a label column — so the sweep cannot silently turn into a no-op.
  - Result: no Spanish text anywhere.

### TD-10 — CSV of a draft's impact

- **Priority:** P3 · **Owner:** development · **Status:** Open
- **Context:** The comparison of two **published** versions downloads a CSV with every application. The impact of an **unpublished draft** only shows the ten biggest movers on screen; exporting it would need a POST download.
- **Effort:** S.

### TD-11 — Mixed line endings in the repository

- **Priority:** P3 · **Owner:** development · **Status:** Decision needed
- **Context:**
  - 189 files use LF and 70 CRLF, since before versioning.
  - `.gitattributes` only fixes endings for `.sh` (LF) and `.ps1`/`.cmd` (CRLF) and marks binaries.
  - Normalizing everything would be a large diff with no content change.
- **Options:** normalize once in a dedicated commit (`* text=auto` + `git add --renormalize .`), or leave as is.
- **Effort:** S.

### TD-12 — Lock files and runtime-specific publish

- **Priority:** P3 · **Owner:** development · **Status:** Moot since 2026-09-27: the portable build that published with `-r win-x64` was removed
- **Context:**
  - Projects restore with lock files (`packages.lock.json`), and CI uses locked mode (`dotnet restore --locked-mode`).
  - Publishing the portable package with `-r win-x64` adds `Microsoft.NET.ILLink.Tasks` and a `net10.0/win-x64` section to 10 lock files. Seen on the second build of 2026-09-15.
  - Committing those changes would make the locked-mode restore in CI fail, because a normal restore does not request them.
- **Mitigation (2026-09-15):** `tools/portable/build-portable.ps1` saves every `src/*/*/packages.lock.json` before publishing and writes them back afterwards, even if the publish fails.
- **Work:** after package or SDK updates, or when adding `win-arm64`, confirm that `git status` shows no lock file changes after a portable build.
- **Effort:** S.

### TD-13 — Security review of phases 09–13 by a second person

- **Priority:** P1 · **Owner:** security · **Status:** Open
- **Context:**
  - The formal security review with findings H-01…H-23 was phase 08.
  - Phases 09–13 added:
    - the database adapter;
    - header-based file recognition;
    - English notices;
    - rules administration with step-up;
    - comparison;
    - exports;
    - the timeline.
  - Each phase added controls, tests and threat-model updates. The review of 2026-09-14 recorded new findings (see `hallazgos.md`, section "Revisión de las fases 09 a 13"), but no second person has reviewed them.
- **Acceptance criteria:** a peer review or pentest of boundaries ②, ⑤ and ⑥; findings recorded. **Effort:** M.

### TD-14 — Size of the rules editor screen

- **Priority:** P3 · **Owner:** development · **Status:** Open
- **Context:** `src/web/src/routes/RulesPage.tsx` still has about 1,270 lines after extracting the pure draft logic to `src/rules/draft.ts`, which has unit tests.
- **Work:** split tabs into files.
- **Effort:** S.

### TD-15 — Warm-up service and caches with many versions

- **Priority:** P3 · **Owner:** development · **Status:** Open
- **Context:**
  - History points are cached per rules version and upload, up to 256 entries.
  - Models of old rules versions are cached up to 16.
  - The warm-up service recomputes every 15 s if something is cold (`Portfolio:WarmupSeconds`, 0 disables it).
  - Memory with hundreds of uploads and frequent rule changes is not measured.
- **Acceptance criteria:** memory profile with 200 uploads and 10 rules versions. **Effort:** S.

### TD-16 — Column mapping of any upload in the lineage, and a first-time review

- **Priority:** P3 · **Owner:** development · **Status:** Open
- **Context (2026-09-28, ADR 0027):**
  - Every upload records which column loaded each field (manifest `ColumnMaps`; `snapshot_version.ColumnMaps` in the database). *Data → Mapping* shows the **current** upload only; the lineage of an older upload does not show its mapping yet.
  - Uploads before 2026-09-28, including the four `data/timeline` quarters, have no recorded mapping. The tab says so.
  - A mapping is prepared with the files at hand (only their header is read). There is no way to prepare one for a source without a file.
  - The review lists every field of the contract; the EAI Master has 28. If exports keep only a few of them, a "show only the fields in this file" toggle would shorten it.
  - Since 2026-10-02 (ADR 0028), unreadable optional values are loaded as empty and listed in the upload result (*Values loaded as empty*). Like the column mapping, that list is not shown in the lineage of an older upload.
  - The organization's Veracode SCA export (2026-10-05) names eight fields differently (`SVR_DSCR`, `FND_STTS_DSCR`, `CVSSV3_SCOR`…); they are aliases now. Scoring compares severity ("1 - Critical"…) and status ("Open") as text, as the BRD describes for that file. Its free-text vulnerability summary (`SUM_DSCR`) can exceed 4,096 characters, so the per-field limit is 65,536 and an oversized cell is reported with its column and row.
  - DR is only scored for critical applications (2026-10-07, ADR 0031), as the report owner confirmed: with the Critical Application Indicator at "N" it has no data. `criticalOnly` in the ruleset; an unknown indicator does not exclude. A rules version published before that date lacks the flag: restore the built-in v1.3 and publish it. The owner also confirmed the Architectural Value formula (73.06 on their example) and the origin of tech debt (`V_TECH_DEBT_APP_SCORE`). The Operational Score keeps being read from the EAI/CMT file, column `Operational_Score` (decided on 2026-10-07; upstream it comes from `MV_APP_MDRNZTN_DTL`).
  - Dashboard (2026-10-06, business request): the heatmaps of questions 1 and 4 have one row per CIO, and primary capability became a column option next to region, country and target disposition. Totals per row, column and portfolio (averaged over applications, not cells), and totals in the prioritization matrix, complexity, obsolescence, resilience and the donuts. The footer claimed a SaaS/cancelled/decommissioned exclusion that no filter applied; it now says every EAI master application counts (DD-09). Causes of differences with the Power BI report: `docs/runbooks/conciliar-con-power-bi.md`.
  - When an upload is rejected only because of some files and the EAI master passed, *Recalculate with the files that passed* offers to upload again without them (2026-10-05). The System still publishes all or nothing; leaving a file out is confirmed by the person uploading. A source left out has no data in that upload. The organization also sends an OCP containers export (*Containerized*, weight 0 in BRD v1.3) that matches no source.
  - The organization's EAI master export has no `APP_ID`; its APP_ID is the same code as the EAI (confirmed on 2026-10-02), so tech debt is linked by the EAI code. If that ever changes, the export must bring `APP_ID` again, which takes precedence.
- **Acceptance criteria:** the lineage shows the recorded column of each field for the selected upload, with the same wording as the Mapping tab. **Effort:** S.

### TD-17 — An upload reads each file twice

- **Priority:** P3 · **Owner:** development · **Status:** Open
- **Context (2026-10-05, ADR 0030):**
  - The organization's Veracode SCA export weighs 73 MB. The per-file limit went from 64 MB to 128 MB after measuring it against the System pod's memory (1 GiB, a 768 MiB heap).
  - With the old reader, a 350,000-row synthetic SCA file ran out of memory in that heap. Rows are now compact (an array over a shared column index, repeated strings cached): 350,000 rows peak at 675 MB and 490,000 rows at 815 MB.
  - An upload still reads every file twice: once to validate it, and again when publishing, to assemble the new version from what was stored. Both adapters do it.
  - The margin with 490,000 rows is about 200 MB.
- **Acceptance criteria:** publishing assembles the version from the validated read, or reads each file as a stream that keeps only what the snapshot needs (counts per EAI for SCA); a 128 MB SCA file stays under 512 MB in the same heap. **Effort:** M.

---

## 5. Product follow-ups

### PF-01 — Save and share what-if scenarios
- **Priority:** P3 · **Owner:** product · **Status:** Open
- **Context:** The plan's What-If Lab mentions saving and sharing scenarios. Scenarios are computed on request and not stored.
- **Effort:** M.

### PF-02 — Override rule UI and audit
- **Priority:** P2 · **Owner:** product · **Status:** Blocked by SR-04
- **Effort:** M.

### PF-03 — Currency Score KPI
- **Priority:** P3 · **Owner:** product · **Status:** Done on 2026-09-18
- **Delivered:** the dashboard's *Currency exposure* KPI and the obsolescence panel read the CMT Currency Score (SR-02). Only the legacy threshold remains open (DD-01).

### PF-04 — Excel (XLSX) exports
- **Priority:** P3 · **Owner:** product · **Status:** Decision needed
- **Context:** Exports are CSV with a UTF-8 BOM, so Excel opens them. Native XLSX would add a library dependency, which must pass the vulnerability policy.
- **Effort:** S.

### PF-05 — Compare rules for a group of applications
- **Priority:** P3 · **Owner:** product · **Status:** Open
- **Context:** The comparison shows the whole portfolio and one application. A LOB, country or CIO view of the impact would reuse the same engine.
- **Effort:** S–M.

### PF-06 — Rules change history per application
- **Priority:** P3 · **Owner:** product · **Status:** Open
- **Context:** An application's screen could list how each published rules version changed its score. It would reuse the comparison.
- **Effort:** S.

### PF-07 — KPI dictionary and portal editing of the dashboard definitions
- **Priority:** P2 · **Owner:** product + enterprise architecture · **Status:** Done on 2026-09-18 (ADR 0025)
- **Delivered:** the *Definitions* page shows, for every indicator and panel, the definition and formula with the values in force, the data considered, what is not measured yet, the owner and status of its area, the frequency and the real data coverage. *Rules → Portfolio analytics* edits the 37 parameters, statuses and owners with the rules draft, simulation and publication. Every panel of the dashboard and of Insights explains its calculation.
- **Acceptance criteria met:** no indicator without its six fields; an API test publishes a threshold from the portal and the dashboard moves without a deployment.

### PF-08 — EA Value Evidence Register
- **Priority:** P3 · **Owner:** enterprise architecture · **Status:** Decision needed
- **Context:** Step 7 of the MVP proposal: a register of architecture decisions and the value they delivered. It is a recording and tracking tool, not a view over the current sources, so it was left out of the dashboard plan (PLAN §8.10).
- **Question:** is it in scope for this platform, and who records the evidence?
- **Effort:** M–L; it needs its own plan.

---

## 5bis. Data that would sharpen the portfolio answers

The six portfolio questions — how healthy is it, what to modernize first, where to reduce
complexity, where risk is concentrated, what drives obsolescence, where resilience is
missing — are answered today from the eight current sources, the computed score and the
roadmap catalog. Nothing below blocks an answer; each item turns one answer from
*indicative* into *real*. The method and its assumptions are in
[ADR 0023](adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md).

### DQ-01 — Application class column, as the business words the question
- **Priority:** P2 · **Owner:** EAI master owner + enterprise architecture · **Status:** Decision needed
- **Context:** The question was phrased as "more than X applications with the same *application class* per country". The sources have no such column, so the screen groups by `L1 Business Capability` from the EAI master, which is the closest thing that exists. If "application class" is a real attribute somewhere (an architecture repository, a CMDB), one column per application would answer the question exactly as asked.
- **What it changes:** the complexity answer stops being an approximation of the business's own wording.
- **Effort:** S in the platform (one more column in the EAI contract); the cost is in the source.

### DQ-02 — Estimated cost or effort per application
- **Priority:** P1 · **Owner:** application owners + finance · **Status:** Decision needed
- **Context:** The only effort in today's data is the weeks of the roadmap milestones, which are defined **per roadmap**, not per application: two applications on `Refactor` show the same weeks. The priority list therefore ranks by a proxy. A t-shirt size (S/M/L), a range of person-weeks or a budget figure per application would make "impact against effort" a real comparison.
- **What it changes:** the ranking of what to modernize first, which is the answer most likely to move money.
- **Effort:** S in the platform; the cost is in collecting it. A coarse value (S/M/L) is worth much more than nothing and is far cheaper than an estimate per application.

### DQ-03 — Business value of an application (revenue, volume, tier)
- **Priority:** P2 · **Owner:** business owners · **Status:** Decision needed
- **Context:** Business weight is built today from three attributes that do exist: critical indicator, user count and number of capabilities covered. Revenue supported, transactions per month or a criticality tier finer than a boolean would replace that proxy with the real thing.
- **What it changes:** the weighting of impact, and with it the order of the priority list.
- **Effort:** S in the platform; the decision is which measure the business considers fair.

### DQ-04 — Application dependencies and integrations
- **Priority:** P2 · **Owner:** enterprise architecture · **Status:** Decision needed
- **Context:** The complexity answer can say "four applications cover Policy Servicing in Chile", but not whether consolidating them is feasible: nothing in the sources says what calls what. A dependency edge list (source, target, kind) would let the screen say which of the four is the hub and which is a leaf.
- **What it changes:** complexity stops being a list of candidates and becomes a consolidation order.
- **Effort:** M — a new source with its own contract and lineage.

### DQ-05 — End-of-life and end-of-support dates of components
- **Priority:** P2 · **Owner:** RPT / architecture repository owner · **Status:** Decision needed
- **Context:** Obsolescence is measured today as counts of obsolete and not-approved components, with no dates. That answers "what is costing us points now" but not "what becomes obsolete next quarter", which is the version of the question a budget cycle needs.
- **What it changes:** obsolescence gains a time axis, and the priority list could bring forward what expires soon.
- **Partly addressed on 2026-09-26:** the Code screen dates every component the repositories declare, with endoflife.date as the source (ADR 0026). What the inventory declares —components that do not show up in any repository— still has no dates.
- **Effort:** M.

### DQ-06 — Incident and availability history
- **Priority:** P2 · **Owner:** operations · **Status:** Decision needed
- **Context:** The resilience answer is built from declared controls: DR scope, monitoring flags, operational score, RTO/RPO ranges. It says where a defence is missing, not where the portfolio has actually failed. Sev1 counts, MTTR and measured availability per application would tell whether a declared control works.
- **What it changes:** resilience stops being a compliance answer and becomes an evidence-based one.
- **Effort:** M — a new source, plus a retention decision (OP-03 applies).

### DQ-07 — Hosting and migration status beyond the cloud flags
- **Priority:** P3 · **Owner:** platform · **Status:** Decision needed
- **Context:** The sources carry `Cloud Indicator`, `Cloud Type`, `Cloud Vendor` and `Containerized`. A migration state (on-premises, IaaS, PaaS, SaaS, in flight, with a target date) would let the obsolescence and priority answers separate "old and staying" from "old and already being replaced".
- **What it changes:** avoids proposing work on applications a migration will retire anyway.
- **Effort:** S–M.

---

## 5ter. Executive dashboard definitions

The executive dashboard (`/`, the home page; PLAN §8.10) follows the *EA Data & Insights –
MVP Proposal (DRAFT)*. The proposal fixes the shape of each panel but leaves many
definitions open: "*Rules & Matrix to be defined globally*", "*Currency Score < X*", "*see
available concepts for Currency / Stability Scoring*", and several TBD dimensions. Each one
has a default so the dashboard works today, and the screen marks it as provisional. The
reasoning is in [ADR 0024](adr/0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md).

**Since 2026-09-18 (ADR 0025) every value below is edited from the portal** —*Rules →
Portfolio analytics*— and published as a rules version: answering an item is a
publication, not a deployment. Each area also has a status; marking it *agreed* removes the
provisional label from the dashboard. What cannot be edited from the portal is where a
value comes from and which metric explains which concept: that is code.

The figures quoted below come from the dummy portfolio (250 applications, seven
capabilities, three countries). They are not findings about the real portfolio; they show
where a default saturates or leaves a level empty.

### DD-01 — The legacy threshold X
- **Priority:** P2 · **Owner:** CMT owner + enterprise architecture · **Status:** Decision needed (X only)
- **Context:** The proposal defines *Legacy Platforms* as "Currency Score < X", and the *Currency Exposure* KPI stands in for EOS/EOL until TLM is a source. **The Currency Score was decided on 2026-09-18:** the CMT `Raw_Tech_Score` (SR-02).
- **Default:** X = 60, where the BRD's lowest band (0–59) ends.
- **On dummy data:** 16 % of the applications with a Currency Score are legacy (38 of 231).
- **Question:** what is X?
- **How to close it:** *Rules → Portfolio analytics → Currency Score*: set *Legacy platform below*, mark the area as agreed, simulate, publish.
- **Effort:** none in code.

### DD-02 — Risk Score formula and risk levels
- **Priority:** P1 · **Owner:** enterprise architecture + security · **Status:** Decision needed
- **Context:** Question 4 draws a 0–100 Risk Score per application, in a heatmap and as bubble size. It is not the risk index of the Insights screen, which ranks groups by weighted counts.
- **Default:** the *Modernization Risk Vectors* pillar inverted and rescaled: (100 − pillar) ÷ 80 × 100. Levels from the mockup: below 40 low, 40–59 moderate, 60–79 high, 80 and above critical. The concentration index is the share of total risk carried by the top 20 % of heatmap cells.
- **Question:** is the pillar the right base, or should the Risk Score combine SCA flaws, Kenna and currency with its own weights? Are the mockup levels final?
- **Effort:** S for levels; M for a new formula.

### DD-03 — Criticality, opportunity and the candidate quadrant
- **Priority:** P1 · **Owner:** business owner of the score + enterprise architecture · **Status:** Decision needed
- **Context:** Question 2 plots business criticality against modernization opportunity, with "*Rules & Matrix to be defined globally*".
- **Default:** criticality 0–3. Very high: critical and broad; high: critical; medium: broad; low: the rest. Broad means a significant user base (the roadmap catalog threshold) or three or more capabilities. Opportunity 0–3 from the uplift of the application's own roadmap: below 5, 5–10, 10–20, 20 and above. Candidates: both at high or very high.
- **Automatic plans (2026-10-07, ADR 0033):** the organization's EAI master export has no `Roadmap` column, so question 2 was empty. An application without a MAPS roadmap, and not marked *Eliminate*, now gets an automatic plan: the what-if levers that take it to a score of **90** (`automaticPlanTarget` in `contracts/roadmap-v1.json`; null turns it off). Its opportunity is that plan's uplift. A MAPS roadmap still wins when the export carries one. The target is not yet editable from *Rules*; changing it means publishing a new roadmap catalog.
- **On dummy data:** the *high* criticality row is empty, because every critical application is also broad.
- **Question:** which attributes define criticality (a tier from the business would replace the proxy, DQ-03), does opportunity include effort once DQ-02 exists, and is 90 the right target for automatic plans?
- **Effort:** S for thresholds; M if new attributes are needed.

### DD-04 — Complexity dimensions and their thresholds
- **Priority:** P2 · **Owner:** enterprise architecture · **Status:** Decision needed
- **Context:** Question 3 asks for six dimensions. The refinement defines two (*Legacy Platforms* = Currency Score < X; *Tech Debt* = open technical debts); the rest are TBD.
- **Default:** legacy platforms (DD-01); duplicate technologies = a capability with three or more applications in the same country; technical debt = Tech Debt Score in its worst band, because the open-debts source does not exist yet; technology proliferation = a capability written in three or more languages. *Shared dependencies* and *other complexity* wait for a source (DQ-04).
- **On dummy data:** duplicate technologies and technology proliferation mark all 250 applications: at these thresholds, seven capabilities across three countries cannot be told apart.
- **Question:** the definition and threshold of each dimension, and whether *application class* (DQ-01) replaces the capability.
- **Effort:** S for thresholds; M per new source.

### DD-05 — Resilience levels (Stability Scoring concepts)
- **Priority:** P2 · **Owner:** operations + resilience owner · **Status:** Decision needed
- **Context:** Question 6 refers to "available concepts for Stability Scoring", which did not arrive.
- **Default:** recovery from DR scope and RTO (moderate from 24 h, high from 48 h, critical without DR); operational vulnerability from the operational score (below 80, 60 and 40) and monitoring; the application takes its worst dimension. Availability, critical dependencies and single points of failure wait for a source (DQ-06, DQ-04).
- **On dummy data:** 77 % of the applications with data have high or critical gaps.
- **Question:** the Stability Scoring concepts and their cut-offs.
- **Effort:** S–M.

### DD-06 — Health band names and cut-offs: mockup vs model
- **Priority:** P2 · **Owner:** business owner of the score · **Status:** Decision needed
- **Context:** The mockup uses *Healthy 80–100, Monitor 60–79, At Risk 40–59, Critical < 40*. The platform uses the model bands (*Solid 85–100, Acceptable 70–85, At risk 50–70, Critical 20–50*), which the rest of the site shows too.
- **Default:** the model bands, so the dashboard and the other screens agree.
- **Question:** adopt the mockup bands everywhere, keep the model bands, or use the mockup bands only on the dashboard (not recommended: "At risk" would mean two things on one site).
- **Effort:** S.

### DD-07 — Tech Debt KPI
- **Priority:** P3 · **Owner:** product · **Status:** Decision needed (KPI only)
- **Context:** The refinement leaves the Tech Debt KPI in doubt and Governance Adoption out. **The home page was decided on 2026-09-18:** the dashboard is `/`, the portfolio grid moved to `/portfolio`, and `/dashboard` redirects keeping its filters.
- **Default:** five KPIs without Tech Debt. The *technical debt* complexity dimension already counts applications whose mapped Tech Debt Score is at or below 20.
- **Question:** is a sixth KPI worth it —share of applications with high technical debt— or is the complexity panel enough?
- **Effort:** S.

### DD-08 — Who may change the definitions
- **Priority:** P3 · **Owner:** security + enterprise architecture · **Status:** Decision needed
- **Context:** The definitions travel with the rules, so the rules administrator (`ruleset.admin`) changes both, with the same step-up and audit. A definition changes no score, only how the dashboard reads it.
- **Question:** is one role enough, or should the business owners of the definitions have a role that edits only the *Portfolio analytics* tab?
- **Default:** one role.
- **Effort:** S–M: a second role, a check per part of the draft, and the screen hiding the tabs a role cannot edit.

### DD-09 — Portfolio population: which applications are left out
- **Priority:** P1 · **Owner:** business owner of the score + EAI data owner · **Status:** Decision needed
- **Context (2026-10-06):** the organization's Power BI report says it excludes SaaS, cancelled and decommissioned applications and keeps only Common Service and Core Business applications, and its LATAM view filters Application Status with a multiple selection. The site's footer said the same, but no filter did it: every application in the EAI master upload counts. The footer now says so. This is the first suspect when the LATAM averages do not match the report.
- **Question:** which EAI master column marks "Common Service" and "Core Business" (it is not among the 28 columns of the contract), which `Application Status` values are cancelled or decommissioned, and is SaaS `Cloud_Type = SaaS`?
- **Default until then:** no exclusion; the *App status* filter narrows by hand.
- **Effort:** S once the fields are known: a population rule in the rules contract (versioned and audited like the rest), applied before scoring and shown in the footer and in each answer.

---

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
- **Context:** The index adds points per finding: 10 per component past its end of life, 4 per component ending within 12 months, 5 per critical advisory, 2 per high advisory, 3 per deprecated library, 1 per library two or more major versions behind and 5 per repository with no commits in 12 months or archived. Levels: moderate 10, high 25, critical 50. It is a sum with no cap, so an application with many repositories accumulates more. All 15 values are editable in *Rules → Portfolio analytics* (area `code`).
- **On the sample data:** 186 of 250 applications have repositories; 96 have at least one component past its end of life and about 60 are at high or critical code risk.
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

## 6. Operations

### OP-01 — Runbook walkthrough with operations
- **Priority:** P1 · **Owner:** operations · **Status:** Open
- **Runbooks** in [docs/runbooks](runbooks):
  - secret rotation;
  - faulty upload;
  - disaster recovery;
  - security incident;
  - faulty rules and rollback;
  - **git backup (new)**.
- **Acceptance criteria:** each runbook executed once in a test environment, with notes. **Effort:** M.

### OP-02 — Backups of the data volume and the rules store
- **Priority:** P1 · **Owner:** operations · **Status:** Open
- **Context:** Uploads and published rules versions are runtime state, not in git: `data/history`, `.rules` or the `rules_version` table. The disaster recovery runbook describes restoring them, but no backup schedule is configured.
- **Acceptance criteria:** scheduled backups; restore test. **Effort:** S–M.

### OP-03 — Upload retention policy
- **Priority:** P1 · **Owner:** data owners + compliance · **Status:** Decision needed
- **Context:**
  - With the database adapter, `Database:RetainVersions` keeps the last N uploads; with files, retention is manual.
  - Uploads contain vulnerability data per application.
- **Question:** How long must uploads be kept? Legal or audit requirements?
- **Effort:** S.

### OP-04 — GitHub repository settings
- **Priority:** P1 · **Owner:** repository owner · **Status:** Open (first push done on 2026-09-15)
- **Context:**
  - Remote `origin` = `https://github.com/camilarodriguez10/modscore1.git`, a **private** repository (SEC-21).
  - `main` holds the baseline, phase 13 and the full backup of every created file except captures and images.
  - Since 2026-10-05, every code change is pushed to `main` without asking, at the repository owner's request.
- **Work:**
  1. Branch protection on `main`: required reviews, required status checks from the CI gates, no force push. Signed commits per plan §7.2 if the team uses them.
  2. Enable Renovate and secret scanning.
  3. Tag phase closes (`fase-13`).
  4. Watch repository size. The portable `.zip` files versioned until 2026-09-27 stay in the history (about 93 MB each). If the size becomes a problem, purge them with a history rewrite (`git filter-repo`), coordinated with everyone who has a clone. GitHub rejects files over 100 MB.
  5. When adding required status checks, remember that `ci.yml` skips docs-only changes: its checks stay "Expected" on a docs-only PR. Either require only the `seguridad.yml` checks for those PRs or merge them explicitly.
- **Acceptance criteria:** branch protection active; first CI run green (SEC-20).
- **Effort:** S.

### OP-05 — Portable package distribution checklist
- **Closed on 2026-09-27:** the portable package was removed; the application runs from the code ([guide](guia-localhost.md)).
- **Priority:** P2 · **Owner:** whoever distributes it · **Status:** Closed
- **Checklist:**
  - Build with `tools/portable/build-portable.ps1` on a clean checkout of a tagged commit.
  - Share the `.zip` and its SHA-256 through corporate storage, not email attachments.
  - Testers extract locally (not in a synced folder), run `start.cmd`, follow `README.txt`, and never use real data.
  - Collect feedback against the "What to test" list.
- **Effort:** S.

---

## 7. Done in phase 13 (for traceability)

| Request of 2026-09-14 | What was done | Evidence |
|---|---|---|
| Review comments, update the plan, handoff | PLAN sections 1, 6.1, 8, 8.6, 8ter, 9 and 10 updated; `HANDOFF.md`; this file | `PLAN.md`, `HANDOFF.md` |
| Git backups | Repository, `.gitignore`, `.gitattributes`, remote, baseline commit, backup scripts with gates and blocked files, runbook | `tools/git/`, `docs/runbooks/respaldo-en-git.md` |
| ADR and documentation corrections; score rules first | "Null or zero" rule (ADR 0019); ruleset notes against the captures (RFC-03 wording, RFC-13…17); ADR 0003, 0017 and 0018 corrected; PLAN ASVS totals; 18 metrics | `contracts/ruleset-v1.3.json`, `docs/adr/0019-*.md`, `MappingRuleTests` |
| English pending file | This document | `docs/PENDING-ITEMS.md` |
| H-16, H-17, H-20, H-21 to the end | Last phase of the plan | PLAN §8ter |
| ASVS items into the plan | Phase 14 with every partial item | PLAN §8ter, SEC-01…14 |
| Technical debt | Cheap rules listing with immutable-version cache; stale staging cleanup; C#↔TS contract test; lazy-loaded admin screens (main bundle 429 → 406 kB); pure draft logic extracted and tested; load test with rules scenarios and first-request latency | `RulesService`, `FileRulesStore`, `ViewModelContractTests`, `App.tsx`, `src/rules/draft.ts`, `tools/Modernization.LoadTest` |
| Performance within the stack | Parallel history per upload; background warm-up after start, uploads and publications; simulation reuses the active side and computes the draft side in parallel | Section 8 below |
| Robustness; OIDC to be replaced by APIM | Rules store failures keep scoring with active rules (5303), 503 on old versions, publish rejected with a notice; `IReauthenticationFlow` closes the previous session; ADR 0020 migration path | `RulesStoreFailureTests`, `ReauthenticationFlowTests`, `docs/adr/0020-*.md` |
| Keep outdated packages without vulnerabilities | Policy recorded; TD-01 | This file |
| SPA unit and end-to-end tests | Vitest (35 tests: draft logic, notices, filters, API client, trend markers); Playwright with Edge (6 scenarios) | `src/web/src/**/*.test.ts`, `src/web/e2e/` |
| Suggestion 4: rule changes on the trend | Timeline endpoint for readers; dashed markers and list under the trend | `ScoreTrend.tsx`, `RulesComparisonAndExportTests` |
| Suggestion 5: new comparison view | `/admin/rules/compare`: two versions or the draft, portfolio impact and one application metric by metric | `RulesComparePage.tsx`, `RulesComparisonTests` |
| Suggestion 7: downloadable content by filters and notice | Portfolio, roadmap and comparison CSV; in-app notice of new rules | `ExportEndpoints.cs`, `RulesChangedNotice.tsx` |
| Portable executable package | Self-contained single-file build for System and Channel with the SPA, launcher with local-only binding and per-run token. **Removed on 2026-09-27** | `tools/portable/` (deleted) |

---

## 8. Measurements

Release builds on the development machine (16 logical cores). File adapter with 30 uploads of 250 applications. 8 concurrent clients, 10 s per scenario. "First" is the first request with cold caches.

| Scenario | Before phase 13: first / p50 | After, warm-up off: first / p50 | After, warm-up on, busy machine: first / p50 | **Final, warm-up on, idle machine: first / p50** |
|---|---|---|---|---|
| System · portfolio history (30 uploads) | **1,655 ms** / 6.3 ms | **584 ms** / 6.4 ms | **8.2 ms** / 6.4 ms | **15.4 ms** / 5.2 ms |
| System · rules simulation (Kenna 5, MFA 10) | 125 ms / **117 ms** (66 req/s) | 147 ms / **57 ms** (131 req/s) | 136 ms / 204 ms (see TD-06) | 94 ms / **47 ms** (168 req/s, p95 66 ms) |
| System · rules versions list | 1.1 ms / 0.3 ms | 1.0 ms / 0.3 ms | 1.3 ms / 0.3 ms | 0.9 ms / 0.3 ms |
| System · scores (200 per page) | 72 ms / 16.3 ms | 85 ms / 14.9 ms | 45 ms / 17.5 ms | 35 ms / 12.2 ms |
| System · single score | 2.8 ms / 0.2 ms | 3.7 ms / 0.2 ms | 3.2 ms / 0.2 ms | 2.6 ms / 0.2 ms |
| System · what-if (MFA) | 76 ms / 0.3 ms | 57 ms / 0.3 ms | 57 ms / 0.3 ms | 51 ms / 0.3 ms |

Numbers are for comparing changes on the same machine, not for sizing production.

---

## 9. Test inventory and commands

| Suite | Count | Command |
|---|---|---|
| Engine (golden cases, mapping table, what-if, roadmap, rules editor, comparison, dashboard, insights, end-of-life catalog, code analyzer) | 349 | `dotnet test tests/Modernization.Scoring.Engine.Tests` |
| Source contracts (Dummy, File, Database; rules stores; sample repositories; dummy timeline; column mapping; rejection reasons; regional values, Power BI and Veracode exports; memory per row; Excel workbooks and several files per source; renamed Product Strategy export) | 342 | `dotnet test tests/Modernization.Sources.ContractTests` |
| API end-to-end through both layers and security | 359 | `dotnet test tests/Modernization.Api.Tests` |
| SPA unit (Vitest) | 95 | `npm --prefix src/web test` |
| SPA end-to-end (Playwright, Edge locally, Chromium in CI) | 35 | `npm --prefix src/web run test:e2e` |
| SPA accessibility (axe, the daily screens, the Code detail and the column mapping, in light and dark) | 27 | Same command; `e2e/accessibility.spec.ts` |
| SPA end-to-end against an already running site | Same | With the site running (for example the Channel serving `src/web/dist`), from `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |
| SPA lint (ESLint, required by CI) | 0 errors | `npm --prefix src/web run lint` |
| Vulnerable packages | 0 | `dotnet list ModernizationScore.slnx package --vulnerable --include-transitive` and `npm --prefix src/web audit --audit-level=moderate` |

---

## 10. Proposed phase plan (to validate with the team)

| Phase | Name | Content | Main items |
|---|---|---|---|
| 13 | Review and remediation | Delivered on 2026-09-14 | Section 7 |
| 14 | Security verification (ASVS L2) | **In progress.** Done on 2026-09-15: CI green, SEC-06, SEC-07, SEC-12, SEC-14, and e2e with axe in CI (TD-04, TD-05). Left: the eleven partials that need a test environment, ZAP, and the peer review | SEC-01…05, SEC-08…11, SEC-13, SEC-19, SEC-20, TD-09, TD-13 |
| 15 | Authentication through APIM | **In progress.** Option C with A decided on 2026-09-16; pluggable credential, JWT validation, `Both` mode for the cutover, APIM step-up and group-to-scope map, all tested with doubles. Left: the issuer itself (APIM), SEC-19 against the real provider and closing H-16 | PA-02, PA-03, PA-05, SEC-03, SEC-19 |
| 16 | Scale and operations | Target and engine; Redis; replicas; backups; retention; SIEM; notifications | PA-01, PA-04, PA-06, PA-07, PA-08, PA-09, OP-01…03 |
| 17 | Infrastructure findings (last, by decision) | H-16 workload identity, H-17 image signing and immutable logs, H-20 session management, H-21 upload antivirus | SEC-15…18 |
| C2 | Code screen on real repositories (parallel track) | `code-collector` with a read-only GitHub App, repository → application mapping, egress, advisory source, catalog refresh; the screen (C1) already exists on sample data | GH-01…GH-09 |

Business decisions SR-01…SR-15 run in parallel from now on. SR-01, SR-03 and SR-07 should close before phase 14 ends, because they change published scores.
