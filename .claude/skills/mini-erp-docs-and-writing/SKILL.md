---
name: mini-erp-docs-and-writing
description: >
  The documents of record in mini-erp and how to keep them true. Load this when
  editing README.md, CLAUDE.md, docs/specification.md, anything in docs/adr/, or a
  skill under .claude/skills/; when writing a new ADR or superseding one (whether a
  change needs an ADR is decided in mini-erp-change-control); when adding or changing
  the text of a business rule (BR-xx), a permission row, an entity in the domain
  model or a phase's status; when you need the next free ADR number or BR id; when a
  code change makes a document wrong and you must find every place to update; when
  writing code comments, XML doc comments, commit messages or PR text and need the
  house style; and when asked "where is this documented" or "which document wins".
  Covers the document registry, the code-change-to-document matrix, the ADR
  lifecycle with a skeleton, the specification's rules, the house style, and the
  known places where documents and code disagree. What the README may claim:
  mini-erp-portfolio-claims.
---

# mini-erp documents and writing

In **mini-erp** the documents are part of the product: a reader (a recruiter, an
interviewer, a later session) must be able to trust them without reading the code. A
document that says more than the code does is a defect.

Working agreements this skill implements (`CLAUDE.md`): **2. Decisions are written
down**, **3. Business rules are traceable**, **6. Everything in the repository is in
English**.

Terms, defined once:

- **ADR**: architecture decision record, one file per decision in `docs/adr/`.
- **Supersede**: replace an accepted decision with a new ADR; the old file stays.
- **Living document**: a document updated in the same change as the behaviour it
  describes (the specification, the README, `CLAUDE.md`).

State on 2026-11-15: phase 1 is complete; ten ADRs, all `Accepted`; business rules
BR-01 to BR-16.

## 1. Registry: the documents of record

| Document | Purpose | Reader | Update when |
|---|---|---|---|
| `README.md` | what the project is, what it shows, how to run it, what works today, test counts, roadmap | a visitor with five minutes | a feature lands, a count changes, a phase's status changes. Only claims with proof (mini-erp-portfolio-claims) |
| `docs/specification.md` | the intended behaviour of all four phases (its sections: §4 below) | someone who needs the intended behaviour | behaviour, a rule, a permission, an entity, a phase's scope or status changes |
| `docs/adr/NNNN-*.md` | one significant decision each, with rejected alternatives | someone asking "why this way" | a new decision is taken; never to change an accepted one |
| `docs/adr/README.md` | the ADR index, status values and template | | an ADR is added or changes status |
| `CLAUDE.md` | working agreements, architecture in one screen, endpoint conventions, layout, commands, URLs | an AI session or a new contributor | a rule, a convention, the layout, a command or a URL changes |
| `.claude/skills/*/SKILL.md` | runbooks that add the how, the checklists and the traps | a smaller model or a mid-level developer | a fact a skill states stops being true (each skill ends with re-verification commands) |
| `Directory.Packages.props` | every package version with its licence in a comment | | a package is added or bumped |
| PR descriptions | what changed, new packages and licences, test evidence | Stefan at review; later, the history | every PR (shape in mini-erp-change-control §4) |
| Code comments and XML doc comments | why, not what; the rule or ADR a type implements | the reader of the file | the code they describe changes |

**When two disagree** (guidance of this skill; `CLAUDE.md` does not rank the
documents): the code and its tests show what *is*; the specification says what is
*intended*; an accepted ADR says what was *decided*. A disagreement is a defect in one
of them. Do not pick silently: tell Stefan which two disagree and propose which to
change. A skill never overrides `CLAUDE.md`, the specification or an ADR.

## 2. When code changes, which documents change

| Change | Update in the same PR |
|---|---|
| New endpoint or feature | `README.md` "What works today"; specification §4 if an entity or field is new; `CLAUDE.md` only if a convention is new |
| New or changed business rule | specification §6 (new id, never reused); the test name carries the id |
| New permission or role mapping | specification §2 table (it must equal `RolePermissions` and `PermissionMatrixTests.Matrix`) |
| New package, image or tool | `Directory.Packages.props` licence comment; PR table; specification §10 if it is a technology choice; an ADR if it is a design decision or an ADR-0007 exception |
| New module, project or top-level file | `CLAUDE.md` "Repository layout"; `README.md` architecture diagram if the picture changes |
| New command, port or URL | `CLAUDE.md` "Commands" and its URL table; `README.md` quick start if a visitor needs it |
| Test count changes | `README.md` "Tests" (both the total and the per-project table), after a run that shows it |
| A phase finishes | specification §8 (dated "Done" note, as phase 1 has); `README.md` status line and roadmap; `CLAUDE.md` status line |
| A decision changes | a new ADR; old ADR's status line; `docs/adr/README.md` index; every document that described the old decision |
| A question is settled | specification §11 (a row; the decision date is in the column header), removed from §12 |
| A skill is added, renamed or removed | `CLAUDE.md` "Skill library" (the inventory and the planned list) |
| A fact in a skill changes | that skill, including its dated numbers |

