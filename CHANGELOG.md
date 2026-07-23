# Changelog

Version format: `0.MAJOR.MINOR`
- **MAJOR** (second digit) — big updates / new features / systems
- **MINOR** (third digit) — small changes / tweaks / fixes

Version is displayed on the main menu (`Assets/Scenes/Core/MainMenu.unity`).

Entries below 0.4.6 were reconstructed retroactively from git history (commits `ef9ce39`..`ab6744e`).

---

## 0.9.6 — Fire VFX trim + flippers and portals are fireproof
_2026-07-20 · Contributor: JJ_
- `FireVfxLibrary` now owns spawning of its own prefabs and applies a per-prefab scale and
  emission-rate trim, so the shared smoke and flames can be toned down without editing the
  CFXR assets. Smoke defaults to 0.3 scale / 0.35 emission — it was eating far too much
  screen space at full size. Per-object `fireVfxPrefab` / `fueledVfxPrefab` overrides spawn
  untrimmed, so Charcoal and Fireball are unaffected.
- Swapped the shared On Fire prefab from `CFXR Fire` to `CFXR2 Firewall A`.
- New `FireStatusUtility.CanCatchFire`: components whose `componentType` is `Flipper` or
  `Portal` never get a `ComponentFireStatus`, so they can no longer be Fueled or lit. Also
  guarded in `BallFireStatus`'s contact-ignite loop so an editor-placed status on one of
  those can't light either, and in its burn tick so a ball that is itself On Fire stops
  re-triggering a flipper or portal it happens to be resting against.

## 0.9.5 — Flame VFX for burning board components
_2026-07-20 · Contributor: JJ_
- `FireVfxLibrary` gained an `onFireVfxPrefab` slot, wired to `CFXR Fire`, and
  `FireStatus.StartFireFeedback` now falls back to it when the object has no
  `fireVfxPrefab` of its own. Board components — which are always given their
  `ComponentFireStatus` at runtime and so can never be wired in the inspector — finally
  show flames while On Fire. Charcoal and Fireball keep their own `OnFireVFX` prefab.

## 0.9.4 — Smoke VFX for Fueled objects
_2026-07-20 · Contributor: JJ_
- `Assets/Scripts/StatusEffects/FireStatus.cs` now spawns a looping smoke effect on any
  object carrying Fuel beyond its innate Flammable rating (new `IsFueled` property), so a
  board component that Charcoal has fueled visibly smolders before it ever catches. The
  smoke is parented to the object, refreshes off `StacksChanged`, and is torn down when the
  object ignites (the fire VFX takes over), burns out, or is destroyed.
- New `Assets/Scripts/StatusEffects/FireVfxLibrary.cs` + `Assets/Resources/FireVfxLibrary.asset`
  — a Resources-loaded prefab library, needed because `ComponentFireStatus` is always added
  at runtime and so can never have inspector-wired prefab fields. Points at
  `CFXR Smoke Source 3D` by default; a per-object `fueledVfxPrefab` field on `FireStatus`
  overrides it.

## 0.9.3 — Breaking-news chyron crawl (Monitor 1b political-decay screen)
_2026-07-19 · Contributor: JJ_
- New `Assets/Scripts/UI/BreakingNewsCrawl.cs` — a MonoBehaviour that procedurally builds a
  horizontal breaking-news chyron inside any RectTransform under a Canvas. A red "BREAKING"
  tag pins to the left (cycling through configurable labels like BREAKING / LIVE / ALERT /
  URGENT and pulsing between two colors on a flash timer), while a seamless two-copy
  marquee scrolls political-decay headlines right-to-left across the remainder of the
  container. Headlines, colors, scroll speed, tag width, cycle intervals, and font are all
  inspector-tunable; the crawl area auto-adds a `RectMask2D` so text is hard-clipped to its
  bounds. Meant to sit as a third slab on the Monitor 1b canvas alongside
  `StockTickerDisplay` and `StockChartDisplay`.

