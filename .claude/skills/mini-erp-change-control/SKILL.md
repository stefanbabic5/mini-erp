---
name: mini-erp-change-control
description: >
  Change control for the mini-erp repository. Load this BEFORE creating or naming a
  branch, proposing a commit, pushing, opening a pull request, writing a PR
  description, when CI is red, after a PR is merged or when cleaning up branches,
  adding or bumping a NuGet
  package, container image or tool,
  changing a business rule (BR-xx), a permission, a table shape or a migration, or
  deciding whether a change needs an ADR. Covers the non-negotiable rules with the
  reason behind each (never commit to main, no commit/push/PR without Stefan's
  go-ahead, free dependencies only per ADR-0007, decisions are written down, evidence
  over claims), the change classification table, branch prefixes, the PR description
  shape used by PRs #1-#9, the licence check for a new dependency, and the
  planted-bug check. Does not cover how to write the code (mini-erp-add-feature), how
  to test it (mini-erp-testing-and-qa) or how to write an ADR
  (mini-erp-docs-and-writing).
---

# mini-erp change control

How a change is classified, gated and shipped in **mini-erp**, a public, MIT-licensed,
multi-tenant ERP backend on .NET 10 (https://github.com/stefanbabic5/mini-erp). One
maintainer, **Stefan (stefanbabic5)**, reviews and merges every pull request himself.
The source of the rules is `CLAUDE.md` ("Working agreements"); this skill adds the
reasons, the checklists and the traps. One thing below is **practice seen in the
history, not text in `CLAUDE.md`**: the planted-bug check (§6).

Two facts shape everything below (verified 2026-11-15):

1. **Nothing on GitHub enforces the rules.** `main` has no branch protection
   (`gh api repos/stefanbabic5/mini-erp/branches/main/protection` answers "Branch not
   protected"). A red CI run does not block a merge, and a direct push to `main` would
   succeed. Discipline is the gate. (What GitHub does enforce since 2026-11-15: a PR
   can only be squash-merged, and its branch is deleted after the merge.)
2. **The repository is public and claims only what it proves** (working agreement 4).
   A change that makes a document say more than the code proves is a defect, even if
   every test passes.

Terms used below, defined once:

- **ADR**: architecture decision record, a short file in `docs/adr/` (context, decision,
  rejected alternatives, consequences).
- **BR-xx**: a business rule id from `docs/specification.md` §6.
- **OSI licence**: an open-source licence approved by the Open Source Initiative (MIT,
  Apache-2.0, BSD, PostgreSQL).
- **Squash merge**: GitHub merges a PR as one commit on `main`. Here that commit takes
  the PR's title as its subject and the PR's description as its body.
- **Planted-bug check**: breaking the code on purpose to see the new tests fail, then
  restoring it (§6).

## 1. The non-negotiable rules

Do not relax any of these unless Stefan says so in the current conversation.

| # | Rule | Why |
|---|---|---|
| 1 | Never commit to `main`. Work on a branch. | `main` is what everyone who clones the repository gets; every change must arrive through a PR Stefan has read. Nothing on GitHub prevents a direct push, so this rule is the only guard. |
| 2 | Never commit, push, create a remote or open a PR without Stefan's explicit go-ahead **in the current conversation**. | Stefan must be able to explain every line (working agreement 7). An approval given for one change does not carry over to the next. |
| 3 | Free dependencies only ([ADR-0007](../../../docs/adr/0007-free-dependencies-only.md)). Never add MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+, NServiceBus or a paid cloud service. | The project has no budget, and several popular .NET libraries went commercial in 2025. A pinned major version cannot become paid later. |
| 4 | A significant decision gets an ADR; an `Accepted` ADR is never rewritten. | The ADRs record why, including the alternatives that were rejected. Rewriting one loses that history; a changed decision is a new ADR. |
| 5 | A test that proves a business rule carries the rule id in its name (`BR13_...`). A new rule gets a new id; ids are never reused. | Any rule can be traced to its proof with one search. |
| 6 | Never say something works without running it. Report results as they are. | "Evidence over claims" is working agreement 4. A PR description that says "121 passed" must come from a real run. |
| 7 | Everything in the repository is in English. | Code, comments, documents, commit messages, PR text. (Conversation with Stefan is in Serbian; that is not repository content.) |
| 8 | Commit messages, PR descriptions, code and documents contain no AI attribution: no `Co-Authored-By` trailers, no "Generated with" footers, no "assisted by" tags. | `CLAUDE.md`, working agreement 5. With squash merges the PR description becomes the commit body on `main`, so a footer in a PR would land in the history. |

## 2. Classify the change first

| Class | Examples | What it needs before the PR |
|---|---|---|
| **Docs only** | README wording, a specification clarification, a skill (never the text of an `Accepted` ADR) | Links and commands in the changed text re-checked. Still a branch and a PR. |
| **Feature inside the conventions** | a new endpoint in an existing module, built like `src/Modules/MiniErp.Catalog/Items/` | Tests per mini-erp-testing-and-qa (including the cross-tenant test), specification updated in the same change, `dotnet build` and `dotnet test` green, planted-bug check. |
| **New dependency or major version bump** | a NuGet package, a container image, a local tool | The licence check in §5, a row in the PR's "New packages and licences" table, and the licence written next to it: a comment in `Directory.Packages.props` (package) or `compose.yaml` (image), or in parentheses on the `dotnet-tools.json` line of `CLAUDE.md`'s "Repository layout" (local tool). **An ADR only when** the dependency changes the architecture (as EF Core or Keycloak did) or is an exception to ADR-0007 (as ADR-0010 is) (`CLAUDE.md`, working agreements 1 and 2). If unsure which, ask Stefan. |
| **Decision** | a new pattern, a data-shape choice, a trade-off, anything that changes or contradicts an accepted ADR | Options and a recommendation to Stefan first. Then a new ADR (`Proposed`, then `Accepted` by Stefan). If it replaces an old one, the old one's status line becomes `Superseded by ADR-XXXX`; its text stays. How to write it: mini-erp-docs-and-writing §3. |
| **Business rule change** | a new BR, a changed BR, a new permission or role mapping | Stefan's agreement on the rule before coding it (a changed rule changes behaviour). Specification §6 (or §2 for permissions) updated in the same change; a test with the id in its name. A permission touches four places: mini-erp-add-feature §7. |
| **Schema change** | a new table, column, index, or changed mapping | A new migration for that module's DbContext (never edit a migration that is already on `main`), the model-and-migrations check clean. Commands: mini-erp-add-feature §4. |
| **Tenant or security surface** | anything touching `ModuleDbContext`, `TenantSaveChangesInterceptor`, `SecurityRegistration`, `IgnoreQueryFilters`, raw SQL, `AllowAnonymous` | All of the above for its class, plus: Stefan's agreement **before** writing it (§7), and a line at the top of the PR description. A data leak between tenants is the worst bug this system can have (BR-13). |

When a change has a real design choice in it, present the options with a recommendation
to Stefan instead of choosing silently (working agreement 7).

## 3. Branches

Allowed prefixes (`CLAUDE.md`): `feature/`, `fix/`, `docs/`, `test/`, `chore/`; PRs
#1-#9 used `feature/`, `docs/` and `chore/`. Names are short
and kebab-case: `feature/catalog-items`, `chore/phase-1-wrap-up`,
`docs/initial-specification`.

```bash
git switch main
git pull
git switch -c feature/<short-name>
```

Creating a local branch is not a commit and needs no approval. Everything after it
(commit, push, PR) does.

**Stacked branches.** PRs #3 and #9 were built on top of an unmerged earlier branch.
Their descriptions start with a quoted note saying which PR to merge first. Do the same
whenever a branch contains another open PR's commits.

## 4. Commit and pull request

Before asking Stefan for the go-ahead:

```bash
dotnet build --configuration Release
dotnet test --configuration Release   # Docker must be running; see mini-erp-build-and-run
git status --short                    # only files you meant to change
git diff --stat                       # what Stefan will be shown
```

Use the Release configuration here because the PR's Testing section reports Release
results (as CI runs them); report only what you ran.

Then show Stefan what will be committed (files, the commit message) and wait for a yes.
After his yes, and only then (a second yes is needed for the push and the PR unless he
gave it together):

```bash
git add <the files you changed>
git commit -m "<message>"
git push -u origin <branch>
gh pr create --base main --title "<message>" --body-file <file with the description>
```

- **Commit message:** an imperative English subject line, as in the history:
  `Add Catalog items with validation, ETags and tenant isolation`. A short body
  paragraph saying why is fine (three commits on `main` have one).
- **PR title:** the same line.
- **PR description**, the shape of the feature PRs #3-#8 (headings vary slightly
  between them; PR #9, a docs PR, lists acceptance criteria and a fresh-clone check
  instead):

```markdown
## What
Phase N, step M: <one line>.
- **<Thing>**: what was added and the rule or ADR it implements.

## New packages and licences
| Package | Version | Licence |
|---|---|---|
(or the sentence "No new packages.")

## Testing
- `dotnet build -c Release`: <real result>. `dotnet test -c Release`: **<n> passed** (<m> new).
- What the new tests prove, by rule id.
- Checked against a planted bug: <what was broken, which tests failed>; restored, all pass.
- Manual checks, if any, against the real stack.
```

**Merging is Stefan's.**

- Never merge a PR and never enable auto-merge.
- Since 2026-11-15 the repository allows **only squash merges** (merge commits and
  rebase merges are switched off), the squash commit takes the PR's title and
  description, and GitHub deletes the branch after the merge. History: PRs #1-#10 went
  in as merge commits (`Merge pull request #10 from ...`), before the switch.
- After a merge, Stefan says so. Then, when he asks for the clean-up:
  1. `git switch main` and `git pull`.
  2. Check the branch has nothing that is not on `main`. After a squash merge
     `git log main..<branch>` always lists the branch's commits (they were collapsed into
     a new one), so compare the content instead: `git diff <branch> main` must be empty
     for the files the PR touched, and no commit on the branch may be newer than the
     merge. If anything is left over, stop and tell Stefan.
  3. `git branch -D <branch>` (`-d` refuses, because Git does not see a squash as a
     merge).
  4. `git push origin --delete <branch>` only if the remote branch still exists (GitHub
     normally deleted it already); `git fetch --prune`.

