# Modernization Score — decisions we need from the business

| | |
|---|---|
| **Date** | 2026-09-16 |
| **For** | Business owner of the Modernization Score · Power BI model owner · (part 3) application security, observability and enterprise architecture |
| **Purpose** | Confirm how the platform reads eleven rules of the BRD, so the scores it publishes are the organization's numbers and not our interpretation |
| **Time needed** | 45 minutes for part 1 · the rest can be answered by mail |
| **What we need out of it** | A yes/no on three rules, and one dashboard capture. Everything else has a working default |

## The short version

The platform reproduces the two documented cases **exactly** — EAI 14381 = 79.67 (the BRD worked example, from raw data) and EAI 10969 = 79.61 (the Power BI v1.3 dashboard, metric by metric). That is the proof the engine is right.

What is *not* proven is a handful of rules where **the BRD and the dashboard say different things**, or where the BRD says something that no example exercises. For each of those we implemented the reading we could defend, wrote down why, and made it changeable without redeploying. This document asks you to confirm or correct eleven of them.

**Three of them should be settled before scores are published to the organization.** They are in part 1. The rest are in parts 2 and 3 and have no deadline.

## How to read each item

| Field | What it is |
|---|---|
| **Question** | What we need you to answer |
| **Why it matters** | What changes in the numbers if the answer is different |
| **Evidence** | What the BRD and the dashboard actually say, so nobody has to take our word |
| **What we do today** | The rule the platform applies right now |
| **Options** | Every reading we considered |
| **Our recommendation** | What we would do, and why |
| **What we need from you** | The concrete thing that closes the item |

**Changing your mind is cheap.** Nine of the eleven rules live in a versioned rules file that an administrator edits from the portal: weights, bands, scores per case. Publishing a new version recalculates the whole portfolio, keeps the old version readable, and needs no deployment and no downtime. Only two items (the override rule and the aggregation level) would need code.

---

# Part 1 · Before scores are published

## 1. Composite metrics — are they a count-weighted average?

**Question.** Architectural Value and Product Strategy each summarize several product counts into one number. Is that number, in the Power BI model, the average of the category scores weighted by how many products fall in each category?

**Why it matters.** The two composites carry **20 of the 100 weight points** — half of the Architecture & Maturity pillar. If the strategy is wrong, most applications with products move a few points in that pillar.

**Evidence.**

- The **BRD weights table** gives a weight to each count separately: Architectural Value — Modern 5, Obsolete 1, Heritage 2, Not Approved 3 (sum 11); Product Strategy — Invest 4, Tolerate 1, Eliminate 2, Migrate 3 (sum 10).
- The **v1.3 dashboard** shows each composite as a single metric of weight 10.
- The **BRD worked example (EAI 14381)** uses the dashboard form: its Architecture denominator is 25 = RAW Tech 5 + Architectural Value 10 + Product Strategy 10.
- In that example, Architectural Value is **97.50** with 15 Modern and 1 Heritage products, and Product Strategy is **70.00** with 2 Invest, 1 Tolerate and 1 Eliminate. A count-weighted average of the category scores (Modern and Invest 100, Heritage and Tolerate 60, Obsolete, Not approved, Eliminate and Migrate 20) reproduces **both numbers exactly**.
- EAI 10969 on the dashboard shows composites of 75.72 and 88.37. No small set of product counts produces those values, so for that application the dashboard appears to receive the composite **already calculated**.

**What we do today.** Count-weighted average, weight 10 each. When a source delivers the composite already calculated, that value wins over the counts.

**Options.**

| | Option | Consequence |
|---|---|---|
| A | Count-weighted average (current) | Reproduces both documented cases |
| B | Per-count weights as in the BRD table | Changes the Architecture pillar weights and every score with composites |
| C | Always use the pre-calculated composite, ignore counts | Depends on CMT and MTS always delivering it |

**Our recommendation.** Option A. It is the only reading that reproduces the worked example from raw counts.

**What we need from you.**

