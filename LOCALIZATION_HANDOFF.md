# Localization Handoff

Status of the Spanish localization pass on the MainMenu collection, current as of v0.8.6. Goal is full Spanish, French, and one more (TBD) across the whole game.

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

Examples:
- `mainMenu.start`, `mainMenu.credits`, `mainMenu.creditsBody`, `mainMenu.slotActive`
- `mainMenu.settings.masterVolume`, `mainMenu.settings.leftFlipper`

Strings that are runtime-substituted (scores, level numbers, slot numbers like "Save 1") are NOT in the table — those need separate handling via script-side `LocalizedString` with smart-string arguments. None of those have been done yet.

---

## What's wired up and working

**Slot.prefab** (`Assets/Prefabs/UI/ProfileScreen/Slot.prefab`):
- `Active` label → `mainMenu.slotActive`
- `All-Time Score:` → `mainMenu.allTimeScore`
- `Total Wins:` → `mainMenu.totalWins`
- Nested Button1 instance used as the Delete button → String Reference overridden to `mainMenu.delete` (prefab-level override; propagates to all 3 save slot instances)

**MainMenu.unity scene** — Credits screen:
- Credits title (`Name (1)` under `Credits` parent) → `mainMenu.credits`
- Credits role labels body (`Name (2)`) → `mainMenu.creditsBody`
- Names column (`Name (3)`) intentionally left in English — proper nouns
- Names column RectTransform `Pos X` was bumped to ~-1100 so the longer Spanish role labels don't overlap

**MainMenu.unity scene — pre-existing wirings from JJ's earlier scaffold** (still in place):
- A few buttons wired to `mainMenu.collection`, `mainMenu.team22`, `mainMenu.settings`, `mainMenu.profile`

**Profile.prefab**:
- Profile title text already wired to `mainMenu.profile`

---

## What still needs to be wired (MainMenu collection)

All keys exist in the table with English + Spanish values. Components just need to be added.

### MainMenu.unity scene (use right-click → Localize shortcut on each TMP)

- `Start` → `mainMenu.start`
- `Mission Select` → `mainMenu.missionSelect`
- `Progression` → `mainMenu.progression`
- `Close` → `mainMenu.close`
- `Choose Your Ship:` → `mainMenu.chooseYourShip`
- `Ship Name` → `mainMenu.shipName`
- `Ship Description` → `mainMenu.shipDescription`
- `Win condition` → `mainMenu.winCondition`
- `Name` (player name field label) → `mainMenu.playerName`

### Prefabs (open each in Prefab Mode, then wire)

- `Assets/Prefabs/UI/MainMenuScreen/Main Menu.prefab` — has several pre-existing `LocalizeStringEvent` components with empty references (KeyId 0). Fill them in based on the source text.
- `Assets/Prefabs/UI/MainMenuScreen/Settings Panel.prefab` — 17 labels to wire (Sound, Master Volume, Music Volume, Effects Volume, Display, Display Mode, Resolution, Pixellation, UI, Gameplay, Controls, Left Flipper:, Right Flipper:, Launcher:, Enter:, Pause:, Back:). Skip `Option A` and `Button` — those are runtime placeholders for dropdown selections and keybindings.
- `Assets/Prefabs/UI/ChallengeCard.prefab` — `Rank:` → `mainMenu.rank`, `Highscore:` → `mainMenu.highscore`. Skip `Test Flight`, `S+`, `50K` (runtime data).
- `Assets/Prefabs/UI/Quick Run (1).prefab` — `Quick run` → `mainMenu.quickRun`.

### Skip entirely (don't add LSE components)

- `v0.8.6` (version string)
- `1000000000000` and other numeric placeholders
- `description`, `temporary main menu` (dev placeholders left in scene)
- `Save 1` — runtime-driven; needs script-side LocalizedString work in a later pass
- Score/level/multiplier numbers (`000`, `$0`, `x2`, `LVL 1`, etc.)
- Team member names in credits

---

## Known issues / cleanups

### 1. Collection rename never saved

The `m_TableCollectionName` in `Assets/Localization/Locales/Menu Labels Shared Data.asset` is still literally `Menu Labels`. Renaming via the Tables window didn't commit. Fix: select the Shared Data asset in the Project window, edit the `Table Collection Name` field directly in the Inspector to `MainMenu`, press Enter, Ctrl+S. Wires won't break — they reference by GUID.

The `.asset` filenames (`Menu Labels.asset`, `Menu Labels_en.asset`, etc.) can also be renamed in the Project window with F2 if you want them tidy, but that's cosmetic.

### 2. Four typo keys with leading spaces

These exist in the Shared Data asset and have no Spanish translation:
- `␣mainMenu.settings.displayMode` (with leading space — the no-space version `mainMenu.settings.displayMode` exists separately and is the correct one)
- `␣mainMenu.settings.resolution`
- `␣mainMenu.highscore`
- `␣mainMenu.rank`

Delete each in the Tables window. The non-space versions of `displayMode` and `resolution` already exist with English values. For `highscore` and `rank`, the non-space versions also exist with English values (`Highscore:` and `Rank:`); add Spanish (`Récord:`, `Rango:`) if not already filled in.

### 3. `mainMenu.creditsBody` naming

I named the multi-line credits text key `mainMenu.creditsBody` rather than `mainMenu.creditsRoles`. The content is the role labels ("Programming / Sound / 2D Art / 3D Art / Special Thanks to... / Font / Component Outline"). Name is fine as-is; just noted for clarity.

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
- For prefab instances reused with overridden text (like the Button1 prefab used as Settings, Delete, etc.), wire the LSE inside the prefab itself, then override the **String Reference** per instance — not the text.
- Always edit prefabs in **Prefab Mode** (double-click the prefab in Project window). Editing instances in a scene creates per-instance overrides that don't propagate.
- Spanish text runs ~30% longer than English. Expect layout adjustments per screen — the Credits screen needed a `Pos X` bump on the names column. Other screens may need similar tweaks or Horizontal Layout Groups.
- Switch locale at runtime via the Locale dropdown in the Game view toolbar (or Project Settings → Localization → Specific Locale Selector for editor default).
