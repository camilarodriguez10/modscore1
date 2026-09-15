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

- **ID** — stable identifier to use in meetings, tickets and commits. `SR` scoring rules, `PA` platform/architecture, `SEC` security, `TD` technical debt, `PF` product follow-up, `OP` operations.
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

- Score engine reproducing both documented cases exactly: **EAI 14381 = 79.67** (BRD worked example) and **EAI 10969 = 79.61** (Power BI v1.3 dashboard).
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
  - Git repository with backup scripts, and a portable package to test on other machines.
- **Tests**:
  - 640 .NET tests: 200 engine, 175 contracts, 265 API.
  - 35 SPA unit tests.
  - 6 end-to-end scenarios in Microsoft Edge.
  - Zero vulnerable packages, and `npm audit` is clean.

### What needs the team

| Group | Items | Blocking production? |
|---|---|---|
| Scoring rules (business) | SR-01 … SR-15 | SR-01, SR-03, SR-07 must be confirmed before scores are published to the organization (P0/P1) |
| Platform and architecture | PA-01 … PA-09 | PA-01 (target and database engine) and PA-02 (authentication through APIM) are P0 |
| Security | SEC-01 … SEC-24 | SEC-19 and SEC-20 are P0 (SEC-21 was decided on 2026-09-15); ASVS partials are P1; H-16/H-17/H-20/H-21 deferred to the last phase by decision |
| Technical debt | TD-01 … TD-15 | None blocks; TD-06 and TD-09 are P1 |
| Product follow-ups | PF-01 … PF-06 | No |
| Operations | OP-01 … OP-05 | No. The first push was done on 2026-09-15; OP-04 (branch protection and repository settings) is P1 |

### All items at a glance

| ID | Title | Category | Priority | Suggested owner | Status |
|---|---|---|---|---|---|
| SR-01 | Composite metrics strategy (RFC-01) | Scoring rules | P0 | Business owner of the score + Power BI model owner | Decision needed |
| SR-02 | Currency Score definition (RFC-02) | Scoring rules | P2 | CMT owner | Decision needed |
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
| PA-02 | Authentication through Azure API Management with OAuth (ADR 0020) | Platform / security | P0 | Platform + identity + security | Decision needed |
| PA-03 | Portfolio scope claim (RFC-10) | Platform / security | P0 | Identity | Decision needed |
| PA-04 | Shared session cache (RFC-11) | Platform | P2 | Platform | Open |
| PA-05 | Workload identity Channel → System (RFC-12, H-16) | Platform / security | P1 | Platform + security | Open |
| PA-06 | Horizontal scaling constraints | Platform | P2 | Platform | Open |
| PA-07 | Secrets manager integration verified | Platform / security | P1 | Platform | Open |
| PA-08 | Observability and SIEM rules | Platform / security | P1 | Operations + security | Open |
| PA-09 | Notifications to responsible parties when rules are published | Platform / product | P2 | Product + platform | Decision needed |
| SEC-01 … SEC-14 | ASVS L2 partial requirements | Security | P1 | Security + platform | Open (phase 14) |
| SEC-15 | ASVS 3.3.4 — see and close own sessions (H-20) | Security | P3 | Development | Deferred (by decision) |
| SEC-16 | ASVS 7.3.3 — tamper-proof logs (H-17) | Security | P2 | Operations | Deferred (by decision) |
| SEC-17 | ASVS 12.4.2 — antivirus on uploads (H-21) | Security | P3 | Development + platform | Deferred (by decision) |
| SEC-18 | H-16 static shared service token | Security | P2 | Platform | Deferred (by decision) |
| SEC-19 | Step-up against a real identity provider not verified | Security | P0 | Identity + development | Open |
| SEC-20 | Security gates never run outside CI (DAST, Trivy, CodeQL, Checkov, gitleaks, image build) | Security | P0 | DevSecOps | Open |
| SEC-21 | Repository visibility, BRD captures and commit metadata | Security | P0 | Repository owner | Decided (2026-09-15) |
| SEC-22 | Portable package uses development authentication | Security | P1 | Everyone who distributes it | Accepted risk (with rules) |
| SEC-23 | Rules timeline shows the publication reason to every reader | Security / privacy | P3 | Rules administrators | Accepted risk |
| SEC-24 | CSV exports and data exfiltration | Security | P1 | Security + data owners | Decision needed |
| TD-01 | Outdated packages kept on purpose | Technical debt | P3 | Development | Accepted (policy) |
| TD-02 | React 19 features promised in the plan but not used | Technical debt | P3 | Development | Decision needed |
| TD-03 | OpenAPI-generated client replaced by a contract test | Technical debt | P3 | Development | Decision needed |
| TD-04 | Visual QA and accessibility audit of the new screens | Quality | P1 | UX + development | Open |
| TD-05 | End-to-end tests in CI | Quality | P1 | DevSecOps | Open |
| TD-06 | Simulation latency under concurrency | Performance | P1 | Development | Open |
| TD-07 | History performance with the database adapter | Performance | P2 | Development | Open |
| TD-08 | Load test coverage of Channel, exports and comparison | Performance | P2 | Development | Open |
| TD-09 | Language sweep does not cover the new endpoints | Quality | P1 | Development | Open |
| TD-10 | CSV of a draft's impact | Product / debt | P3 | Development | Open |
| TD-11 | Mixed line endings in the repository | Hygiene | P3 | Development | Decision needed |
| TD-12 | Lock files and runtime-specific publish | Supply chain | P3 | Development | Mitigated |
| TD-13 | Security review of phases 09–13 by a second person | Security | P1 | Security | Open |
| TD-14 | Size of the rules editor screen | Maintainability | P3 | Development | Open |
| TD-15 | Warm-up service and caches with many versions | Performance | P3 | Development | Open |
| PF-01 | Save and share what-if scenarios | Product | P3 | Product | Open |
| PF-02 | Override rule UI and audit | Product | P2 | Product | Blocked by SR-04 |
| PF-03 | Currency Score KPI | Product | P3 | Product | Blocked by SR-02 |
| PF-04 | Excel (XLSX) exports | Product | P3 | Product | Decision needed |
| PF-05 | Compare rules for a group of applications (LOB, country) | Product | P3 | Product | Open |
| PF-06 | Rules change history per application | Product | P3 | Product | Open |
| OP-01 | Runbook walkthrough with operations | Operations | P1 | Operations | Open |
| OP-02 | Backups of the data volume and the rules store | Operations | P1 | Operations | Open |
| OP-03 | Upload retention policy | Operations / compliance | P1 | Data owners + compliance | Decision needed |
| OP-04 | GitHub repository settings (first push done) | Operations | P1 | Repository owner | Open |
| OP-05 | Portable package distribution checklist | Operations | P2 | Whoever distributes it | Open |

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

