# Decisions

One entry per real decision: a choice between options that someone might otherwise reopen later. Newest first.
Before reopening a settled question, read its entry. If new facts change the answer, add a new entry that
supersedes the old one instead of editing history.

## D4 · 2026-11-12 · Archive reader first, 5C8 support as a separate small task
**Decided:** build the read-only archive reader and browser (M6) next, and iron out the writer design (M7)
alongside it. 5C8 build support is a separate, smaller milestone.
**Rejected:** File access log (M5) first: useful for the loose-files question, but the reader is the foundation
for texture packs.
**Why:** Josh's call (2026-11-12). The archive format is already confirmed on every retail archive
([research.md § 1](../research.md#1-the-games-archives-eb3-big-files)), so the reader is low-risk.
**Revisit if:** M5 turns out to be needed to answer whether the game reads loose textures before M7 is designed.
**Supersedes:** nothing (the brief's M1→M7 order)

## D3 · 2026-11-12 · The archive code is our own C#
**Decided:** write the EB3/BIG4 reader, RefPack/chunkref decoders and the future writer as the launcher's own
C# 5 code, from the verified notes in `research.md`. Credit SkateGM, Skate 3 Recompiled+, 5C8 and mchughalex's
recomp for the format research.
**Rejected:** porting 5C8's `eb_extract.py` or Recompiled+'s `world_archive.py` (no licence; facts only).
Porting SkateGM's MIT `skate3_ui` folder (allowed with credit, but it has no writer or compressor and is Python).
**Why:** the repo is MIT and must stay safe to publish ([research.md § 7](../research.md#7-sources-and-licences)).
**Revisit if:** a compatibly licensed C# implementation appears.
**Supersedes:** nothing

## D2 · 2026-11-12 · Mods declare inner-archive files by folder layout
**Decided:** a mod replaces a file inside an archive by putting it at
`mods\<Mod>\archives\<archive path>\<inner path>`, e.g.
`mods\My Mod\archives\data\content\createacharacter.big\<inner path>`. This needs `mod.json` format 2, so older
launchers warn instead of linking `archives\` into the game.
**Rejected:** a manifest of replacements inside `mod.json` (harder for modders to make by hand).
**Why:** Josh's call (2026-11-12). Matches how mods already mirror `game\` paths.
**Revisit if:** the M7 design review finds a case the folder layout can't express.
**Supersedes:** nothing

## D1 · 2026-11-12 · Entry-level mods rebuild the archive into `mods\.active-game`
**Decided:** apply inner-file mods by rebuilding the whole archive into `mods\.active-game` (never `game\`):
unchanged files copied as stored, replaced files added, index regenerated. Cache the result so an archive is only
rebuilt when the mods touching it change.
**Rejected:** an overlay/patch file read at runtime (Jared's approach) or extra VFS drives (Doug's approach): both
need changes to the recomp itself, and a launcher can only change files on disk
([research.md § 4](../research.md#4-how-other-projects-change-game-files)).
**Why:** Josh's call (2026-11-12). Rebuilding reproduces all 50 retail archives' tables exactly
([research.md § 2](../research.md#2-rebuilding-an-archive)).
**Still open:** how replaced files are *encoded*. The original plan was "store them uncompressed", but every
uncompressed `fetexture.big` crashed in game ([research.md § 3](../research.md#3-in-game-tests-what-the-game-accepts)).
Until in-game tests 5 and 6 are in, the plan is to compress replaced files the way the original did. Record the
outcome as a new entry.
**Revisit if:** tests 5/6 show rebuilt archives can't be made to load.
**Supersedes:** nothing
