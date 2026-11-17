# Report: <MILESTONE>-<slug>

<!-- Copy to docs/reports/TEMPLATE.md in your project. The coding agent writes to docs/reports/<MILESTONE>-<slug>.md and commits it on its branch.
     The coordinator reviews from this file, so it must make sense without the chat. -->

**Prompt:** `docs/prompts/<MILESTONE>-<slug>.md`
**Branch:** `<branch>` (from tag `<tag>`) · **Last commit:** `<hash>`
**Status:** done | stopped (<which stop condition>) | partial

## Tool versions
| Tool | Version used | Matches HANDOFF.md? |
|------|--------------|---------------------|
| <tool> | <version> | yes / **no: expected <version>** |

## What changed
- <file or area>: <what and why> (`<commit>`)

## Findings
| Finding | Status | Evidence |
|---------|--------|----------|
| <claim> | confirmed / unverified / wrong | <file, address/offset, log, capture or test> |

Written to: `<docs/research/... section>` (confirmed findings stamped `[confirmed @ <branch> | tool=version]`,
new sections added to `docs/research/INDEX.md`)

## Tests
```
<command you ran>
<the summary line it printed>
```

## Stopped because (only if status is "stopped")
<Which stop condition, what you tried (each attempt in one line), and what you'd need to continue.>

## Open questions
- <Anything the coordinator or the user must decide or provide.>