- **Priority:** P2 · **Owner:** CMT (Currency Model) owner · **Status:** Decision needed
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
  - Manifests target Kubernetes (AKS assumed from BRD mentions): `deploy/k8s`, chiseled non-root images, Pod Security Admission restricted, default-deny NetworkPolicy and service-mesh mTLS.
  - The database adapter is engine-agnostic and verified against SQLite with migrations. An unverified engine fails explicitly at startup.
- **Question:** AKS or other? SQL Server or PostgreSQL? Managed identity for the database?
- **Work after decision:**
  - Add the EF Core provider package (vulnerability-free) and a migration set for the engine.
  - Run the database contract suite against it.
  - Add a deployment migration job.
- **Acceptance criteria:** contract tests green against the chosen engine; golden 79.67 read from it; migration job documented.
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
| SEC-03 | 2.10.1 | Integration secrets are not static | Shared token with seamless rotation and mTLS | Short-lived workload identity (PA-05) or APIM (PA-02 C) | No static secret in configuration | M |
| SEC-04 | 4.3.1 | MFA on administrative interfaces | Uploads need `ingestion.write`; rules need `ruleset.admin` + step-up within 10 minutes | MFA enforcement for both roles is an IdP/APIM conditional access policy | Sign-in with each role requires MFA | S |
| SEC-05 | 6.4.1 | Secrets manager | CSI key-per-file mount declared | Verification against the real vault (PA-07) | Secrets read from the vault; none in env vars | S |
| SEC-06 | 7.1.3 | Security events logged | Sessions, denials, CSRF, rate limits, uploads, scope, rules publications and rejections, step-up, **exports (4213, new)** | Rejected validations (400) are not audited | Decide whether 400 on sensitive endpoints must be audited; add event | S |
| SEC-07 | 8.3.4 | Retention policy for sensitive data | `Database:RetainVersions` for the database adapter; manual for files | Business retention policy (OP-03) | Documented policy and configured retention | S |
| SEC-08 | 9.1.2 | Only strong cipher suites | Ingress configuration | Verification in the real ingress | TLS scan (for example `testssl.sh`) | S |
| SEC-09 | 9.1.3 | Only TLS 1.2 or higher | Ingress configuration | Same | Same | S |
| SEC-10 | 9.2.2 | Encryption on internal connections | Mesh policy `all-authenticated` | Verification in cluster; database and Redis with TLS | Connection strings require TLS; mesh verification | S |
| SEC-11 | 11.1.8 | Configurable alerts | Stable EventIds | SIEM rules (PA-08) | Alerts fire in test | M |
| SEC-12 | 13.2.2 | JSON schema validation | Typed DTOs, `BoundedJson` (128 KB, strict JSON, no unknown fields) for rules, explicit checks | No formal schema validation | Decide if strict typed binding is enough or add schema validation for rules bodies | S–M |
| SEC-13 | 14.1.5 | Configuration integrity | Checkov in CI | Image signing and admission verification (H-17) | Signed images, admission policy rejects unsigned | M |
| SEC-14 | 14.2.6 | Isolation of third-party components | Containers, NetworkPolicy | No per-library sandbox | Accept as design limit or document compensating controls | S |

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
  - Size limits: 64 MB per file, 256 MB total.
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

