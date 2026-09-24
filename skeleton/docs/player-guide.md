# Last Animal — player guide (M14)

Last Animal is a single-player 3D ARPG about understanding creatures through a
spoken DNA language. You explore zones, fight or befriend what lives there,
raise a companion, and talk to the ecosystem in its own language.

## Controls

Verified against the `[input]` map in `project.godot`:

| Action | Keys |
|---|---|
| Move | `W`/`A`/`S`/`D` or arrow keys |
| Attack | `Space` or left mouse button |
| Interact | `E` |

## The HUD

Four readouts (C13 contract, `src/ui/Hud.cs`): **Life**, **Manna**, **DNA
meter**, and **Companion hearts**. Life drops when enemies hit you; Manna is
the resource your spoken DNA draws on; the DNA meter tracks your language
progress; companion hearts mirror your companion's loyalty.

## Core loop

1. **Extract DNA.** Defeating a creature yields its DNA signature
   (`CombatSystem.OnKill` → `DnaSignature`, C10/C2). Every creature's
   signature is a counter sequence — a word in the game's language.
2. **Speak DNA.** You play signatures back with `DnaLanguage.Speak` and the
   ecosystem answers with `DnaLanguage.Counter` — it learns from what you have
   said and strikes back with adapted counters (`EcosystemAdaptation`,
   C5). The DNA meter shows how your language stacks up.
3. **Companions.** A companion follows you (`CompanionFollowBody`), has needs
   (`CompanionNeeds`) and a loyalty score (`LoyaltyChanged` signal). Loyalty
   moves with how you treat it; neglect has consequences.
4. **Betrayal and the Empathy Book.** The `BetrayalSystem` (C7) can turn a
   companion against you. The Empathy Book (`src/ui/EmpathyPanel.cs`, logic in
   `src/empathy/EmpathyBook.cs`, C9) lets you read a companion's hidden
   emotional state and route a resolution: **Forgive** or **Permanent break**.
5. **Salary.** The `SalarySystem` (C6) pays (or withholds) a periodic salary —
   the economy pressure behind your choices.
6. **Zones.** The world is zoned (`zones/`: meadow, canyon, ruins; bluetest and
   redtest are engine test zones, not meant as destinations). Entering a zone
   triggers `EcosystemSpawner.OnZoneEnter` (C15): the spawn list reacts to your
   spoken-DNA history, so the ecosystem you face is the one you taught.

## Dialogue

NPC dialogue is surfaced through the on-screen dialogue box
(`DialogueSystem.Show(nodeId)`, C13). Dialogue content is keyed by node id;
the set of nodes is defined by the NPC/companion content in the zones.

## Saving

The game autosaves to `user://savegame.json` (see [install.md](install.md) for
the on-disk location). DNA counters, emotion, progression and zone are
persisted (C14).

---
Claim labels: controls, HUD, systems and save path are VERIFIED against the
source files named above. The moment-to-moment feel (difficulty, pacing) is
UNVERIFIED — no human has played the packaged Windows build yet (no wine on
the build host; see [build-and-run.md](build-and-run.md)).