**CI.** One job, `build-and-test`, runs on every push to `main` and every PR to `main`
(its commands: mini-erp-build-and-run §5). Check it with `gh pr checks <number>`. A red
run is a blocker even though GitHub would allow the merge: reproduce it locally, fix
it on the same branch, and tell Stefan.

## 5. Adding a dependency: the licence check

1. Find the licence of the **version you will pin**, at the source: the package's NuGet
   page and its repository's `LICENSE` file. Do not rely on memory; licences changed for
   several libraries in 2025.
2. Allowed: MIT, Apache-2.0, BSD, PostgreSQL licence, or a free tier with no payment and
   no card. A "free for individuals" licence is allowed **only when no OSI-licensed
   option is reasonable, and then only with its own ADR**
   ([ADR-0010](../../../docs/adr/0010-docker-desktop-for-local-containers.md) is the one
   existing example).
3. Tell Stefan the package, version and licence when proposing it. Wait for agreement.
4. Write the licence next to it:
   - **package**: the version in `Directory.Packages.props` in the right `ItemGroup`,
     the licence in a comment above it; the project file references it **without** a
     version (central package management);
   - **container image**: a comment with the licence above the `image:` line in
     `compose.yaml` (and the same image in `ApiFactory.cs` if it is PostgreSQL);
   - **local tool**: `dotnet-tools.json` cannot hold comments, so add the licence in
     parentheses on the `dotnet-tools.json` line of `CLAUDE.md`'s "Repository layout"
     (`local tools: dotnet-ef (MIT)`).
