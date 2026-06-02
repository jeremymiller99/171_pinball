# Localization Handoff

Status of the Spanish localization pass on the MainMenu collection, current as of v0.8.7. Goal is full Spanish, French, and one more (TBD) across the whole game.

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
