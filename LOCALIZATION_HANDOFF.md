# Localization Handoff

Status of the Spanish localization pass, current as of v0.8.7. Goal is full Spanish, French, and one more (TBD) across the whole game.

**Latest work: see "Session update — 2026-06-02" immediately below.** The MainMenu-specific sections further down are still accurate background.

---

## Session update — 2026-06-02

### Done this session
- **`mainMenu.leaderboard` key added** (created in-editor via Tables window) and the **Leaderboard button wired** to it. This was the first of the two unwired MainMenu LSEs. English "Leaderboard" / Spanish "Clasificación".
- **New `Gameplay` String Table Collection created** (English + Spanish) and **22 keys bulk-imported** from CSV. Covers all the *static* in-game UI (pause menu, win/game-over screens, shop buttons, HUD label, error popup).
  - Import source: **`gameplay_localization.csv`** at the **repo root** (outside `Assets/`). Re-importable anytime via Tables window → Import/Export → Import CSV.
- **Started wiring the Gameplay keys** (in progress — see checklist below; confirm exactly what's already wired next session by switching locale to Spanish and eyeballing each panel).

### MainMenu — still open

1. **Second unwired LSE still needs wiring.** One of **{Profile, Progression, Settings, Play, Quit}** on the `Main Menu.prefab` instance in `MainMenu.unity` still has `String Reference = None` (KeyId 0). Find it: Hierarchy filter `t:LocalizeStringEvent` → the remaining one showing `None`. Point it at its existing key (the key already exists + has Spanish; it just isn't wired on this instance). Could not name it from disk — Unity stores nested-prefab children as composed hash IDs with no readable label.
2. **"Delete / Settings" crossed override to verify.** A Button1 instance was observed whose **displayed text says "Delete"** but whose **LSE points at a settings key** (or vice-versa). Button1.prefab is shared across many buttons; both the **text** and the **String Reference** are per-instance overrides, so they can get crossed. Fix: select the instance, read the TMP **Text Input** field (that's what the button really is), and set its String Reference to the matching key — `mainMenu.delete` if it's truly Delete, `mainMenu.settings` if it's truly Settings.

### Gameplay collection — wiring checklist (search by PARENT name, not displayed text)

Reminder: **Hierarchy search matches GameObject names, never the visible words.** Most labels are named `Text (TMP)`, so search the parent button/panel name below, expand, and wire the `Text (TMP)` child.

**`GameplayCore.unity`** — the in-game scene (NOT MainMenu; open it first):

| Search name | Displayed text | Key |
|---|---|---|
| `Pause Menu` (expand) | RESUME | `gameplay.resume` |
| `Pause Menu` → child | MENU | `gameplay.menu` |
| `Pause Menu` → child | QUIT | `gameplay.quit` |
| `Pause Menu` → child | Game Paused | `gameplay.paused` |
| `Settings Button` | SETTINGS | `gameplay.settings` |
| `Round Info` | Round Info (title) | `gameplay.roundInfo` |
| `Endless button` | TRY ENDLESS! | `gameplay.tryEndless` |
| `Win Screen` (expand) | You Win! | `gameplay.youWin` |
| `Win Screen` → child | MENU | `gameplay.menu` |
| `Debug Panel` (expand) | Debug (header) | `gameplay.debugTitle` |
| `Debug Panel` → child | You've encountered a bug. | `gameplay.debugBody` |
| `Debug Panel` → child | Close | `gameplay.close` |

**`Round Failed Panel.prefab`** (Prefab Mode — it IS instanced in GameplayCore, so wiring here propagates):

| Search name | Displayed text | Key |
|---|---|---|
| `Title Text` | GAME OVER | `gameplay.gameOver` |
| `Score:` | Score: | `gameplay.score` |
| `Rank:` | Rank: | `gameplay.rank` |
| `Level:` | Level: | `gameplay.level` |
| `Time Played: (1)` | Time Played: | `gameplay.timePlayed` |
| `Retry button` (expand) | Again | `gameplay.again` |
| `Menu Button` (expand) | Menu | `gameplay.menu` |

> ⚠️ Naming trap: the object literally named **`Time Played:`** shows the *value* "0:00" (skip). The label lives on **`Time Played: (1)`**.

**`Shop Panel.prefab`** (Prefab Mode — instanced in GameplayCore, propagates):

| Search name | Displayed text | Key |
|---|---|---|
| `Continue Button` | REROLL | `gameplay.reroll` |
| `Continue Button (1)` | DONE | `gameplay.done` |
| `Confirm Button` | CONFIRM | `gameplay.confirm` |
| `Cancel Button` | CANCEL | `gameplay.cancel` |
| `Label` | Do you want to swap? | `gameplay.swapPrompt` |

> ⚠️ Two near-identical `Continue Button` / `Continue Button (1)` — click each, read the TMP Text Input to tell REROLL from DONE.

**`Board Canvas.prefab`** (Prefab Mode — NOT in GameplayCore; used by the `Board_*.unity` scenes):

| Search name | Displayed text | Key |
|---|---|---|
| `Rounds` | Level: | `gameplay.level` |

> ⚠️ Object named `Rounds` but displays "Level:".

**`Win Screen.prefab`** — skip. Not instanced anywhere; the live win screen is the one in `GameplayCore.unity` (wired above).

**Skip-list (script-driven placeholders — handle in the scripts phase, NOT with LSE):** `Round Info (1)` "Round: 1", `Home Run` "NICE!", "You reached level X and scored x points", BallSpeedText "0.00 m/s", Debug **cheat** buttons (Color Switch / Unlock Everything / Instant Level Up / Start), Round Failed value texts (`score text` / `rank text` / `level text` / the `Time Played:` 0:00 object), Shop `ConfirmText` "Replace ? with ? for $?" + `Coins`, all Board Canvas numbers, Tooltip `Name` / `Type` / `Description` + "Sell Price: $5".

---

## Next phase: scripted / data-driven content (the big one)

None of this can use `LocalizeStringEvent` — a script writes `.text = ...` at runtime and would overwrite any LSE. Needs Unity Localization's `LocalizedString` API (or `LocalizationSettings.StringDatabase.GetLocalizedString(...)`).

**Decision pending — pick the approach first:**
- **A. `LocalizedString` fields** on each ScriptableObject (replace `string description` etc.). Unity-native, per-asset editor picker. Downside: re-enter every asset's text.
- **B. Keyed `Content` table + ID lookup helper** — keys like `ball.<id>.desc`, one helper resolves by asset id/name at runtime with fallback. Centralizes translations. Downside: a naming convention + one helper class. **Currently leaning B.**

**Scope of data-driven text (all have `displayName` and/or `description`):**

| Content | Definition file |
|---|---|
| Challenges/missions ("Test Flight", "Great for beginners", winConditionDescription) | `Assets/Scripts/Managers/ChallengeModeDefinition.cs` |
| Balls | `Assets/Scripts/Balls/BallDefinition.cs` |
| Artifacts | `Assets/Scripts/Artifacts/ArtifactDefinition.cs` |
| Components | `Assets/Scripts/BoardComponents/BoardComponentDefinition.cs` |
| Devil rounds / modifiers | `Assets/Scripts/Modifiers/RoundModifierDefinition.cs` |
| Ships | `Assets/Scripts/Progression/PlayerShipDefinition.cs` |
| Boards (name only) | `Assets/Scripts/Managers/BoardDefinition.cs` |

**Also script-driven (smart-string / runtime composites):** game-over rank screen values + rank labels (`GameOverRankingDisplay.cs` / `RunRankUtility`), "Round: {0}", "Score: {0}", save-slot "Save {0}", "You reached level X and scored x points", shop "Replace {0} with {1} for ${2}", "Sell Price: ${0}", tooltip Name/Type/Description. Consumers include `MenuUI`, `RenderTextureRaycaster` (hover/inspect tooltips), modifier-card popups, shop UI, game-over display.

Note: code (`.cs`) edits are safe while Unity is open (it recompiles). Adding **table keys/assets** must be done in-editor or with Unity closed.

---

## Setup that already exists

- Package: `com.unity.localization` 1.5.9 (already in `Packages/manifest.json`).
- Locale assets at `Assets/Localization/Locales/`:
  - `English (en).asset` (source locale)
  - `Spanish (es).asset` (target)
  - `Localization Settings.asset`
- One String Table Collection so far: **`Menu Labels`** (see "Known issues" — meant to be renamed to `MainMenu` but the rename never wrote to disk; the on-disk `m_TableCollectionName` is still `Menu Labels`. References work either way because they use GUIDs internally).
  - Shared data: `Menu Labels Shared Data.asset`
  - English table: `Menu Labels_en.asset`
  - Spanish table: `Menu Labels_es.asset`

The MenuLabels collection's GUID is `1b13340a4d1910d42996bf9b6b9db652`. Every `LocalizeStringEvent` that references it stores `GUID:1b13340a4d1910d42996bf9b6b9db652` for `m_TableCollectionName`, so renaming the collection later is safe — wires don't break.

---

## Key naming convention

`mainMenu.<thing>` for general menu items. `mainMenu.settings.<thing>` for Settings Panel labels. Use lowercase camelCase after the dot.

Strings that are runtime-substituted (scores, level numbers, slot numbers like "Save 1") are NOT in the table — those need separate handling via script-side `LocalizedString` with smart-string arguments. See the "Script-level work" section below for what's pending.

---

## Current key inventory (40 keys total)

All with English source + Spanish translation filled in:

**Top-level menu buttons:**
- `mainMenu.play` — Play / Jugar (note: key is named "play" but historically was renamed from "start" and back; English source is "Play")
- `mainMenu.settings` — Settings / Configuración
- `mainMenu.collection` — Collection / Colección
- `mainMenu.profile` — Profile / Perfil
- `mainMenu.team22` — Team 22 / Equipo 22
- `mainMenu.missionSelect` — Mission Select / Seleccionar Misión
- `mainMenu.progression` — Progression / Progresión
- `mainMenu.credits` — Credits / Créditos
- `mainMenu.close` — Close / Cerrar
- `mainMenu.quit` — Quit / Salir

**Ship Select panel:**
- `mainMenu.chooseYourShip` — Choose Your Ship: / Elige tu nave:
- `mainMenu.shipName` — Ship Name / Nombre de la nave
- `mainMenu.shipDescription` — Ship Description / Descripción de la nave
- `mainMenu.winCondition` — Win Condition / Condición de victoria
- `mainMenu.playerName` — Name / Nombre

**Credits screen:**
- `mainMenu.creditsBody` — multi-line "Programming / Sound / 2D Art / 3D Art / Special Thanks to... / Font / Component Outline" with matching Spanish

**Profile / save slots:**
- `mainMenu.slotActive` — Active / Activo
- `mainMenu.allTimeScore` — All-Time Score: / Puntuación histórica:
- `mainMenu.totalWins` — Total Wins: / Victorias totales:
- `mainMenu.delete` — Delete / Eliminar

**Challenge cards / mission select:**
- `mainMenu.rank` — Rank: / Rango:
- `mainMenu.highscore` — Highscore: / Récord:
- `mainMenu.quickRun` — Quick run / Partida rápida

**Settings panel:**
- `mainMenu.settings.sound` — Sound / Sonido
- `mainMenu.settings.masterVolume` — Master Volume / Volumen general
- `mainMenu.settings.musicVolume` — Music Volume / Volumen de música
- `mainMenu.settings.effectsVolume` — Effects Volume / Volumen de efectos
- `mainMenu.settings.display` — Display / Pantalla
- `mainMenu.settings.displayMode` — Display Mode / Modo de pantalla
- `mainMenu.settings.resolution` — Resolution: / Resolución
- `mainMenu.settings.pixellation` — Pixellation / Pixelado
- `mainMenu.settings.ui` — UI / Interfaz
- `mainMenu.settings.gameplay` — Gameplay / Jugabilidad
- `mainMenu.settings.controls` — Controls / Controles
- `mainMenu.settings.leftFlipper` — Left Flipper: / Flipper izquierdo:
- `mainMenu.settings.rightFlipper` — Right Flipper: / Flipper derecho:
- `mainMenu.settings.launcher` — Launcher: / Lanzador:
- `mainMenu.settings.enter` — Enter: / Aceptar:
- `mainMenu.settings.pause` — Pause: / Pausa:
- `mainMenu.settings.back` — Back: / Atrás:

---

## What's wired up and confirmed working

**Slot.prefab** (`Assets/Prefabs/UI/ProfileScreen/Slot.prefab`):
- `Active` → `mainMenu.slotActive`
- `All-Time Score:` → `mainMenu.allTimeScore`
- `Total Wins:` → `mainMenu.totalWins`
- Nested Button1 instance used as the Delete button → String Reference overridden to `mainMenu.delete` (prefab-level override; all 3 save slot instances inherit)

**MainMenu.unity scene:**
- Credits title (`Name (1)` under `Credits` parent) → `mainMenu.credits`
- Credits role labels body (`Name (2)`) → `mainMenu.creditsBody`
- Credits names column (`Name (3)`) intentionally left in English — proper nouns
- Names column RectTransform `Pos X` bumped to ~-1100 so longer Spanish role labels don't overlap
- Start (Play), Mission Select, Progression, Close, Choose Your Ship:, Ship Name, Ship Description, Win condition, Name — all wired
- Plus existing JJ-scaffold wirings on `mainMenu.collection`, `mainMenu.team22`, `mainMenu.settings`, `mainMenu.profile`

**Main Menu.prefab** (`Assets/Prefabs/UI/MainMenuScreen/`):
- The Play button (Button1 instance named `Play`) wired to `mainMenu.play`
- Quit button wired to `mainMenu.quit`
- Team 22 inherited from the prefab itself

**Settings Panel.prefab**: most/all of the 17 labels wired during this session — `leftFlipper` and `rightFlipper` explicitly confirmed wired on the last pass.

**ChallengeCard.prefab**:
- `Rank:` → `mainMenu.rank`
- `Highscore:` → `mainMenu.highscore`

**Quick Run (1).prefab**:
- `Quick run` → `mainMenu.quickRun`

**Profile.prefab**:
- Profile title text wired to `mainMenu.profile`

---

## Outstanding issues to close out the MainMenu pass

### 1. Two unwired LocalizeStringEvent components in MainMenu.unity

At `Assets/Scenes/Core/MainMenu.unity` lines 12652 and 16092 there are LSE components with **KeyId 0** and blank `m_TableCollectionName`. Both are on TMP_Text children of Main Menu.prefab instances. They throw "No translation found" warnings at runtime.

To fix in the Editor:
- In MainMenu.unity Hierarchy, filter `t:LocalizeStringEvent` or expand each Main Menu prefab instance and inspect every TMP_Text child.
- Any LSE whose String Reference shows `None` / blank in the Inspector — either point it at the right key based on the TMP's source text, or right-click the component → Remove Component if that text shouldn't be localized.

### 2. Orphan reference to deleted old `mainMenu.play` keyId

The old `mainMenu.play` key from JJ's original scaffold had keyId `39696351232` and was deleted. There's still a prefab-modification reference to that ID in `Assets/Prefabs/UI/MainMenuScreen/Main Menu.prefab` (around line 1003) and in `Assets/Scenes/Core/MainMenu.unity`. It looks like a scene-level override on a Button1 instance still pointing at the dead keyId.

To fix: open Main Menu.prefab, find the Play button (Button1 instance named `Play`), confirm its child Text (TMP)'s LocalizeStringEvent shows `MainMenu/mainMenu.play` (the new key, keyId `43837843045343232`) in the Inspector. If any reference shows red / invalid / "Missing", repoint it to `mainMenu.play`. Same check in the MainMenu scene Inspector for that prefab instance.

### 3. Collection rename never saved

`m_TableCollectionName` in `Menu Labels Shared Data.asset` is still literally `Menu Labels`. Renaming via the Tables window didn't commit. Fix: select the Shared Data asset in the Project window, edit the `Table Collection Name` field directly in the Inspector to `MainMenu`, press Enter, Ctrl+S. Wires won't break — they reference by GUID. Filenames can be renamed via F2 too if you want them tidy (cosmetic).

### 4. ChallengeCard runtime instances — verify in Play mode

ChallengeCards are spawned at runtime by `MissionSelectController` (via the `challengeCardPrefab` field in MainMenu.unity). The prefab itself IS wired for `Rank:` and `Highscore:`, so spawned instances should pick up the wiring. Last observation was that the labels appeared to still be English — but Spanish "Rango:" and "Récord:" look like the English cognates "Range" and "Record", which may have caused a false negative. In the next test:
- Switch locale to Spanish in Play mode
- Open Mission Select
- Verify the labels actually read `Rango:` and `Récord:`. If they literally read `Rank:` and `Highscore:`, dig further — could be that something at spawn time overwrites text, or the prefab edit wasn't persisted.

---

## Script-level localization needed (separate phase)

These can NOT be fixed with `LocalizeStringEvent`. They need code changes in the relevant scripts to use Unity Localization's `LocalizedString` API (or `LocalizationSettings.StringDatabase.GetLocalizedString(...)`) with smart-string formatting where dynamic values are involved.

- **Save slot labels — "Save 1 / Save 2 / Save 3"**. In `Slot.prefab` the TMP currently shows `Save 1` as hardcoded text but a script substitutes the actual slot number at runtime. Need `mainMenu.saveSlot` key with value `Save {0}` (English) / `Partida {0}` (Spanish), and update whichever script writes the text to fetch the localized string and format with the slot index.
- **Progression screen** (`Assets/Scripts/UI/ProgressionScreenController.cs`): all text is set in code via `.text = ...`:
  - Hardcoded `"LOCKED"` string (line 624) — needs key `mainMenu.progression.locked`
  - `xpTotalText.text = ...` (line 1021) — total score earned label; need key like `mainMenu.progression.totalScoreEarned`
  - `nextUnlockText.text = ...` (lines 1041, 1047) — next unlock text; need keys
  - `refs.nameText.text = displayName` (line 864) and `refs.thresholdText.text = ...` (line 869) — these are *data*, sourced from `ProgressionConfig`/`ProgressionTier`. To localize, either: (a) add localized name/threshold fields to the tier config, (b) keep names as data but localize the *threshold format string* (e.g. "Threshold: {0:N0}").
- **Settings keybind labels** (Left / Right / Down / Esc / Enter / Backspace etc.) — these come from Unity's Input System showing the currently bound key. Standard practice in games is to keep key names in their native form so users recognize them on their physical keyboard. **Recommend leaving these alone**; mention in the loc handoff so a translator doesn't expect them to be translatable.

---

## Layout adjustments needed for Spanish text

Spanish strings run ~30% longer than English. Two fixes are useful as a pattern:

- **Widen the container or shift adjacent elements**: used on the Credits screen — bumped the names column's RectTransform Pos X so the longer Spanish role labels don't overlap. Works when the layout is fixed-anchored.
- **TMP Auto Size**: select the TMP_Text, check Auto Size in the component, set sensible Min/Max font sizes. Text shrinks to fit when needed and grows back when there's room. Use this for Settings Panel labels where the layout space is tight and there are many labels (Volumen general, Volumen de música, etc.).

The Play button initially wrapped because Spanish "Comenzar" was 8 letters. Was fixed by switching the Spanish translation to "Jugar" (5 letters), which fits the same button width as "Play". So sometimes the cleanest fix is a shorter translation, not a layout change.

---

## Adding the third language later

No re-wiring will be needed when adding the third locale. Process:

1. Create a new Locale asset (e.g. French) in `Assets/Localization/Locales/`.
2. The Tables window will show a new column per collection automatically.
3. Fill in French translations for each key.

`LocalizeStringEvent` components reference a key, not a locale — Unity looks up the active locale at runtime.

---

## Next collection: `Gameplay`

Hasn't been started. Will hold in-game UI strings:
- Pause menu: `Resume`, `Menu`, `Quit`, `Game Paused`
- Win/lose screens: `You Win!`, `GAME OVER`, `Again`, `Time Played:`, `Score:`, `Level:`
- HUD labels: `Round:`, `LVL:`
- Settings entry point from pause (`SETTINGS`) — could reuse MainMenu Settings keys or duplicate
- Shop panel: `REROLL`, `DONE`, `CONFIRM`, `CANCEL`, `Do you want to swap?`, `Replace ? with ? for $?`
- Board-specific: `SHOP` (Board_NA)
- Debug popup: `Debug`, `You've encountered a bug…`, `Close`
- Round info / modifier cards: TBD (most are runtime data, may need LocalizedString in scripts)

Texts live across `Assets/Scenes/Core/GameplayCore.unity`, the 3 `Assets/Scenes/BoardScenes/Board_*.unity`, and prefabs in `Assets/Prefabs/UI/` (Round Failed Panel, Shop Panel, Win Screen, Board Canvas, etc.). Same workflow: collection → keys → wire components → translate.

---

## Workflow reminders

- **Right-click → Localize** on a TMP_Text component header is the fastest way to add a `LocalizeStringEvent`. It adds the component, sets the String Reference, and wires the Update String event to set `TextMeshProUGUI.text` — all in one click.
- For prefab instances reused with overridden text (like the Button1 prefab used as Settings, Delete, Quit, Play, etc.), wire the LSE inside the prefab itself, then override the **String Reference** per instance — not the text.
- Always edit prefabs in **Prefab Mode** (double-click the prefab in Project window). Editing instances in a scene creates per-instance overrides that don't propagate.
- Container vs. child gotcha: some "buttons" in the Hierarchy don't actually carry the TMP_Text themselves — they're parents whose child `Text (TMP)` has it. If right-click → Localize is missing, expand the GameObject and try the child.
- Hierarchy search caveats: the search bar matches GameObject names, not displayed text. Many TMPs are just named `Text (TMP)`. Fallback: filter by `t:TextMeshProUGUI`, click through, read the Text Input field in the Inspector to identify what each one displays.
- Switch locale at runtime via the Locale dropdown in the Game view toolbar (or Project Settings → Localization → Specific Locale Selector for editor default).
- Reverting prefab overrides: select the prefab instance root in the Hierarchy → Inspector header → **Overrides ▼** → **Revert All**. Was used to clean up the broken scene-level overrides that were blocking Slot.prefab's Delete wiring from propagating to all 3 save slots.

---

## Direct YAML edits

For mechanical bulk changes (renaming keys, fixing typos, removing leading whitespace from key names) the table assets can be edited directly as YAML files at `Assets/Localization/Locales/`. Always close Unity before editing on disk so the in-memory state doesn't overwrite the disk version on save. Re-open Unity afterwards and let it re-import.

This is how the 4 leading-space typo keys (`␣mainMenu.settings.displayMode`, `␣mainMenu.settings.resolution`, `␣mainMenu.highscore`, `␣mainMenu.rank`) were cleaned up — stripped the leading space from each `m_Key:` while preserving the key IDs and existing translations.