1. The **DAX measure** of both composites from the Power BI model, so we can record it in the rules file as the source of truth.
2. **One more real application** with its product counts visible on the dashboard. We would add it as a third permanent regression test, so this never drifts again.
3. From CMT and MTS: do they deliver the composite pre-calculated, the counts, or both?

---

## 2. Tech Debt — is its weight 5 or 10?

**Question.** The BRD weights table says the Tech Debt Score weighs **5**. The v1.3 dashboard says **10**. Which one is the model?

**Why it matters.** Every application **with** Tech Debt data is affected. The two cases we use as proof do not settle it: **neither of them has Tech Debt data**, so both score identically with 5 or with 10. This is the one rule in the platform that is currently unverifiable against any published number.

**Evidence.**

- BRD weights table: Tech Debt 5.
- Dashboard v1.3: Tech Debt 10.
- EAI 14381 and EAI 10969 both exclude the metric for lack of data.
- If the weight becomes 5, five weight points have to go somewhere else in Architecture & Maturity, because the pillar has to keep totalling 40.

**What we do today.** Weight 10, the published dashboard weight.

**Options.**

| | Option | Consequence |
|---|---|---|
| A | Keep 10 (dashboard) | Current behaviour |
| B | Change to 5 (BRD table) | Frees 5 points that must be reassigned inside Architecture & Maturity — you would also have to say to which metric |

**Our recommendation.** Do not decide from the documents: **one dashboard capture of an application that has Tech Debt data, showing its Architecture pillar numerator and denominator, settles it numerically in five minutes.** We turn that application into a regression test and the question closes for good.

**What we need from you.** That capture, or read-access to one such application in the dashboard.

---

## 3. "Null or zero values are excluded" — does zero mean no data?

**Question.** The BRD's cross-cutting section says: *"Null or zero values are excluded from scoring."* Several metric tables, however, name zero explicitly as a real value. Which one wins, and where?

**Why it matters.** It changes the score of **every application that reports a zero**. The sharp case is Kenna: when a host reports 0, is that "no risk found" (the best possible score) or "never scanned" (no data at all)? Reading it one way gives the application the top score on an 8-point metric; the other way removes those 8 points from its denominator.

**Evidence.**

- Cross-cutting rule, BRD section "ALL Sections": *"Null or zero values are excluded from scoring. All resulting scores are normalized within a 20 to 100 range, and null values are not included in the normalization calculation."*
- But specific tables score zero on purpose: Tech Debt `0 → 100`; SCA `None → 100`; MFA `No (with no users) → 100`; DevSecOps `0/NULL–3 → 20`.

**What we do today.** The specific table wins over the general rule. Each metric declares whether zero means "no data": **yes** for Kenna, Open-Source Score, RAW Tech Score and Operational Score; **no** everywhere else. When a zero is excluded this way, the calculation screen says so in words, so nobody has to guess why a metric disappeared.

**Measured impact.** The two documented cases do not change. In the generated data, one application with RAW Tech Score 0 changes its score. With real data, applications with Kenna 0 lose the best host-risk score and their denominator drops by 8 points.

**Options.**

| | Option | Consequence |
|---|---|---|
| A | Specific table wins over the general rule (current) | Zero is data where the BRD scores it, and no-data where it does not |
| B | Exclude zero everywhere | Contradicts the Tech Debt table, where 0 is the best possible value |
| C | Never exclude zero | An unscanned host with Kenna 0 gets the best risk score |

**Our recommendation.** Option A — and in particular, tell us what a **Kenna 0** means in your source. If Power BI scores it, we publish a rules version flipping that one flag: no deployment, no code.

**What we need from you.** A written confirmation, and the reading of Kenna 0 and Open-Source Score 0 in the source systems.

---

# Part 2 · Changes numbers, does not block

## 4. The "Override rule" — when does a manual score replace a calculated one?

**Question.** The dashboard shows two values per pillar in some cases, under an "Override rule". When does an override apply, who can apply it, is it per pillar or global, and does it expire?

**Evidence.** The domain model already carries an override (value, reason, who applied it, when) and the effective score uses it. **No source delivers overrides and no screen creates them**, so today the feature is inert.