After editing, search for the old fact everywhere, because the same number or name
often sits in several documents:

```bash
git grep -n "121 tests\|121 passed\|\*\*121" -- README.md CLAUDE.md docs .claude/skills
```

## 3. ADRs

**Does this need an ADR?** That gate is owned by mini-erp-change-control §2 (rows
"Decision" and "New dependency"). In short: a pattern others will copy, a data-shape
choice, a trade-off with a real alternative, a new kind of dependency, or anything
that contradicts an accepted ADR needs one; a change that follows an existing ADR or
convention (a new endpoint built like items) does not. This section is about writing
it.

**Lifecycle.** `Proposed` → `Accepted` → (`Superseded by ADR-XXXX` | `Deprecated`).

1. Take the next number: `ls docs/adr/` (0010 is the latest on 2026-11-15). File name:
   `NNNN-short-kebab-title.md`.
2. Write it with status `Proposed` and today's date. Present the options and a
   recommendation to Stefan; he decides.
3. On his agreement, change the status to `Accepted` and add the row to the index in
   `docs/adr/README.md`.
4. **An `Accepted` ADR is never rewritten**, not even to fix its prose (no accepted
   ADR's text has been edited in this repository's history). To change a decision,
   write a new ADR whose Context explains what changed, and edit only the old ADR's
   status line to `Superseded by ADR-XXXX` (and the index).

Skeleton, in the form the existing ADRs actually use (the template in
`docs/adr/README.md` is the minimum):

```markdown
# ADR-0011: <Decision as a short statement>

**Status:** Proposed · **Date:** 2026-MM-DD

## Context
<The forces: which business rule or requirement, what makes a decision necessary now.
Refer to rules and other ADRs by id: (BR-02), [ADR-0002](0002-postgresql-and-ef-core.md).>

## Decision
- <What will be done, stated plainly, as bullets.>
- **Proof:** <the test or measurement that will show it works, if it is a correctness decision.>

## Alternatives considered
- **<Option>**: <why it was rejected, honestly; say when it would be the better choice.>

## Consequences
- <What becomes easier, what becomes harder, what must now be watched.>
```

What makes the existing ADRs good, and a new one must match: the rejected alternatives
are real options with a fair reason; consequences include the costs; a correctness
decision names its proof (ADR-0008 and ADR-0009 both do); links to other ADRs are
relative (`0007-free-dependencies-only.md`), to the specification `../specification.md`.

An ADR that is an exception to another says so in its Decision and in its index row,
as ADR-0010 does ("This is an exception to ADR-0007, recorded here").

## 4. The specification

- Header line: `**Status:** draft vX.Y · **Owner:** Stefan · **Last updated:** YYYY-MM-DD`.
  Update the date with every change.
- **Business rules (§6)**: one row per rule, id `BR-NN` in bold, one or two sentences
  stating the rule so that a test could prove it. The next free id on 2026-11-15 is
  BR-17. Ids are never reused or renumbered, even if a rule is dropped.
- **Permissions (§2)**: the table is the contract; `PermissionMatrixTests` holds the
  same table in code.
- **Domain model (§4)**: field lists per entity. It describes all phases, so many
  entities there do not exist in code yet. Do not read it as a description of the code.
- Sections: §1 purpose, goals and non-goals; §2 roles and permissions; §3 modules; §4
  domain model; §5 sales order lifecycle; §6 business rules; §7 API conventions; §8
  phases; §9 non-functional requirements; §10 technology; §11 resolved questions; §12
  open questions.
- **Phases (§8)**: scope and acceptance criteria per phase. A phase is marked done with
  a dated sentence saying how the criteria were checked.
- **Module table (§3)** has a "BC equivalent" column; keep it filled for new concepts.
- Section references elsewhere use `§N` ("specification §6").

## 5. README

Sections, in order: status note, "What it shows", "Quick start", "What works today",
"Architecture" (Mermaid diagram plus ADR bullets), "Tests", "Documentation", "Roadmap",
"License". The status note ends with the sentence "This README only claims what the
code and its tests show." Keep that sentence true; mini-erp-portfolio-claims has the
claim-to-proof table.