5. Keep the package on the major version that matches `net10.0`. A minor or patch
   bump inside the pinned major needs no licence check (`CLAUDE.md` asks for one when
   adding a package or bumping a major), but it is still a PR and is named in it.
6. List it in the PR's "New packages and licences" table (packages, images and tools).
7. Restore must stay clean: NuGet Audit checks every package, transitive ones
   included, on each restore, and a known vulnerability fails restore and build
   (`error NU190x`). Do not silence it with `NoWarn`, `WarningsNotAsErrors` or
   `NuGetAuditSuppress` without Stefan's agreement; upgrade instead
   (mini-erp-build-and-run §7).
8. A cloud-specific service is allowed only behind an interface that has a local
   implementation running in Docker.

Hand-written code replaces the banned libraries: plain handler methods instead of a
mediator, explicit mapping methods (`ItemResponse.From`) instead of a mapper, Shouldly
or plain xUnit assertions.

## 6. The planted-bug check

PRs #3, #6, #7 and #8 report one (#4 and #5 do not). It is not written in `CLAUDE.md`;
it is the practice that makes "evidence over claims" concrete, so do it for every
change that adds behaviour. It proves the new tests can fail.

1. With all tests green, break the behaviour the tests should guard (PR #7 removed the
   version check in the update; PR #8 ignored the PIB check digit; PR #6 disabled the
   query filter and the write guard).