- **Priority:** P0 · **Owner:** DevSecOps · **Status:** Open
- **Context:**
  - `.github/workflows/ci.yml` declares tests, vulnerable packages, CodeQL, Gitleaks, OSV-Scanner, Trivy, Checkov, SBOM (CycloneDX), actions pinned by SHA, and DAST with OWASP ZAP in staging.
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
    - Checkov 3.3.17 first failed 4 checks, so the CI job would have failed:
      - CKV_K8S_40 on both Deployments: UID 1654 is below 10000. Fixed: `runAsUser`, `runAsGroup` and `fsGroup` are now 10001. Both images ran as UID 10001 with a read-only root filesystem, and `/health` and the SPA answered.
      - CKV_DOCKER_2 on both Dockerfiles: no `HEALTHCHECK`. Suppressed inline with a reason: chiseled images have no shell or curl, and Kubernetes uses its `/health` probes instead.
      - After the fixes: Kubernetes 188 passed, 0 failed; Dockerfile 128 passed, 0 failed, 2 skipped.
    - `aquasecurity/trivy-action` v0.36.0 (pinned SHA `ed142fd`, not affected by the March 2026 compromise) installs **Trivy v0.70.0** by default. That version is affected by GHSA-mcj4-mphf-j9ff (High), GHSA-q3fv-x8vg-qqm4 and GHSA-8rc5-4fr6-64pw, all fixed by v0.72.0. The workflow now pins `version: v0.74.0`, the same version used locally.
    - `.dockerignore` now excludes `artifacts/`, `*.zip`, test results and `*.tsbuildinfo`.
- **Work:**
  - Check the next CI run: CodeQL with the new gate, and the images job in CI.
  - Run OWASP ZAP against a staging deployment (needs PA-01).
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
    - the latest portable `.zip`, about 93 MB;
    - the local run state (`data/history/`, `data/modernization.db*`, dummy data only);
    - the helper scripts of the working sessions (`tools/sesiones/`);
    - a copy of the published plan (`docs/plan/`);
    - `.claude/launch.json`.
  - Build outputs, test results and secrets stay out.
- **Acceptance criteria:** decisions recorded in [docs/runbooks/respaldo-en-git.md](runbooks/respaldo-en-git.md) (done); repository settings applied (OP-04).
- **Effort:** S.

### SEC-22 — The portable package uses development authentication

- **Priority:** P1 · **Owner:** everyone who distributes the package · **Status:** Accepted risk with rules
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

- **Priority:** P1 · **Owner:** UX + development · **Status:** Open
- **Context:**
  - Screens are **Rules** (phase 12), **Compare rules**, **trend markers**, the **rules notice** and **Download CSV** buttons (phase 13).
  - Functional behavior is covered by end-to-end tests.
  - No visual review on several widths and no accessibility audit (axe, screen reader, keyboard only) has been done for them.
- **Work:** axe scan (can be added to Playwright with `@axe-core/playwright` after a vulnerability check); keyboard-only walkthrough; review at 1280, 1024 and 768 px; contrast of the highlighted rows.
- **Acceptance criteria:** zero serious or critical axe violations; walkthrough notes resolved. **Effort:** M.

### TD-05 — End-to-end tests in CI

- **Priority:** P1 · **Owner:** DevSecOps · **Status:** Open
- **Context:**
  - Six Playwright scenarios run locally with the installed Microsoft Edge (`channel: 'msedge'`), so no browser is downloaded.
  - They start System, Channel and Vite, or reuse running ones; they can also target a running portable package with `MODSCORE_E2E_BASE_URL`.
  - Run time is about 30 s after builds.
- **Work:** CI job with .NET 10, Node 24 and Edge or Chromium (a runner image with a browser, or `npx playwright install` with its supply-chain review); upload traces on failure.
- **Acceptance criteria:** e2e job required on pull requests. **Effort:** S–M.

### TD-06 — Simulation latency under concurrency