**Why it matters.** Until the rule is defined, the platform cannot reproduce any dashboard value that was produced by an override.

**Our recommendation.** Define the rule first, in one paragraph. The implementation would follow the pattern the rules editor already uses: its own role, a mandatory reason, an audit entry and a recent authentication required to apply it. The calculated score stays visible next to the override — an override should never hide the arithmetic.

**What we need from you.** The rule in writing. This is one of the two items that needs code.

## 5. SCA flaws — presence or count, and what about applications never scanned?

**Question.** Is "has at least one open critical flaw" the right reading, rather than "how many"? And is there a list of applications that have actually been scanned?

**Evidence.** The BRD maps severity to a score (High/Critical → 20, Medium → 20, Low → 60, None → 100), not a count. The source file has one row per flaw, so an application with **no open flaws** and an application **never scanned** look identical: both are absent.

**What we do today.** Presence per severity. An application that appears in the file at all (for example with closed flaws) counts as scanned; an application that never appears is treated as **no data** and the metric is excluded, instead of being rewarded with a perfect score.

**Our recommendation.** Keep presence. If application security can give us the list of scanned applications, we replace the heuristic with the fact.

**What we need from you (application security).** Confirmation, and the scanned-applications list if it exists.

## 6. Band gaps — is `0–59.9` meant to be `< 60`?

**Question.** Do the Power BI band conditions run edge to edge?

**Evidence.** As written in the BRD, Operational Score bands are 0–59.9, 60–79.99 and 80–100: a value of **59.95** belongs to no band. Tech Debt bands are 0, 0.1–2.4 and 2.5–5: **2.45** belongs to no band. Real Tech Debt values arrive in steps of 0.25, so they never land in a gap; Operational Score could.

**What we do today.** The engine refuses to guess: that one application fails with an explicit error, leaves the aggregates and shows the problem on its own screen. It does not silently pick a neighbouring band.

**Our recommendation.** Confirm the DAX conditions (almost certainly `< 60` and `>= 60`), and we publish a rules version with contiguous bands. No code, no deployment.

**What we need from you.** The DAX conditions of those two metrics.

## 7. Containerized — is the "2 + 2 points" text still alive?

**Question.** The BRD's Containerized section says *"this score is added to product strategy (2 pts) and architecture value (2 pts)"*, while the weights table shows Containerized with weight 4 **struck through**. Is that text a leftover from an older version?

**Evidence.** With Containerized, the BRD's Architecture table sums to 40 (RAW 5 + API 5 + Containerized 4 + Architectural Value 11 + Product Strategy 10 + Tech Debt 5). The dashboard reaches the same 40 with a different split (RAW 5, API 5, Architectural Value 10, Product Strategy 10, Tech Debt 10), and the worked example confirms the dashboard's split.

**What we do today.** Containerized has weight 0 and is shown but not scored; "No" does not score at all.

**Our recommendation.** Treat the sentence as superseded and remove it from the BRD. If instead a containerized application really should get a bonus inside the two composites, tell us and we model it.

**What we need from you.** Confirmation, and a BRD correction if it is indeed obsolete.

## 8. LOB and CIO scores — average of applications, or recomputed from raw values?

**Question.** When the portfolio shows a score for a LOB, a country or a CIO, is that the average of the application scores, or is it computed from the raw values added up across the group?

**Evidence.** The dashboard shows aggregates such as "Suma de Kenna_Score", which suggests some measures are aggregated before scoring.

**What we do today.** The average of application scores. It is explainable — you can click a group and see exactly which applications produce it — and consistent with the per-application breakdown.

**Our recommendation.** Keep the average. If the model aggregates raw values first, we need the rule written down, because the two approaches give different numbers for the same group.

**What we need from you.** Confirmation. This is the second item that needs code if the answer differs.

---

# Part 3 · Quick confirmations

These have no effect on today's numbers, or a very small one. A one-line answer each is enough.