2. Run `dotnet test`. The tests that name that behaviour must fail, and ideally only
   those.
3. Restore the code (`git diff` must be empty for the files you broke) and run
   `dotnet test` again: all green.
4. Report it in the PR in one line: what was broken, how many tests failed.

If nothing fails, the tests do not prove the rule. Fix the tests before the PR.

## 7. What a session must never do on its own

- Commit, push, open or merge a PR, or delete a branch without Stefan's go-ahead.
- Edit a migration that is already on `main`, or an `Accepted` ADR's decision.
- Add `IgnoreQueryFilters()`, raw SQL or `AllowAnonymous()` without Stefan's agreement
  **before** writing it. This is the one gate for bypassing tenant isolation or
  authorization; the other skills point here. Raw SQL that touches tenant data always
  carries a tenant condition (ADR-0003); that part is not negotiable.
- Remove or weaken a test to make a change pass.
- Raise a number in the README (test counts, claims) without a run that shows it. See
  mini-erp-claims-and-evidence §1.
- Change a business rule, a permission mapping or other behaviour silently: agree it
  with Stefan first (working agreement 7).

## When NOT to use this skill

| You need | Use |
|---|---|
| How to build a feature, endpoint, DbContext or migration | mini-erp-add-feature |
| Which tests a change needs and how to write them | mini-erp-testing-and-qa |
| The ADR template, the specification's structure, house style | mini-erp-docs-and-writing |
| Why the architecture is the way it is | mini-erp-architecture-contract |
| What the README or another document may claim | mini-erp-claims-and-evidence |

## Provenance and maintenance

Written 2026-11-15 from `CLAUDE.md`, ADR-0007, ADR-0010, `.github/workflows/ci.yml` and
PRs #1-#9. Re-verify what may drift:

```bash
gh api repos/stefanbabic5/mini-erp/branches/main/protection   # "Branch not protected" as of 2026-11-15
git log --oneline -12 main                                    # merge commits up to PR #10, squash commits after it
gh api repos/stefanbabic5/mini-erp --jq '{allow_squash_merge,allow_merge_commit,allow_rebase_merge,delete_branch_on_merge,squash_merge_commit_title,squash_merge_commit_message}'
gh pr list --state merged --limit 20                          # branch prefixes in use
cat .github/workflows/ci.yml                                  # CI steps and triggers
grep -n "Version=" Directory.Packages.props                   # pinned packages
```

If `CLAUDE.md` and this skill disagree, `CLAUDE.md` wins; fix the skill.
