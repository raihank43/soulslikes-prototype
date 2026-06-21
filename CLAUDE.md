<!-- growing-docs template v1.11.0 — stamped at scaffold/upgrade time; /project-adopt reads this to upgrade precisely. Keep this line. -->
# SoulslikePrototype — Project Instructions for Claude

> **Documentation system:** durable project knowledge lives in `docs/` (see the Project Artifacts Index below) — this file stays lean so it can be re-read in full after compaction. Two workflows nest: the **per-change workflow** is the micro loop for any code change; the **daily workflow** is the macro rhythm for the day-by-day build plan.

## Project context

A 3D soulslike action game built in **Unity 6.4** using URP.

Combat draws from:

- **Dark Souls** — stamina-gated movement, dodge rolls with i-frames, lock-on targeting.
- **Sekiro** — deflect/parry windows, posture system, deathblows on staggered enemies.

Scope is intentionally focused: interconnected hand-crafted areas (DS1-style), not open world. The current goal is a vertical slice — one arena, one enemy, one full combat loop — before any expansion.

The full day-by-day build plan lives at **`docs/day-1-to-7-plan.md`** (each day: goal + acceptance test, assets to gather, a starting plan-mode prompt). Read the relevant day before starting any "Day N" work.

---

## Workflow — Follow This For Every Change

Applies to every request that **changes the project** — major feature, one-line fix, refactor, bug fix. Read-only requests (explaining code, exploring) skip it. This is the per-change loop; it runs *inside* the daily ritual below.

### Step 1: Read Before You Work
1. Read this file.
2. Open `docs/PLAN.md` — check the **Project Phase** marker (currently `BUILDING`) and read **Current Focus** (the cold-start brief: just-shipped / in-flight / next + "Start here" docs).
3. Use PLAN.md's **Features table as your map** — open the doc in the `Doc` column for the feature you're touching, instead of globbing `docs/`.
4. Check `docs/RULES.md` for conventions and anti-patterns.

### Step 2: Do The Work
- Follow `docs/RULES.md` conventions and `docs/ARCHITECTURE.md` patterns.
- Write down any gotcha or non-obvious behavior in the relevant `docs/feature-*.md` immediately.
- **Code is the source of truth.** If a doc contradicts the code, the code wins — fix the doc. Precedence: code → `feature-*.md` → `ARCHITECTURE.md`/`PLAN.md` → `README.md`.

### Step 3: Update Documentation (BEFORE committing)
Actively decide for each — don't skip the decision:
- [ ] **Feature doc** in `docs/` — new one for a new feature, update for a modified one.
- [ ] **`docs/PLAN.md`** — feature status change? new decision to log? Current Focus stale?
- [ ] **`README.md`** — would a first-time reader need to know what changed?
- [ ] **`docs/ARCHITECTURE.md`** — did structure, data flow, or stack change?
- [ ] **`docs/RULES.md`** — new convention or anti-pattern discovered?

Refresh the `Last updated:` line (date or short commit SHA) on any doc you touch that carries one.

### Step 4: Verify It Works
Refresh Unity via MCP, check the console for compile errors, and sanity-check/playtest the change before committing. Don't commit blind.

### Step 5: Commit and Push
- Never stage secrets. Show `git status` + `git diff --stat` and wait for confirmation before committing (project rule).
- Stage all changes including doc updates; write a clear `type(scope): description` message.
- Push only if a remote is configured (this repo: `origin`).

### When You Learn Something Cross-Cutting
- About the **project's code** (convention, pattern, anti-pattern) → `docs/RULES.md`.
- About the **user or how you work together** (preferences, working style, external systems) → Claude Code memory (outside the repo, persists across projects).

---

## Daily workflow

The macro rhythm for the day-by-day plan. These are trigger phrases — when I use them, follow the exact sequence.

### When I say "let's start Day N"
1. Read this CLAUDE.md fully.
2. Read the relevant day section in `docs/day-1-to-7-plan.md`.
3. Confirm what we're building today and ask any clarifying questions.
4. Enter plan mode for the day's feature. **Do not write code until I approve the plan.**

### When I say "wrap up the day"
1. Run `git status` and `git diff --stat`. Show me the output.
2. Run the Step 3 doc checklist for what we built — update the relevant `docs/feature-*.md`, `docs/PLAN.md` (status + Current Focus + decisions), and any of `ARCHITECTURE.md`/`RULES.md`/`README.md` that changed. Focus on non-obvious decisions and *why*, gotchas + fixes, and component contracts other features depend on — skip restating what the code already says.
3. Wait for me to review and edit the proposed doc changes.
4. After docs are saved, propose a commit message and run the commit.
5. Suggest 2–3 things I should playtest before tomorrow to catch regressions.

---

## Project Artifacts Index

| File | Purpose |
|------|---------|
| `CLAUDE.md` | This file — the lean system prompt: summary, both workflows, artifact index |
| `README.md` | Human-readable project overview |
| `docs/PLAN.md` | Phase marker, Current Focus, roadmap, feature status, decisions log, rejected ideas |
| `docs/ARCHITECTURE.md` | Tech stack, folder structure, system overview, data flow, key patterns |
| `docs/RULES.md` | Code conventions, naming, Unity workflow rules, anti-patterns |
| `docs/feature-ai.md` | Enemy AI (the Mutant) — state machine, attacks, gotchas |
| `docs/feature-combat.md` | Hitboxes, health, lock-on (`Soulslike.Combat`) |
| `docs/feature-player.md` | Locomotion, attacks, stamina (`Soulslike.Player`) |
| `docs/feature-ui.md` | HUD bars + lock-on indicator (`Soulslike.UI`) |
| `docs/feature-import-verifier.md` | Asset-import verifier (P2) — FBX reset guard; decided design, not yet built |
| `docs/_feature-template.md` | Template to copy for a new feature doc |
| `docs/build-journal.md` | Historical per-day build notes (Day 0–4.5) — finer detail than the feature docs; code + feature docs win on conflict |
| `docs/day-1-to-7-plan.md` | The day-by-day build roadmap |

## Conventions & architecture

Project conventions, naming, Unity-specific workflow rules, and anti-patterns live in **`docs/RULES.md`**. Tech stack, folder structure, system overview, and data flow live in **`docs/ARCHITECTURE.md`**. Read both before writing code. The most load-bearing house rules:

- **New Input System only** — legacy `Input.GetAxis`/`GetKey` throws at runtime.
- **Avoid singletons** — prefer events (health/stamina expose them; HUD + AI subscribe).
- **The Animator IS the combo state machine** — no script-side combo counter; transition conditions decide the graph.
- **StateMachineBehaviours own per-state side effects** (root motion, flags) — Animation Events near a state's exit time get silently dropped.
- After editing scripts, **refresh Unity via MCP and check the console** before claiming done. **Don't create new scenes** without asking. **Don't commit** `Assets/_Recovery/` or `combat_log.txt`.