- **Priority:** P1 · **Owner:** development · **Status:** Open
- **Context:**
  - Measured with Release builds, 30 uploads, 200+ applications, 8 concurrent clients and 10 s per scenario. On an idle machine, rules simulation p50 went from **117 ms to 47 ms** (p95 66 ms) and throughput from 66 to 168 requests/s after phase 13: the active side is cached and the draft side runs in parallel.
  - A run taken while the machine was busy writing files showed p50 204 ms. The same code measured idle gave 47 ms: latency is sensitive to CPU contention.
  - Parallel scoring inside requests competes with other requests for thread-pool threads.
  - Simulation is rate-limited to 10 per minute per user, so real concurrency is low.
- **Work:** measure on target hardware with realistic concurrency; if needed, bound `MaxDegreeOfParallelism` in `RulesImpactSimulator.ScoreAll` and in the plan summary.
- **Acceptance criteria:** p95 under 500 ms with 8 concurrent simulations on target hardware. **Effort:** S.

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

### TD-09 — Language sweep does not cover the new endpoints

- **Priority:** P1 · **Owner:** development · **Status:** Open
- **Context:** Phase 11's `LanguageContractTests` crawls Channel responses and fails on Spanish text. The comparison, timeline and simulation-with-application responses were added later and are not in the crawl. The CSV headers are English, but the rule notes embedded in comparisons come from the ruleset (English).
- **Work:** add the new endpoints and the CSV bodies to the sweep.
- **Acceptance criteria:** sweep covers every Channel GET endpoint. **Effort:** S.

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

- **Priority:** P3 · **Owner:** development · **Status:** Mitigated
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
- **Priority:** P3 · **Owner:** product · **Status:** Blocked by SR-02
- **Effort:** S–M.

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
  - Every push is still confirmed with the repository owner before it happens.
- **Work:**
  1. Branch protection on `main`: required reviews, required status checks from the CI gates, no force push. Signed commits per plan §7.2 if the team uses them.
  2. Enable Renovate and secret scanning.
  3. Tag phase closes (`fase-13`).
  4. Watch repository size. Each portable `.zip` adds about 93 MB to the history for good, and GitHub rejects files over 100 MB. Keep only the latest `.zip` in the tree; move to GitHub Releases or Git LFS if builds become frequent.
- **Acceptance criteria:** branch protection active; first CI run green (SEC-20).
- **Effort:** S.

### OP-05 — Portable package distribution checklist
- **Priority:** P2 · **Owner:** whoever distributes it · **Status:** Open
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
| Portable executable package | Self-contained single-file build for System and Channel with the SPA, launcher with local-only binding and per-run token | `tools/portable/` |

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
| Engine (golden cases, mapping table, what-if, roadmap, rules editor, comparison) | 200 | `dotnet test tests/Modernization.Scoring.Engine.Tests` |
| Source contracts (Dummy, File, Database; rules stores) | 175 | `dotnet test tests/Modernization.Sources.ContractTests` |
| API end-to-end through both layers and security | 265 | `dotnet test tests/Modernization.Api.Tests` |
| SPA unit (Vitest) | 35 | `npm --prefix src/web test` |
| SPA end-to-end (Playwright, Edge) | 6 | `npm --prefix src/web run test:e2e` |
| SPA end-to-end against the portable package | 6 | With `start.ps1 -NoBrowser` running, from `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |
| SPA lint (ESLint, required by CI) | 0 errors | `npm --prefix src/web run lint` |
| Vulnerable packages | 0 | `dotnet list ModernizationScore.slnx package --vulnerable --include-transitive` and `npm --prefix src/web audit --audit-level=moderate` |

---

## 10. Proposed phase plan (to validate with the team)

| Phase | Name | Content | Main items |
|---|---|---|---|
| 13 | Review and remediation | Delivered on 2026-09-14 | Section 7 |
| 14 | Security verification (ASVS L2) | First CI run and fixes; ASVS partial items verified in a test environment; peer security review; e2e and axe in CI | SEC-01…14, SEC-19, SEC-20, TD-04, TD-05, TD-09, TD-13 |
| 15 | Authentication through APIM | Decide option; implement flow; scope claim; remove static token if option C | PA-02, PA-03, PA-05, SEC-03, SEC-19 |
| 16 | Scale and operations | Target and engine; Redis; replicas; backups; retention; SIEM; notifications | PA-01, PA-04, PA-06, PA-07, PA-08, PA-09, OP-01…03 |
| 17 | Infrastructure findings (last, by decision) | H-16 workload identity, H-17 image signing and immutable logs, H-20 session management, H-21 upload antivirus | SEC-15…18 |

Business decisions SR-01…SR-15 run in parallel from now on. SR-01, SR-03 and SR-07 should close before phase 14 ends, because they change published scores.