| # | Question | Who answers | What we do today | If the answer differs |
|---|---|---|---|---|
| 9 | **Currency Score** — the dashboard shows one (94.66 in a capture) that the BRD never defines. What is it, and should the platform show it? | CMT owner | Not implemented; it is not part of the Modernization Score | We would add it as an independent KPI, outside the score |
| 10 | **MFA with unknown user count** — the score depends on whether the application has users. When we do not know, we assume it has them (the conservative security reading). Right? | Application security | Assume "with users", and say on screen that the count is unknown | One line in the rules file |
| 11 | **`Consolidate`** — the MAPS model uses it under "Eliminate", but the BRD disposition table does not list it. Is it valid, and which score does it map to? | Enterprise architecture | Mapped to 20 by assumption; weight 0, so it only drives the future plan | One line in the rules file |
| 12 | **"Monitoring" column** — the source has Agent, APM, Monitoring and Synthetics. In the sample, Monitoring is true whenever any other is. Is it a derived OR, or an independent capability? | Observability | Any of the four enabled scores 100 | No score change if it is derived |
| 13 | **DevSecOps `0/NULL–3 → 20`** — as written, a missing assessment would score 20 instead of being excluded. Only matters if the metric regains weight. | DevOps maturity | Metric has weight 0; a null is excluded | Decide when it regains weight |
| 14 | **Rows with EAI Code 0** in the OSS source — one row has EAI 0 with a score of 30. It is not an application. Fix at the source, or report it? | RPT data owner | Silently ignored; lineage does not flag it | We would report orphan rows per source, with a count |
| 15 | **The EAI 9768 example** in the BRD uses a scheme that does not match v1.3 and contradicts the current one. | BRD author | Ignored; we use EAI 14381 | Mark it as superseded in the BRD, so the next reader does not follow it |

---

# Part 4 · Data we would like (not decisions)

None of this blocks anything. The platform already answers the six portfolio questions — how healthy is it, what to modernize first, where to reduce complexity, where risk is concentrated, what drives obsolescence, where resilience is missing — with the sources we have today. These three would turn two of those answers from *indicative* into *real*.

| | What | Why it matters | How little would do |
|---|---|---|---|
| **A** | **Cost or effort per application** | "What to modernize first" ranks by value per week. The only effort in today's data is the duration of each roadmap, which is defined **per roadmap, not per application**: two applications on the same roadmap show the same weeks. It is a proxy, and the screen says so | A t-shirt size — S / M / L — per application. This is the single cheapest input with the largest effect on the ranking |
| **B** | **An "application class" column** | The business asked the complexity question as *"more than X applications with the same application class per country"*. No source has that column, so we group by the primary business capability from the EAI master, which is the closest thing that exists | One column per application, if the attribute lives in some repository already |
| **C** | **Business value** | Today we weigh business importance with three attributes we do have: critical indicator, user count and number of capabilities covered. Revenue supported or transactions per month would replace the proxy with the real thing | Any measure the business considers fair, even a tier |

---

# Decision log

To be filled in during the conversation. Items 1 to 3 are the ones that should not stay open.

| # | Item | Decision | Who | Date |
|---|---|---|---|---|
| 1 | Composite metrics strategy | | | |
| 2 | Tech Debt weight | | | |
| 3 | Zero excluded from scoring | | | |
| 4 | Override rule | | | |
| 5 | SCA presence vs count | | | |
| 6 | Band gaps | | | |
| 7 | Containerized 2 + 2 points | | | |
| 8 | Aggregation level | | | |
| 9–15 | Quick confirmations | | | |

---

## What happens after the meeting

| Type of answer | What we do | How long |
|---|---|---|
| Confirms what we implemented | Record it in the rules file as a traceability note, so the next reader sees the decision and its date | Same day |
| Changes a weight, a band or a score | An administrator publishes a new rules version from the portal. The whole portfolio recalculates, the previous version stays readable, and both can be compared application by application | Same day, no deployment |
| Changes the override rule or the aggregation level | Code change, with its tests | One to two weeks |
| Provides a new golden case (item 2) | It becomes a permanent regression test: if any future change moves that number, the build fails | Same day |

The full backlog, with every open item across scoring, platform, security, technical debt and operations, is in `docs/PENDING-ITEMS.md`.