The quick start must work from a fresh clone, copied as written. After changing it, run
it (PR #9 did this in an empty folder with its own compose project).

## 6. House style

Language and tone:

- English everywhere in the repository. Serbian business terms appear once, with the
  English meaning: "PIB (tax id)", "matični broj (registration number)".
- Plain, direct sentences. State what is, then why. No marketing words ("robust",
  "seamless", "powerful"), no hedging where the fact is known.
- Present tense for what exists; "planned", "phase N" or "not built yet" for what does
  not.
- British spelling in prose, as the existing documents use: "licence" (noun),
  "behaviour", "optimise", "serialise". Identifiers and library names keep their own
  spelling.
- Refer to rules and decisions by id: `BR-13`, `ADR-0003`. In test names the id has no
  dash (`BR13_`).
- Explain a term the first time it appears in a document; give the Business Central
  equivalent where it helps a reader from that world.

Formatting:

- Markdown tables for anything with two or more attributes per item.
- Code, paths, commands, routes, claims and setting names in backticks.
- Relative links between documents (`docs/adr/0003-multi-tenancy.md` from the root,
  `../specification.md` from an ADR).
- Dates as `YYYY-MM-DD`. Volatile numbers (test counts, versions) carry a date or sit
  next to the command that reproduces them.
- Bold for the key phrase of a bullet, not for emphasis in running text.
- The existing documents do not use the em dash; use a colon, a comma or two sentences.

Code comments:

- A type that carries a rule, a decision or a non-obvious job has an XML doc comment
  (`/// <summary>`) saying what it is for and which rule or ADR it implements:
  "(ADR-0003)", "(BR-15)", "BC equivalent: No. Series". Most types have one; plain
  request and response records carry an ordinary `//` comment per file instead.
- Inline comments explain **why**: the reason for an order of operations, a trap, a
  deliberate omission. See the comment above the raw SQL in `NumberSeriesService.cs`
  and above `SaveChangesAsync` in `ItemEndpoints.UpdateAsync`.
- No commented-out code, no TODO left without Stefan knowing about it (there are none
  in the repository on 2026-11-15).

Commit messages and PR titles: an imperative English subject line, no trailing
period, no prefix: `Add Sales customers with PIB validation and customer numbers`. The
rest of the commit and PR shape is in mini-erp-change-control §4.

## 7. Known disagreements between documents and code (2026-11-15)

Do not copy these statements into new text as if they were true. Fixing one is a
normal docs change that Stefan decides on.

| Document says | Reality |
|---|---|
| Specification §9: "Dependencies are scanned in CI" | `.github/workflows/ci.yml` has restore, build and test only |
| Specification §7: "`.http` files in the repo cover the main flows" | there are no `.http` files |
| Specification §9: "Business rules are covered by unit tests on the domain" | there is no unit test project; §10 lists NSubstitute, which is not a dependency |
| Specification §9: "Structured logs carry the trace id, tenant id and user id" | nothing adds tenant or user id to the logs |
| Specification §8, phase 1: "`docker compose up` starts the API, ..." | compose starts PostgreSQL, Keycloak and the dashboard; the API runs with `dotnet run` |
| README: "Every endpoint has a test that tries to reach another tenant's data and gets 404" | true for item and customer endpoints; number-series endpoints are keyed by code and have a different isolation test |
| `CLAUDE.md`: "Work lands through pull requests, squash-merged into `main`" | `git log` shows PRs #1-#9 as merge commits |
| `CLAUDE.md` working agreement 2: "a new dependency" is a significant decision that gets an ADR | PRs #3-#6 added about fifteen packages with a licence table and no new ADR; only Docker Desktop got one (ADR-0010). The gate as practised: mini-erp-change-control §2 |
| `CLAUDE.md` and README: a module exposes only its registration class and its `Contracts` namespace | EF Core's generated `MiniErp.<Name>.Migrations` classes are public too; the architecture test allows exactly that exception (PR #7) |
| `CLAUDE.md` "Endpoint conventions": the last five bullets (inventory ledger, outbox, Keycloak, security, stock reservation) | they are architecture bullets that belong under "Architecture in one screen"; the content is right |

Code weaknesses that no document contradicts (block and unblock without a version
check, and so on) are listed in mini-erp-architecture-contract §7, not here.

## 8. Editing a skill

- A skill adds what `CLAUDE.md` and the ADRs do not say: steps, checklists, traps,
  proofs. It links to them instead of copying them.
- One home per fact; other skills point to it by name and section.
- Verify every command, path, type name and test name against the repository before
  writing it, and run what can be run. Mark anything not run as not run.
- Date volatile facts; keep the "Provenance and maintenance" section's commands working.
- Never cite a file outside the repository.

## When NOT to use this skill

| You need | Use |
|---|---|
| Whether a README or CV sentence is allowed | mini-erp-portfolio-claims |
| Branch, commit, PR and dependency gates | mini-erp-change-control |
| The content of the architecture, not how to document it | mini-erp-architecture-contract |
| Commands that belong in a document, verified | mini-erp-build-and-run |

## Provenance and maintenance

Written 2026-11-15 from `README.md`, `CLAUDE.md`, `docs/specification.md`, the ten ADRs
and PRs #1-#9. Re-verify what may drift:

```bash
ls docs/adr/                                         # latest ADR number
grep -n "^| \*\*BR-" docs/specification.md | tail -1  # latest business rule id
grep -n "^## " README.md CLAUDE.md                   # section lists in §5 and §2
grep -n "Status:" docs/specification.md docs/adr/*.md # statuses and dates
git grep -n "—" -- README.md CLAUDE.md docs          # em dashes (only the ADR template has one)
ls .github/workflows/ ; git ls-files "*.http"        # §7: dependency scan, .http files
```