## 0.9.2 — Crashing single-stock line-chart display (Monitor 1b companion)
_2026-07-19 · Contributor: JJ_
- New `Assets/Scripts/UI/StockChartDisplay.cs` + `Assets/Scripts/UI/StockChartLineGraphic.cs`
  — a MonoBehaviour that procedurally builds an animated single-stock line chart inside any
  RectTransform, paired with a custom `MaskableGraphic` that draws the polyline, optional
  area fill, and optional faint grid. A sliding sample window advances on a jittered timer
  using a downward-biased random walk (with configurable plunge spikes and rare small
  bounces), and the header label continuously updates with the current price and cumulative
  window % change. All colors, thickness, grid divisions, background, and clipping are
  inspector-tunable; a `RectMask2D` is auto-added by default so the chart stays inside its
  container. Meant to sit next to `StockTickerDisplay` on the Monitor 1b canvas.

## 0.9.1 — Crashing stock-ticker display (Monitor 1b main-menu ambient screen)
_2026-07-19 · Contributor: JJ_
- New `Assets/Scripts/UI/StockTickerDisplay.cs` — a self-contained MonoBehaviour that
  procedurally builds a scrolling red stock ticker inside any RectTransform under a Canvas.
  Rows animate upward, prices tick down on a jittered timer, and a configurable "big drop"
  chance hammers a random row with a large percentage loss and pulses both the row and an
  optional background image bright red. Rows recycle as they scroll off the top; symbols and
  starting prices are randomized from serialized lists. Intended for the Monitor 1b canvas
  on `Assets/Scenes/Core/MainMenu 1.unity` (attach in the editor — no prefab required; add
  an optional `TMP_Text` header and background `Image` and assign in the inspector).

## 0.9.0 — Fire status system (Flammable / Ignite / On Fire / Fuel) + Fireball & Charcoal
_2026-07-14 · Contributor: Devin_
- New keyword-driven fire status system in `Assets/Scripts/StatusEffects/`: `FireStatus`
  (shared stacks/burn/tick core), `BallFireStatus` (contact spread both directions,
  fuel-on-contact, 0.5s re-activation of the last component hit, loadout write-back),
  `ComponentFireStatus` (added at runtime when a component is first Fueled; ticks its hit
  effect while burning), `FireStatusUtility` (get-or-add helpers + tick gating during
  shop/drain/no-run). Flammable X = X stacks = X seconds of burn once Ignited; Fuel adds a
  stack and extends an active burn; burning consumes ~1 stack/second.
- `BoardComponent.ActivateAsIfHit()`: new programmatic activation path (no ball, so no ball
  multipliers) with overrides on `Bumper` (audio, no bounce/shake), `BombComponent`
  (extracted `TryExplode()`, ticks count toward explosions), `CasinoComponent` (payout only
  every Nth activation), `FrozenComponent` (extracted `HandleHitProgression()`, ticks chip
  the freeze). `DuplicatingComponent` intentionally keeps base behavior (can't clone without
  a source ball).
- New balls: **Fireball** (`FireballBall`, Striker — launches On Fire via new
  `PinballLauncher.BallLaunched` static event, cannot be Fueled, detonates like a bomb when
  its burn ends, then retires through the drain flow) and **Charcoal** (`CharcoalBall`,
  Catalyst — fuels everything it touches; while queued, every launched ball is Fueled twice).
  CSV rows for both already existed in `Ball-Descriptions.csv`.
- Fuel persists between launches: new `_extraFlammableStacksBySlot` parallel list in
  `BallLoadoutController` (synced through all loadout mutators) +
  `BallSpawner.SyncFireStacksFromLoadout()` on hand rebuild.
- Fixed `TooltipUI` bug where the second definition panel was never populated (both tags
  rendered into the first panel).
- Known limitations: board-component definitions still have no tooltip term tags (only balls
  surface keyword panels); legacy `FireComponent` heat-up bumper is untouched and unrelated
  to the new system; prefabs/definitions for the two balls are set up in-editor (see below).
- Note: the "bump version text in MainMenu.unity" step from AGENTS.md appears stale — the
  menu label is CI-driven via `BuildVersionLabel`/`Application.version`; no `v0.x.y` scene
  text exists to update.

## 0.8.7 — Spanish localization pass on MainMenu (continued)
_2026-06-01 · Contributor: Devin_
- Cleaned up 4 typo keys in `Menu Labels` that had leading whitespace (`␣mainMenu.settings.displayMode`, `␣mainMenu.settings.resolution`, `␣mainMenu.highscore`, `␣mainMenu.rank`). Stripped the leading space in-place via direct YAML edit so the key IDs and existing translations were preserved. Also fixed two wrong English source values in the same pass: `displayMode` was set to `Display`, now `Display Mode`; `highscore` was set to `Highscore` (no colon), now `Highscore:` to match the ChallengeCard source string.
- Wired up the remaining MainMenu scene buttons (Mission Select, Progression, Close, Choose Your Ship:, Ship Name, Ship Description, Win condition, Name) plus the Play/Quit buttons in `Main Menu.prefab`, the Settings Panel labels (Left Flipper:, Right Flipper:, and others), `ChallengeCard.prefab` (Rank:/Highscore:), and `Quick Run (1).prefab` (Quick run). Added one new key `mainMenu.quit` → Quit / Salir for the Quit button.
- Renamed `mainMenu.start` back to `mainMenu.play` with English source `Play` — the Main Menu Play button was historically named that way and the rename produced a dead reference + warning. Switched the Spanish translation to `Jugar` so it fits the button width (was overflowing as `Comenzar`).
- Updated `LOCALIZATION_HANDOFF.md` with the current key inventory, what's confirmed wired, the three outstanding issues (two unwired `LocalizeStringEvent` components in MainMenu.unity, an orphan reference to the deleted old `mainMenu.play` keyId, the collection rename still not committed to disk), and a `Script-level localization needed` section covering the Progression screen (`ProgressionScreenController.cs` sets all text in code), save slot labels (`Save N` is script-concatenated), and Settings keybind labels (Input System driven, recommend leaving as-is).
- Menu-scene version text bumped to `v0.8.7`.

## 0.8.6 — Spanish localization pass on MainMenu (work in progress)
_2026-06-01 · Contributor: Devin_
- Added ~35 string keys to the existing `Menu Labels` collection (Profile screen, Credits screen, ChallengeCard, Quick Run, and full Settings Panel labels) with English source values and Spanish translations. Existing 5 keys (`mainMenu.play`/`.settings`/`.collection`/`.profile`/`.team22`) were preserved and Spanish-translated; `mainMenu.play` was deleted in favour of `mainMenu.start`.
- Wired `LocalizeStringEvent` components in `Slot.prefab` (Active / All-Time Score: / Total Wins:) and overrode the nested Button1 instance's String Reference to `mainMenu.delete`. All three save slots now switch correctly via the prefab.
- Wired the Credits screen title (`mainMenu.credits`) and role-labels body (`mainMenu.creditsBody`) on MainMenu.unity; bumped the names column's RectTransform Pos X so the longer Spanish role labels don't overlap.
- Reverted the unintended scene-level prefab overrides on the Profile instance in MainMenu.unity that were blocking Slot.prefab's wiring from propagating, and clearing the "No translation found" warning that came from an unwired LSE on the Credits title.
- Work is partial — handoff details (key list, what's not yet wired, known typo keys to clean up, the pending `MainMenu` collection rename) are in `LOCALIZATION_HANDOFF.md`. Pick up from there next session.
- Menu-scene version text bumped to `v0.8.6`.

## 0.8.5 — basic three-panel tutorial (first play, first level-up, first shop visit)
_2026-05-14 · Contributor: JJ_
- Added `Assets/Scripts/UI/BasicTutorialController.cs`: self-bootstrapping `DontDestroyOnLoad` singleton (RuntimeInitializeOnLoadMethod / BeforeSceneLoad) that owns its own ScreenSpaceOverlay canvas (sort order 9990) and builds three programmatic panels in code — no prefab wiring required. Subscribes to `GameRulesManager.RoundStarted` / `ShopAvailabilityChanged` / `ShopOpened` whenever GRM appears via `ServiceLocator` (rechecked on `SceneManager.sceneLoaded` and per-frame in Update so additive board scenes pick it up).
- Panel 1 (first play): fires on the first `RoundStarted` of the player's first run. Pauses the game (`Time.timeScale = 0`), disables `PinballLauncher` / `PinballFlipper`, shows cursor. Closes via START button → restores time/input/cursor and records `hasSeenFirstPlayTutorial`.
- Panel 2 (first level-up): fires on the first `ShopAvailabilityChanged(true)`. Same pause/lock as panel 1. **No close button** — the panel auto-closes when `ShopOpened` fires (i.e. when the player presses the lit-up shop button). The fullscreen background does NOT block the shop button because `RenderTextureRaycaster` reads `Mouse.current.position` directly and runs its own `Physics.Raycast` against the 3D `ShopButton3D` — Unity's UI raycast blocking doesn't apply. Time/input/cursor are restored before panel 3 builds so `ShopTransitionController`'s camera-pan and UI-slide coroutines (which read `Time.deltaTime`) run normally. Records `hasSeenLevelUpTutorial`.
- Panel 3 (first shop visit): fires on the first `ShopOpened`. Does NOT pause (the shop is already its own modal state). Closes via GOT IT button. Records `hasSeenShopTutorial` (existing flag, previously unused).
- Added `hasSeenFirstPlayTutorial` and `hasSeenLevelUpTutorial` bool fields to `ProfileSaveData` plus matching `HasSeen*Tutorial` / `Record*TutorialSeen` static methods on `ProfileService`. Default-false bools — no profile version bump needed; existing saves see all three tutorials on next play. The pre-existing `hasAnsweredFirstTimePlayingPrompt` flag (used by `ModifierCardPopupController` to suppress the first-round modifier card) is intentionally untouched — different semantics.
- Panel labels and buttons use the project's **Manticore** TMP font (`Manticore 14 SDF`), resolved at runtime via `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()` + name-hint match (mirrors the `FpsCounterHUD` pattern). Result is cached after first lookup. Falls back to TMP default if Manticore isn't loaded by any scene reference. Panel text strings are grouped at the top of `BasicTutorialController.cs` under a clearly-marked `PANEL CONTENT` block for easy editing.
- Menu-scene version text bumped to `v0.8.5`.

## 0.8.4 — run-fail highscore + level-reached analytics events
_2026-05-13 · Contributor: JJ_
- `PinballAnalytics` now exposes `LogRunHighScore(score, boardId)` and `LogRunLevelReached(levelReached, boardId)` which record two new custom events: `runHighScore` (params: `score(long)`, `boardId(string)`) and `runLevelReached` (params: `levelReached(int, 1-based)`, `boardId(string)`). Both must be registered in the Unity Cloud Dashboard with those exact param types — note `score` is a Long, not Int, because pinball scores routinely exceed `int.MaxValue`.
- Both events fire from `GameRulesManager.ShowRoundFailed`, immediately after the existing `SteamLeaderboards.UploadScore` call, using the already-computed `capturedScore`, `boardName`, and current `roundIndex + 1`. They do **not** fire on full-run completion (`CompleteRunAndShowWinScreen`) or on quit-to-menu — only on the explicit out-of-balls fail path, since the ask was "where the player ended" when their run was over.
- Highscore per player is derived dashboard-side via `MAX(score) GROUP BY user`; max level reached via `MAX(levelReached) GROUP BY user`. The event names are descriptive of the moment (one row per failed run), not of "personal best at the time of fire" — every run-fail emits, and the dashboard does the aggregation.
- Menu-scene version text bumped to `v0.8.4`.

## 0.8.3 — shop session time tracking (subtracted from level durations)
_2026-05-13 · Contributor: JJ_
- `PinballAnalytics` now exposes `LogShopSession(levelIndex, durationSeconds, boardId)` which records a new `shopSessionCompleted` custom event. Params: `levelIndex(int, 1-based — the level the player just completed before entering this shop)`, `durationSeconds(float)`, `boardId(string)`. Must be registered in the Unity Cloud Dashboard with those exact param types before data flows.
- `GameRulesManager` stamps `_shopOpenedAt = Time.unscaledTime` in `OpenShop()` and, on `CloseShopAndAdvanceIndexOnly`, computes the elapsed, adds it to a per-level `_shopElapsedThisLevel` accumulator, and fires the `shopSessionCompleted` event. The close path is gated on the prior `_shopOpen` value so idempotent / defensive close calls don't emit phantom zero-duration sessions. Run-end paths (`CompleteRunAndShowWinScreen`, `ShowRoundFailed`, retry via `StartRun`) bypass this method, so quitting mid-shop produces no event.
- `TryProcessLevelUps` now subtracts `_shopElapsedThisLevel` from the `levelCompleted` `durationSeconds` and resets the accumulator. Level durations are now active-play time only — the shop-time caveat from 0.8.2 no longer applies. `Mathf.Max(0f, ...)` guards against negative durations from any timer skew.
- Menu-scene version text bumped to `v0.8.3`.

## 0.8.2 — level completion time analytics event
_2026-05-13 · Contributor: JJ_
- `PinballAnalytics` now exposes `LogLevelCompleted(levelIndex, durationSeconds, boardId)` which records a new `levelCompleted` custom event. Params: `levelIndex(int, 1-based)`, `durationSeconds(float)`, `boardId(string, board scene name)`. The event must be registered in the Unity Cloud Dashboard with those exact param types before data flows.
- `GameRulesManager` tracks `_currentLevelStartTime` (set in `StartRun` alongside `_runStartTime`, reset on every goal-cross inside `TryProcessLevelUps`). When the player crosses `CurrentGoal`, the duration since the previous goal-cross (or run start, for level 1) is logged and the timer is reset for the next level. Timer uses `Time.unscaledTime` to match `RunElapsedTime`.
- Only completed levels are logged — round failures and quit-mid-level produce no event. Batched level-ups inside a single `TryProcessLevelUps` call attribute the elapsed time to the first level in the batch and ~0s to subsequent ones (sum-of-durations still equals total run time).
- Caveat: level time includes any shop visit or board-load gap that fell between the previous goal-cross and this one — the clock keeps running through shop sessions. For a pure active-play metric, a future change would subtract a shop-elapsed accumulator.
- Menu-scene version text bumped to `v0.8.2`.

## 0.8.1 — shop item analytics events
_2026-05-13 · Contributor: JJ_
- Added `Assets/Scripts/Analytics/PinballAnalytics.cs`: static wrapper that initializes Unity Services + `AnalyticsService` once at startup (`RuntimeInitializeOnLoadMethod` BeforeSceneLoad) and exposes `LogShopItemShown` / `LogShopItemPurchased`. All calls are no-ops until the service is ready and never throw into gameplay.
- `ShopOfferShelfController.SpawnOfferDisplay` now logs `shopItemShown` for each offer spawned on the shelf.
- `UnifiedShopController` now logs `shopItemPurchased` from `ConfirmComponentPlacement` (covers click-confirm + drag-drop board), `AutoBuyBallOffer` (drag ball to empty slot), and `ConfirmDragDropBallReplace` (drag ball over existing slot). Logged after `_shelf.ConsumeOffer` so refunded/failed paths don't count. Mystery balls log the placeholder id on `shown` and the resolved concrete ball id on `purchased`.
- Each event sends `itemId`, `itemName`, `itemType` (Ball/BoardComponent), and `price` -- both events and their parameter schemas must be registered in the Unity Cloud Dashboard before data flows; per-item counters come from grouping by `itemId` in dashboard queries.
- Menu-scene version text bumped to `v0.8.1`.

## 0.8.0 — local playtest leaderboard
_2026-05-06 · Contributor: Devin Lopez_
- Added `Assets/Scripts/Leaderboard/{LeaderboardEntry,LeaderboardData,LocalLeaderboard}.cs`: file-backed top-N (cap 100) leaderboard at `Application.persistentDataPath/leaderboard.json`. One entry per run, sorted by score desc; persists last entered name in PlayerPrefs (`LocalLeaderboard_LastName`) for fast turn-taking on a shared dev machine.
- Added `Assets/Scripts/UI/LeaderboardPanelController.cs`: self-contained programmatic overlay panel. Shows score, a name input prefilled with the last name, then the top 10 with the new entry highlighted and a Continue button. Manages its own pause + gameplay-input lock (mirrors `WinScreenController`'s `PinballLauncher`/`PinballFlipper` disable). No prefab wiring required — built fully in code.
- `RunCompletionHelper.RecordProgressAndShowWinScreen` and `GameRulesManager.ShowRoundFailed` now show the leaderboard before the existing win/fail UI, with the original UI as the continuation callback. Both completed and drained runs submit. Existing `SteamLeaderboards.UploadScore` calls are unchanged — the local board runs in parallel and does not depend on Steam.
- Menu-scene version text bumped to `v0.8.0`.

## 0.7.6 — main-menu → gameplay fade transition
_2026-04-22 · Contributor: JJ_
- Added `SceneFader` (`Assets/Scripts/UI/SceneFader.cs`): self-bootstrapping `DontDestroyOnLoad` singleton that builds its own top-most (sort order 32000) screen-space overlay canvas + black `Image` and drives an unscaled-time fade `CanvasGroup`. No scene/prefab wiring required — spawns via `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`.
- `MainMenuUI.StartQuickRun` / `StartChallengeBoards` / `LoadMenuScene` now go through `SceneFader.FadeAndLoadScene` instead of `SceneManager.LoadScene`. Both play paths pass `holdBlackUntilReady: true` so the screen stays fully black across `GameplayCore` load + the additive board-scene load (no flash of empty core).
- `RunFlowController.StartRunFromSession` now calls `SceneFader.Instance.FadeIn()` one frame after `LoadBoard` + `StartRun` + `ResumeGameplayInput` complete, so the fade-in reveals the finished board rather than racing the additive load.
- Menu-scene version text bumped to `v0.7.6`.

## 0.7.5 — firework, drop-target, and frenzy-portal SFX hooks
_2026-04-21 · Contributor: JJ_
- `LevelUpVFXTrigger` now calls `AudioManager.PlayFireworks` at each firework spawn point during the stagger coroutine, so the level-up visual is matched by a firework SFX per burst.
- `DropTarget` plays `PlayDropTargetDown` on every ball hit (fresh hit in `OnCollisionEnter`/`OnTriggerEnter` and re-hit during rise via `BeginFallFromCurrentPosition`) and `PlayDropTargetUp` when the reset timer expires and the rise animation begins.
- `FrenzyPortal` plays `PlayFrenzyActivated` only on a successful frenzy state transition (compares `IsFrenzyActive` before and after `ActivateFrenzy`), so repeat portal entries during an active frenzy don't re-trigger the sound.

## 0.7.4 — pixelation setting, pause-menu settings access, main-menu polish, art-asset reorg
_2026-04-21 · Contributor: JJ_ (commit `fee2eda`)
**Pixelation + pause-menu settings**
- Added `PixelationSettingsManager` (auto-created singleton) that resizes the shared pixel render texture at runtime based on a saved level. Five presets: Crisp (1280x720), Smooth (960x540), Normal (640x360), Retro (400x225, default), Pixel Art (320x180). Setting persists via `PlayerPrefs` and is re-applied on each scene load.
- Added `PixelationSettingsUI` dropdown (mirrors the existing `DisplaySettingsUI`/`VolumeSettingsUI` pattern). Drops into the Settings Panel prefab with a single `TMP_Dropdown` reference.
- `PauseMenuController` now supports opening the Settings Panel from the pause menu: auto-wires a `Settings Button` under the Pause Menu panel by name, instantiates a serialized Settings Panel prefab under the pause canvas, and closes back to the pause menu when the pause action is pressed again.
- New shared `Assets/Prefabs/Settings.prefab` plus a large rework of the Main Menu `Settings Panel.prefab` (~1900-line diff) to host the new pixelation dropdown alongside the existing display/volume sections.

**Main menu polish**
- Added `SpaceshipSilverwolf` prefab instance to the main menu scene as a hero visual.
- `UIHoverBob` attached to the title so it floats (8px amplitude, 1.5 Hz bob, 1.5° rotation wobble).
- New "Team 22" label on the menu.
- Credits panel: swapped the title font credit from "Jersey 10 (Google Fonts)" to "Bacteria (somepx)" to match the new main-menu font.
- `version` text bumped to `0.7.4`.

**Art-asset reorganization**
- Moved `Fonts/`, `Materials/`, `Meshes/` (and loose sprites) under a new `Assets/ArtAssets/{Fonts,Materials,Meshes,Sprites}/` root so art is centralized instead of scattered at `Assets/` root. ~440 file moves; all references updated via GUID — no behavior change, but every prefab/scene touching those assets shows up in this commit.
- Added new fonts: **Bacteria 12**, **Desert 6**, **Manticore 14** (each with SDF assets) and a Pinballistic Steam store sprite.
- Misc ball prefab tweaks on `Gear.prefab` and `PiggyBank.prefab` that rode along with the asset move.

## 0.7.3 — Amp Up rework + tooltip runtime effects
_2026-04-20 · Contributor: JJ_
- `AmpUpBall` now destroys itself on first component hit (like the Egg) and permanently amps up the ball queued behind it in the loadout. An amped ball has a 25% chance per component hit to award +0.1 mult.
- Amped-up status persists per-loadout-slot via `BallLoadoutController._ampedUpBySlot`, survives hand rebuilds, and is re-synced to live `Ball` instances after `BallSpawner.BuildHandFromPrefabs`.
- Hover tooltips on hand balls now show the ball's active amped-up status and any pending egg multipliers (chain-multiplied across consecutive eggs queued in front).
- Removed the old AmpUp-on-drain flat-mult path (`_flatMultBonusByLoadoutSlot`, `TryApplyAmpUpBonusBehindDrainedSlot`, `ConsumePendingFlatMultBonusForLoadoutSlot`, `DrainHandler` AmpUp hook).

## 0.7.2 — retry-breaks-dragging fix
_2026-04-13 · Contributor: JJ_
- Fixed a bug where ball/offer/board-component dragging broke after dying and retrying a round. Root cause: `ServiceLocator.Get<T>()` cached a reference via its `FindAnyObjectByType` fallback and kept returning it after the Unity object was destroyed, so `RenderTextureRaycaster` saw a fake-null `UnifiedShopController` and silently blocked every drag gate.
- `ServiceLocator.Get`/`TryGet` now detect Unity-destroyed cached references, purge them, and re-resolve via the fallback.
- `UnifiedShopController` now self-registers with `ServiceLocator` in `Awake` / `OnDestroy` so the fallback path is never exercised.

### Bundled into the 0.7.3 release — contributions merged 0.7.2 → 0.7.3
_2026-04-13 to 2026-04-20 · Contributors: Devin Alvarez, DrewWhitmer, JJ_
These commits landed on `main` between the 0.7.2 and 0.7.3 changelog entries but were never given their own version bumps. Reconstructed retroactively from git (commits `e0be397`..`10220c5`).
- **Devin Alvarez — 9 new balls** (`30b9ca0`, 2026-04-13): added `Pitball`, `Snowball`, `Gear`, `Confetti` (+ `ConfettiShard` shard), `AmpUp`, `PiggyBank`, `Matryoshka`, and `CrossEyed` ball prefabs / definitions (Recall split off to a separate branch).
- **DrewWhitmer — component upgrade lights + bounce** (`e0be397`, 2026-04-13): upgrade-tier lights and bounce behavior on components.
- **DrewWhitmer — bomb component update** (`a06880d`, 2026-04-13): bomb component polish.
- **DrewWhitmer — controller support for components** (`1714324`, 2026-04-16, PR #26): gamepad navigation through the component shop/board interactions.
- **JJ — main menu tests + camera movement** (`f11843b`, `f6c9ce2`, 2026-04-16): menu-screen test scaffolding and menu camera motion pass.
- **DrewWhitmer — updated flippers** (`7619157`, 2026-04-19): flipper behavior tuning.
- **DrewWhitmer — better kickers, bumpers** (`be4bdfa`, 2026-04-19): kicker/bumper behavior + feel improvements.
- **DrewWhitmer — bouncing component upgrades** (`3d3ec82`, 2026-04-19): bouncing upgrade tier for components.
- **DrewWhitmer — focused shop** (`dc0728c`, 2026-04-20, PR #29): focused-shop layout/flow revision.
- **DrewWhitmer — new shop controller support** (`16678f6` merge / `4e19932` + `9ab57f6`, 2026-04-20): gamepad navigation through the shop + ball controller interactions.

## 0.7.1 — debug unlock-all button
_2026-04-13 · Contributor: DrewWhitmer_
- Added an "unlock everything" button to the debug menu that unlocks all balls and shop components.

## 0.7.0 — Eye On The Prize, Board_NA overhaul, ShopHub
_2026-04-12 · Contributor: JJ_
- Renamed `ChaosBall` → `EyeOnThePrizeBall` and reworked the ball prefab/behavior.
- Large `Board_NA` scene overhaul (lighting, layout, new props).
- New `ShopHub` system consolidating shop entry points.
- New `BallHandSlot` and `DropTargetResetTimerLights` board components.
- New `RenderTextureRaycaster` for UI-on-render-texture input.
- Fixed ball hand issue, removed dead code in `UnifiedShopController` and `BallSpawner`.
- Moved `ModifierPools` into `Resources/` so they load at runtime.

## 0.6.3 — frenzy mode + fireworks
_2026-04-09 to 2026-04-10 · Contributor: JJ_
- Updated drop target frenzy mode.
- Added fireworks FX.

## 0.6.2 — scoring fix
_2026-04-09 · Contributor: JJ_
- Fixed a game-breaking scoring glitch.

## 0.6.1 — FMOD restructure
_2026-04-09 · Contributors: jjanzen93, JJ_
- Restructured FMOD event naming and audio banks.
- Removed unused modifier code exposed by the audio refactor.

## 0.6.0 — modifier rework
_2026-04-09 · Contributor: JJ_
- Devil modifiers now trigger every 5 rounds.
- Omitted angel rounds and pruned unused modifier code paths.

## 0.5.2 — shop & ball particle polish
_2026-04-08 to 2026-04-09 · Contributors: JJ, DrewWhitmer_
- Added shop button and fixed several shop/gameplay bugs.
- Dynamic ball particles tied to component type and speed.
- Shop fixes and board updates.

## 0.5.1 — shop models, board UI, tooling
_2026-04-07 · Contributor: JJ_
- New shop model; ball and component type system.
- New in-game board UI.
- Added an unreferenced asset detector editor tool.

## 0.5.0 — flipper upgrades, component shop, particle refactor
_2026-04-05 · Contributors: DrewWhitmer, JJ_
- Flipper upgrades system + component shop fix.
- Ball particle script refactor; brighter particles.

## 0.4.8 — coins text fix
_2026-04-01 · Contributor: JJ_
- Fixed coins text display.

## 0.4.7 — lighting + build config
_2026-04-01 · Contributors: JJ, DrewWhitmer_
- New lighting pass across scenes.
- Removed stale tests and fixed the build name.

## 0.4.6 — game rules refactor
_2026-03-31 · Contributor: JJ_
- Split `GameRulesManager` into `DrainHandler` and `GoalScaler` for clearer separation of concerns.
- Reworked `Ball`, `ResetZone`, `DuplicatingComponent`, `AlienShip`, `ScoreManager`, and `RoundModifierController` to use the new handlers.
- Cleaned up `BasicTutorialPanelController`.

## 0.4.5 — component prefab refactor
_2026-03-31 · Contributor: JJ_
- Normalized all board component prefabs (Bumpers, Targets, Drop Target, Locked Target, Roll Over, and themed variants: Bomb, Casino, Duplicating, Fire, Frozen) to a shared structure.

## 0.4.4 — manager refactor + coin system
_2026-03-31 · Contributor: JJ_
- Extracted coin handling out of `GameRulesManager`/`ScoreManager` into a new `CoinController`.
- Updated `CoinAdder`, `ScoreJuiceFeedback`, `FloatingText`, `ComponentUIController`, and `RoundTypeIconStripUI` to use it.
- Misc tweaks to `PinballFlipper`, `BallHideController`, `BoardBackgroundMaterialSwitcher`.

## 0.4.3 — service locator standardization
_2026-03-31 · Contributor: JJ_
- Converted many singleton/`Instance` calls across the codebase to go through the service locator.
- Updated ball scripts, board components (Bumper, LockedTarget, Flipper, Launcher, Portal, ResetZone, AlienShip), FX (`AudioManager`, `CameraShake`, `HapticManager`, `ScoreJuiceFeedback`, `GoalCinematicController`), input bindings, and display settings.

## 0.4.2 — dead code cleanup
_2026-03-31 · Contributor: JJ_
- Removed unused scenes (`Game.unity`, `RolloverTest.unity`, `Test.unity`).
- Deleted unused scripts: `PointAdder`, `MultAdder`, `BoardRoundResetter`, old `DropTarget` bits.

## 0.4.1 — ships, board GUIDs, portal polish
_2026-03-31 · Contributor: JJ_
- Added `BoardComponentGuidAssigner` editor tool.
- New player ship definitions: `LoricF1`, `Silverwolf` (prefabs + assets).
- Added `Board_NA` scene and `Challenge_NA` challenge mode.
- Introduced `IBoardComponentSelectionListener`; major updates to `BoardComponent` and `MainMenu`.
- Portal Entrance prefab polish.

## 0.4.0 — baseline for automated tracking
- Starting point. Everything prior to 0.4.1 is pre-changelog history.
