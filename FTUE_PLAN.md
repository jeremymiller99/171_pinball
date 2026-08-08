# FTUE Board Plan — `Board_FTUE`

---

# ⇢ START HERE — status as of 2026-08-08

**Tickets 1–12 are done, merged to `main` (`ftue 3.3`), tree clean. CHANGELOG at 0.17.7.
Ticket 13 is the only one left. Its full specification is §13 at the bottom of this document.**

All 14 beats exist and run end to end. What ticket 13 adds is the boot rule that makes the FTUE
reachable by a real first-time player instead of only through the editor menu, plus localization
and cleanup.

## Ticket ledger

| # | Ticket | Shared files touched | Status |
|---|---|---|---|
| 1 | Editor launcher (`Pinball/FTUE/Play FTUE Board`) | none | ✅ |
| 2 | `FtueState` + director shell | none | ✅ |
| 3 | Unlosable: failure guard + always-return the ball | `GameRulesManager`, `DrainHandler` | ✅ |
| 3b | Ball save lamp stays lit in the tutorial | `DrainHandler` | ✅ |
| 4 | Legacy tutorial panels stand down | `BasicTutorialController` | ✅ |
| 5 | Dialogue view, typewriter, binding prompts | none | ✅ |
| 6 | Profile fields, v7 save migration, `Al` fallback | `ProfileSaveData`, `ProfileService` | ✅ |
| 7 | Dialogue pipeline, beats 1 / 1a | none | ✅ |
| 7b | Camera focus on the launcher | none | ✅ |
| 8 | Beats 2–4a, persistent prompts, launch lockout | none | ✅ |
| 9 | Level goals, shop pool override, unlock bypass, reroll off | `GoalScaler`, `RunPoolFilter`, `ShopOfferGenerator`, `UnifiedShopController` | ✅ |
| 10 | `OfferPurchased` event, pick-one visits | `UnifiedShopController` | ✅ |
| 11 | Beats 5–9, mult-target reveal choreography | none | ✅ |
| 12 | Beats 10–11, power surge count, return to ship, shop-prompt pause | none | ✅ |
| **13** | **Boot rule, localization, cleanup** | `MenuUI` / `MainMenuController` | **TODO — see §13** |

## Working method used throughout — keep doing this

- **One branch per ticket**, named `ftue/<phase>-<slug>`. Work is not committed by the assistant;
  jjmil commits and merges.
- **Compile gate before declaring done.** Unity cannot run headless here. Regenerate a
  `BuildCheck.csproj` from `Assembly-CSharp.csproj` with the `<Compile Include>` list rewritten from
  a walk of `Assets/Scripts`, then `dotnet build BuildCheck.csproj -v q --nologo -t:Rebuild`, then
  delete the scratch csproj. **Baseline is 31 unique warnings / 0 errors** — count normalized
  `file(line,col): warning CSxxxx` matches, because MSBuild logs each warning twice at different
  indentation and a raw line count reads ~42. See the `compile-check-without-unity` memory.
- **Every shared-file edit is a guard on `FtueState`,** written so the pre-existing code path is
  textually unchanged (§7a A8). A reviewer should be able to delete the guard and get the old file
  back.
- **CHANGELOG entry + version bump per change**, per `AGENTS.md`.

## Deviations from this plan that were made and are now settled

Do not re-litigate these; they are shipped and the reasoning is recorded in the sections named.

1. **No `FtueStepDefinition` ScriptableObjects.** Sequencing is code, copy is data
   (`FtueDialogueLine`). The beats are too heterogeneous for a generic step system — §3.3.
2. **No `FtuePlacementSlot`.** Activation timing constrains placement instead — §8b, §7a A6.
3. **`FtueState.Active` is ownership-derived**, not a bool with a reset call — §7a A2.
4. **Dialogue prefabs are direct references on the director**, not loaded from `Resources` — §3.1.
5. **No new token formatter.** `LocalizedUI.Format` already existed and is used — §3.1.
6. **The director owns its own camera tween**; only the launcher and shop-button points are
   authored, the play pose is captured — §8c.

## Known loose ends (not blocking ticket 13)

- **Gamepad prompts.** `FtueBindings` always names the keyboard binding. Doing it properly needs
  last-used-device tracking, which is wider than the tutorial should reach.
- **Narrator portrait art and an FMOD dialogue blip** — deferred by jjmil.
- **`PauseMenuController` also writes `Time.timeScale`.** The director's pause does not defer to
  it. If pressing Escape during a paused beat leaves the game at the wrong speed, that is the cause.

---

_Planning document. No code written yet. Target: a scripted first-time-user experience that runs
entirely on `Board_FTUE`, driven by a narrator character, ending with a return to the ship
(main menu)._

---

## 0. Executive summary

We are building a **scripted, linear, unlosable tutorial run** on one board. The design goal for
engineering is that **99% of the tutorial lives in new, FTUE-only code and data**, and the handful
of edits to shared systems are each a single guarded early-out. A reviewer must be able to look at
every shared-file diff and conclude in one read that `Board_Alpha`, `Board_NA` and `Board_Spinners`
behave exactly as they do today.

The three load-bearing decisions:

1. **The FTUE is a mission** (`ChallengeModeDefinition`), not a QuickRun. This buys us shop-pool
   restriction, deterministic round count, and suppression of the auto-win path for free.
2. **One `FtueDirector` MonoBehaviour lives in the board scene** and runs an authored list of
   steps. It holds every scene reference directly (camera points, trigger volumes, UI roots to
   hide) and dies with the board. No `DontDestroyOnLoad`, no name lookups.
3. **A static `FtueState` flag** is the single thing shared systems consult. Every shared-code edit
   is of the form `if (!FtueState.Active) { ...existing behaviour... }`.

Estimated scope: **4 new runtime scripts, 1 new ScriptableObject type, 3 new data assets, 1 UI
prefab, ~8 small guarded edits to existing files, plus scene authoring.** Roughly 7–8 engineer-days,
parallelisable to about 5 calendar days across two engineers.

**One item blocks Phase 3 and needs a decision before that phase starts:** the mult target is not
starter-unlocked, so the first tutorial shop would be empty. See §2.3a and §8 question 5.

**§7a is a full isolation audit** answering "can this break the existing boards?" for every shared
touchpoint. Short answer: yes it can, in four specific ways, all of which are avoidable and now
have named mitigations. The two that were *not* obvious are the profile-migration regression (A4 —
every existing player would be forced through the tutorial) and shared-prefab authoring (A5 —
`Board_FTUE` is a copy of `Board_NA` and shares `Bumper.prefab`, `Flipper.prefab` and 138 other
assets with it).

---

## 1. What already exists (verified, not assumed)

Engineers should read these before starting. Line references are to the current `master`.

| System | File | What matters for FTUE |
|---|---|---|
| Run/level loop | `Scripts/Managers/GameRulesManager.cs` | Fires `RoundStarted`, `LevelChanged`, `ShopOpened`, `ShopClosed`, `ShopAvailabilityChanged`. Owns `TryProcessLevelUps`, `ShowRoundFailed`, `OpenShop`. |
| Session/run plan | `Scripts/Managers/GameSession.cs` | `ConfigureChallenge(mission, board, ship, seed)` is the entry point we want. `GenerateRounds(n)` makes **every 5th round a Devil round**. |
| Board load | `Scripts/Managers/BoardLoader.cs` | Loads board scene additively, binds spawn point + hand slots, disables duplicate cameras/EventSystems. |
| Drain / ball save | `Scripts/Managers/DrainHandler.cs` | `OnBallDrainedRoutine` is where a lost ball either re-serves or ends the round. `TryReturnSavedBallToHand` already implements "put the ball back". |
| Input gating | `Scripts/Input/GameplayInputGate.cs` | The sanctioned block mechanism. `PinballLauncher`, `PinballFlipper` and `ShopButton3D` all already read `IsBlocked`. |
| Shop pool | `Scripts/Managers/RunPoolFilter.cs` | Single chokepoint for "may this item appear". Consulted by `ShopOfferGenerator.BuildUnlockedPool` and mystery-ball resolution. |
| Shop offers | `Scripts/UI/ShopOfferShelfController.cs` | `RebuildOffers()` regenerates from the generator on every open **and every reroll**. |
| Placement | `Scripts/UI/ShopComponentPlacementController.cs` | Placement is a **replace**, not a spawn. `IsValidPlacementTarget` requires matching `componentType`. `DiscoverBoardComponents` uses `FindObjectsByType` **without** `FindObjectsInactive.Include`. |
| Power Surge | `Scripts/BoardComponents/PowerSurgeManager.cs` | `OnPowerSurgeActivated` / `OnPowerSurgeDeactivated` events; `ActiveSource` tells us what triggered it. |
| Existing tutorial | `Scripts/UI/BasicTutorialController.cs`, `Scripts/UI/TutorialPanelView.cs` | **Conflicts with us — see §3.1.** `TutorialPanelView.Bind()` is a good pattern to copy. |
| Camera | `Scripts/FX/CameraLerpBetweenPoints.cs` | `GoToPoint(Transform)`, `IsMoving`. Uses **unscaled time**, so it animates at `timeScale = 0`. |
| Completion | `Scripts/Managers/RunCompletionHelper.cs` | Records progress, grants unlocks, shows the win screen. Probably **not** what we want for FTUE — see §2.6. |

**There is no dialogue or narrator system in the project today.** That is net-new work.

---

## 2. Design decisions and their consequences

### 2.1 Blocking prerequisite — `Board_FTUE` is not in Build Settings

`ProjectSettings/EditorBuildSettings.asset` lists 7 scenes; `Board_FTUE` is not among them.
`BoardLoader.LoadBoard` will fail with its "Is it in Build Settings?" error. **This is step 0 of
implementation and takes 30 seconds.** Nothing else can be tested until it is done.

### 2.2 The FTUE is a mission, not a QuickRun

Configure via `GameSession.ConfigureChallenge(ftueMission, ftueBoard, ship, seed)`.

What this buys us:
- `ChallengeModeDefinition.componentPoolAllowList` / `ballPoolAllowList` flow through
  `RunPoolFilter` into the shop with **zero new code** for the static part of the restriction.
- `totalRounds` is authored, not inferred.

What this **costs** us, and the team must know it:
- `GameRulesManager.ShouldCompleteRunNow()` returns `false` whenever
  `session.ActiveChallenge != null`, and `RunFlowController.ContinueAfterShopRoutine()` forces
  `boardCleared = false` for challenges. **Therefore `BoardDefinition.clearCondition` is dead code
  under a mission.** Author the FTUE `BoardDefinition` with `clearCondition = None` and treat
  completion as something the director triggers explicitly (§2.6).
- `GameSession.GenerateRounds` marks every 5th round Devil. Set the mission's `devilPool = null` so
  even if a Devil round is generated it carries no modifier, and keep `totalRounds` comfortably
  above the number of level-ups the tutorial can reach.

### 2.3 Per-visit shop restriction needs a small new mechanism

A single static allow-list on the SO cannot express *"visit 1 offers only the mult target; visit 2
offers only balls; visit 3 offers nothing new"*. Two hard constraints on how we solve it:

- The allow-lists are `[SerializeField] private` with no setter, and **mutating a ScriptableObject
  at runtime persists into the asset in the editor**. We will not go that way.
- `RebuildOffers()` regenerates on every open *and* every reroll, so a one-shot injection at open
  time would be undone by a reroll.

**Solution:** a run-scoped static override consulted **inside** `RunPoolFilter`, set by the director
before each shop opens and cleared on FTUE completion. This keeps `RunPoolFilter` as the one and
only chokepoint, and it is inert whenever the override is null (i.e. every non-FTUE run).

We should also **hide or disable the reroll button** during FTUE shop visits; the tutorial copy
assumes a fixed shelf.

### 2.3a The unlock gate runs *upstream* of `RunPoolFilter` — and it blocks the mult target

`ShopOfferGenerator.BuildUnlockedPool` checks unlocks **before** it consults `RunPoolFilter`:

```csharp
if (hasProgression && !ProgressionService.Instance.IsComponentUnlocked(def.Id)) continue;
if (!RunPoolFilter.IsComponentAllowed(def)) continue;
```

So an allow-list can only *narrow* the unlocked pool — it can never add a locked item back. On a
fresh profile (exactly the FTUE case) `ProgressionService.IsComponentUnlocked` returns true only for
`everythingUnlocked`, `ProgressionConfig.IsStarterComponent`, or an already-earned unlock.

**Verified against `Assets/Resources/ProgressionConfig.asset`:**

| Item | Starter-unlocked? | Consequence |
|---|---|---|
| `BallDefinitions/Blue Two.asset` | **Yes** | Shop visit 2 works as designed, no extra work. |
| `BallDefinitions/Red Two.asset` | **Yes** | Same. |
| `BoardComponentDefinitions/DefaultTarget.asset` (the mult target) | **No** | **Shop visit 1 will be empty on a fresh profile.** |

The nine starter components are `DuplicatingTarget`, `DuplicatingBumper`, `Fireworks`, `Matchbox`,
`Point Flipper`, `ShortCircuit`, `MooresLauncher`, `CapacitorBumper`, `Cannon`. Neither
`DefaultTarget` nor `DefaultBumper` is among them.

**This blocks Phase 3's exit criterion and needs your call (§8, question 5).** Two options:
- **(a) Add `DefaultTarget` to `starterComponents`.** One line of data, no code. But it also makes
  the plain mult target purchasable in every normal run from the first shop onward — a balance
  change, not just a tutorial change.
- **(b) Let the FTUE override bypass the unlock check.** Requires a second guard, and it must go in
  `BuildUnlockedPool` (upstream), **not** in `RunPoolFilter` — putting it in `RunPoolFilter` cannot
  work, for the reason above. Keeps normal runs untouched.

Recommendation: **(b)**, unless you already wanted the mult target to be a starter for balance
reasons. It is a slightly larger diff but it cannot leak into live progression.

### 2.4 "Impossible to lose" is four call sites, not one

_Corrected during ticket 3 — the first draft listed three; a full grep found a fourth._

Round failure can be reached from:
1. `DrainHandler.OnBallDrainedRoutine` — the `rules.BallsRemaining > 0` else-branch.
2. `GameRulesManager.StartRound()` — when the first ball spawn returns null.
3. `GameRulesManager.TriggerRoundFailed()` — public, callable by anything.
4. **`PiggyBankBall.cs:93`** — `rules?.ShowRoundFailed()`. Not in the tutorial loadout today, but it
   is exactly the kind of caller that makes per-caller guards the wrong shape.

**Cheapest correct seam: an early-out at the top of `ShowRoundFailed()` itself.** One guard, all
three paths. It also suppresses the Steam/local leaderboard uploads, `LogRunHighScore` and
`LogRunLevelReached` that `ShowRoundFailed` performs — which we very much do not want firing from a
tutorial.

That guard alone stops the *fail screen* but does not put the ball back. For the return-to-launcher
we reuse `DrainHandler.TryReturnSavedBallToHand`, but:
- **bypass `IsWithinBallSaveWindow`** — the 8-second save window is wrong for a tutorial where the
  player may sit and read a dialogue box;
- **skip `ConsumeActiveBallFromLoadout`** — this is what keeps the loadout non-empty so the re-serve
  actually succeeds. Worth a comment in the diff; it is not obvious.

**SHIPPED IN TICKET 3 — one clause, not a new code path.** Rather than adding an explicit re-serve,
the existing ball-save path is simply allowed to always fire in the tutorial:

```csharp
bool ballSaved = eligibleForBallSave
    && (FtueState.SuppressRoundFailure || IsWithinBallSaveWindow(ball));
```

`TryReturnSavedBallToHand` then does the rest exactly as it already does for a real ball save: it
leaves the loadout untouched (so `BallsRemaining` never drops) and puts a fresh copy of the *same*
ball at the front of the hand, which the existing `SpawnBall()` below serves at the launcher. The
ball-saved VFX comes along with it, which is desirable here — the player should notice the ball
came back.

Keeping `eligibleForBallSave` in the expression is load-bearing: balls that consume themselves by
design (Molotov breaking, Holoball expiring) must not be resurrected, and that flag is what already
distinguishes them from a genuine drain.

**Recovery, in case the save ever fails to hold.** `BallSpawner.ActivateNextBall()` spawns a ball at
`BoardRoot.SpawnPoint` when the hand is empty rather than returning null, so the guard in
`ShowRoundFailed` re-serves and logs a warning instead of stranding the player with no ball. There
is no double-spawn risk: both callers only reach `ShowRoundFailed` on the branch where they did
*not* spawn.

Net effect: in FTUE, every drain silently re-serves at the launcher. Design intent is that the
player *notices* the ball came back, so the narrator should acknowledge it (see step list §4).

### 2.5 `BasicTutorialController` will fight us

It bootstraps via `[RuntimeInitializeOnLoadMethod]`, is `DontDestroyOnLoad`, subscribes to
`RoundStarted` / `ShopAvailabilityChanged` / `ShopOpened`, and gates only on
`ProfileService.HasSeen*Tutorial()`. **On a fresh profile — precisely the FTUE case — its CONTROLS
and LEVEL UP panels will render on top of our narrator dialogue.**

Plan:
- Suppress all three of its panels while `FtueState.Active`.
- On FTUE completion, mark `hasSeenFirstPlayTutorial`, `hasSeenLevelUpTutorial` and
  `hasSeenShopTutorial` as seen — the FTUE taught all three lessons better.
- Add a new `hasCompletedFtue` field to `ProfileSaveData` / `ProfileService`, mirroring the existing
  three. This is what the boot flow reads to decide whether to skip the main menu.

### 2.6 Completion routes to the ship, not the win screen

`RunCompletionHelper.RecordProgressAndShowWinScreen` uploads leaderboard scores, unlocks the
"first win" achievement and shows the win/rank screens. For a tutorial that is wrong on every
count. FTUE completion should:
1. set `hasCompletedFtue` + the three legacy tutorial flags,
2. clear `FtueState` and the shop pool override,
3. `SceneFader.Instance.FadeAndLoadScene("MainMenu 1")`.

No leaderboard, no rank, no achievement.

**Scene name verified.** `"MainMenu 1"` is the live ship/menu scene — it is what
`RunFlowController`, `PauseMenuController`, `RoundFailedPanelController` and `MenuUI` all load.
`MainMenuController.cs:669` loads `"MainMenu"`, but its own comment marks that as *"the legacy
main-menu scene"*. Use `"MainMenu 1"`.

### 2.6a The 3-ball starting hand is data, not code

The brief specifies the player is "populated with 3 pinballs", and the visit-2 narrator copy says so
out loud. `BallLoadoutController` defaults to `startingMaxBalls = 5` and lives in **GameplayCore**,
shared by every board — so this is not scene authoring.

The clean lever already exists: `BallLoadoutController.InitializeForNewRun()` prefers
`GameSession.ActiveShip` when one is set, reading `startingMaxBalls` and `startingHand` from it.

**Create a dedicated `FTUE_Ship.asset` (`PlayerShipDefinition`)** and pass it as the `ship` argument
to `ConfigureChallenge`. It gives us, as pure data and with zero code:
- `startingMaxBalls` + a 3-entry `startingHand`,
- `startingCoins` (see the affordability risk in §6),
- its own `ballPoolAllowList` / `componentPoolAllowList`, which **intersect** with the mission's —
  useful as a second static safety net, though the per-visit override in §2.3 still does the real
  work.

Owned by Phase 0 (asset creation) and Phase 4 (tuning the numbers).

### 2.7 Copy corrections needed (design decisions, flagging for you)

Two lines in the brief do not match the current bindings:

- **"hold ENTER to launch" is wrong.** Launch is bound to `<Keyboard>/space`, `<Keyboard>/s`,
  gamepad South, and middle mouse. Enter belongs to the **UI** action map. Either rebind Launch or
  change the copy. **Recommendation:** have the dialogue read the binding's display string at
  runtime so the copy can never drift from the bindings again — it is a few lines with
  `InputAction.GetBindingDisplayString()`.
- **"press CONTROL to activate the shop"** — you already said to disregard this; noting it so it
  does not get lost. Shop entry today is `ShopButton3D` (click, or its `shopAction` hotkey). The
  same runtime-binding-string approach solves this line too.

`"use the LSHIFT and RSHIFT"` **is** correct — those are the index-0 keyboard bindings for
LeftFlip / RightFlip.

### 2.8 The mult-target placement spot

Placement replaces an existing component of the same type. So:
- The "designated spot" must already hold an **active** `BoardComponent` whose `componentType`
  matches the mult target's. A disabled placeholder is invisible to
  `DiscoverBoardComponents()` (no `FindObjectsInactive.Include`) and will not work.
- Conveniently, that same omission means **the bumpers we want locked out of the tutorial can
  simply be disabled GameObjects** and they will neither be hit nor be placement targets.
- ~~Restricting placement to *exactly one* spot is genuinely new work.~~ **Superseded by §8b** — it
  turns out discovery runs on shop *open*, so activating only `MultTarget (2)` beforehand makes it
  the only possible drop spot, with no code change at all.

### 2.9 Pause and input — one trap

Use `GameplayInputGate.Block(this)` / `Unblock(this)`. Do **not** copy
`BasicTutorialController.PauseAndLockInput()`, which disables behaviours by type-name string search.

**The trap:** `PinballLauncher.Update` dumps `_charge` to 0 on *every frame* while the gate is
blocked. So the "hold to launch" beat requires the gate to be **released before** the prompt is
shown, or the plunger will never accumulate charge and the player will be stuck. Sequence must be:
zoom camera → show dialogue → dismiss dialogue → **unblock** → prompt persists → player launches.

Helpfully, `CameraLerpBetweenPoints` uses unscaled time, so camera moves still animate at
`timeScale = 0` if we do choose to hard-pause for a beat.

---

## 3. Architecture

### 3.1 New files

```
Assets/Scripts/FTUE/
  FtueState.cs              static; Active flag + shop pool override        [DONE ticket 2]
  FtueDirector.cs           MonoBehaviour, lives in Board_FTUE              [shell, ticket 2]
  FtueBindings.cs           input-binding display strings for prompts       [DONE ticket 5]
  FtueDialogueView.cs       prefab-root view; Bind / BindTextEntry          [DONE ticket 5]
  FtueStepDefinition.cs     ScriptableObject: one authored beat             [ticket 7]
  FtuePlacementSlot.cs      WITHDRAWN — see §8b / §7a A6
```

**Ordered-token formatting already exists — do not build a second one.**
`LocalizedUI.Format(key, fallbackTemplate, params object[] args)` is already in the project and does
exactly what §8a asks for: pulls the localized template, fills `{0}`/`{1}` via `String.Format`, and
falls back twice (localized → source template → raw) if a translator breaks the placeholders. The
FTUE builds every line through it. `FtueBindings.Display(reference)` supplies the binding argument.

**Dialogue prefabs live in the board scene's references, not in `Resources`.**
Earlier drafts specified `Assets/Resources/FTUE/FtueDialoguePanel.prefab`, copying
`BasicTutorialController` — but that controller only needs `Resources` because it is a
`DontDestroyOnLoad` singleton with no scene to hold references. `FtueDirector` *is* in the scene, so
it holds direct serialized prefab references instead. That removes a runtime load, a path constant
and a whole missing-at-runtime failure mode, and keeps the prefabs out of every build that does not
need them. Author them anywhere sensible (`Assets/Prefabs/FTUE/` suggested).

```
Assets/Data/FTUE/                       [DONE — authored 2026-08-07]
  Mission_FTUE.asset        ChallengeModeDefinition (devilPool = null, boards -> Board_FTUE)
  Board_FTUE.asset          BoardDefinition (clearCondition = None, missions = [])
  Ship_FTUE.asset           PlayerShipDefinition — startingMaxBalls 5, 3x Pinball startingHand,
                            startingCoins 100 (§2.6a)
```

_Asset names are `<Type>_FTUE`, not the `FTUE_<Type>` used in earlier drafts of this document._
`startingMaxBalls 5` with a 3-entry `startingHand` is intentional and correct: `InitializeForNewRun`
seeds the hand from `startingHand` (3 balls) while `maxBalls` sets the ceiling (5), which is exactly
the headroom shop visit 2 needs to sell a fourth ball.

### 3.2 `FtueState` — the shared contract

Static, tiny, and the **only** thing shared systems reference. Keeping it static rather than a
service means the guards in shared files are one-liners with no null dance and no lookup cost in
the hot path.

**Shipped in ticket 2:**
- `Active` — ownership-derived, see §7a A2. True while a live `FtueDirector` holds it.
- `SuppressRoundFailure` — read by `GameRulesManager.ShowRoundFailed` (ticket 3). Starts on with
  `Activate`, since the tutorial is unlosable from its first ball.
- `Activate(Object)` / `Deactivate(Object)` — ownership lifecycle. `Deactivate` ignores a caller
  that is not the current owner, so a late teardown cannot switch off a live tutorial.
- `SetRoundFailureSuppressed(bool)` — lets the completion beat hand failure back to the game.
- `Reset()` — explicit valve for completion.

**Deferred to ticket 9,** where they are first consumed and therefore first testable:
- `AllowedComponentsThisVisit` / `AllowedBallsThisVisit` — nullable overrides read by
  `RunPoolFilter`. Null = no override. Deliberately *not* added early: unused serialized/static
  API is dead weight, and the project's compile gate flags unread fields.

### 3.3 `FtueDirector` — the state machine

Lives in `Board_FTUE`, holds serialized references to:
- the narrator dialogue canvas root,
- the launcher-zoom and play-pose **point transforms** (authored in the board scene — but see
  §3.3a: the camera rig they drive is *not* in the board scene),
- the mid-board "save the ball" trigger volume (already authored in the scene),
- **`GameObject[] multUiGroup`** — the mult bar / mult screen objects to toggle,
- **`GameObject[] dropTargetGroup`** — the drop-target bank objects to toggle,
- **`GameObject placeableMultTarget`** (`MultTarget (2)`) and **`GameObject[] extraMultTargets`**
  (`MultTarget`, `MultTarget (1)`) — see §8b,
- **`GameObject[] lockedOutComponents`** — bumpers and anything else disabled for the tutorial,
- the authored launcher-zoom point (§8c).

**Every one of these is a plain `GameObject[]` the designer fills in the inspector**, toggled with
`SetActive`. No name lookups, no `Find`, no assumptions about hierarchy — the exact objects are the
designer's call and can change without touching code. Per §7a A5 these are **scene-instance toggles
only**; nothing here may be done by editing a shared prefab.

It subscribes to `GameRulesManager.ShopAvailabilityChanged` / `ShopOpened` / `ShopClosed` /
`LevelChanged`, `PowerSurgeManager.OnPowerSurgeActivated`, `PinballLauncher.BallLaunched`, and the
trigger volume, then advances an authored step index. Each step declares: what fires it, what copy
to show, whether to hard-pause, what to enable/disable on entry and exit.

**Author the steps as data (`FtueStepDefinition` assets), not as a `switch` in code.** You will
retune this copy and pacing many times and you do not want a recompile for each pass.

### 3.3a The launcher zoom needs a camera mechanism we do not have yet

`CameraLerpBetweenPoints` — the waypoint lerper §3.3 originally assumed — is the **main menu**
camera script. It is not on the gameplay rig.

The gameplay camera is `CameraRig`, and it lives in **`GameplayCore`**, not in the board scene. Only
two things move it today:
- `CameraIntroPan` — a one-shot, offset-based slide (`startLeftOffset` → rest). No waypoints, no
  general "go to this transform" API.
- `ShopTransitionController` — pans it on local X when the shop opens.

So the "zoom in on the launcher" beat needs a decision, and the FTUE points are inherently
**cross-scene**: the target transforms are authored in `Board_FTUE`, the rig they drive is in
`GameplayCore`. The director resolves the rig at runtime; it cannot be a plain serialized reference.

Options:
- **(a)** Add a `CameraLerpBetweenPoints` to the `GameplayCore` `CameraRig` and have `FtueDirector`
  push board-scene transforms into `GoToPoint()`. Reuses tested code; touches a shared scene.
- **(b)** Give `FtueDirector` its own small tween over the rig transform. No shared-scene change,
  slight duplication. **Recommended** — it keeps the whole mechanism inside FTUE-only code, which is
  consistent with the isolation rules in §7a.

**Either way, guard against `ShopTransitionController`.** Both would be driving the same transform,
and a launcher zoom still in flight when the shop opens would fight the shop pan. The director must
finish (or cancel) any camera move before it lets the shop-available beat fire.

### 3.4 Guarded edits to shared code (the complete list)

Each of these is a single early-out or a single branch. Reviewers should see nothing else.

| File | Edit |
|---|---|
| `GameRulesManager.cs` | Top of `ShowRoundFailed()`: if FTUE suppression is on, re-serve and return. |
| `DrainHandler.cs` | In `OnBallDrainedRoutine`, FTUE path forces the return-to-launcher (bypasses the save window, skips loadout consumption). |
| `RunPoolFilter.cs` | `IsBallAllowed` / `IsComponentAllowed` consult the per-visit override before the existing ship/mission checks. |
| `ShopOfferGenerator.cs` | **Only if §2.3a option (b) is chosen:** `BuildUnlockedPool` lets the FTUE override bypass the upstream unlock check. Must be here, not in `RunPoolFilter`. |
| `BasicTutorialController.cs` | Each of the three `Show*Panel` entry points early-outs while FTUE is active. |
| `UnifiedShopController.cs` | Add `public event Action<ShopOffer> OfferPurchased`, invoked at the three existing purchase sites. **Purely additive** — inert with no subscribers, so other boards are unaffected. Needed for the shop-2 "pick one" rule (§8g). |
| ~~`ShopComponentPlacementController.cs`~~ | **No longer needed.** Superseded by §8b — activating only `MultTarget (2)` before the shop opens constrains placement with zero shared-code change. Kept as a documented fallback if the Phase-3 check in §8b fails. |
| `ProfileSaveData.cs` / `ProfileService.cs` | New `hasCompletedFtue` bool **and `aiName` string** (§8a) + accessor pairs, mirroring the existing three flags. **Plus a save migration** — bump `currentVersion` 6 → 7 once, covering both fields, and grandfather existing profiles (§7a A4). |
| `EditorBuildSettings.asset` | Add `Board_FTUE`. |
| Boot flow (`MenuUI` / `MainMenuController`) | If `!hasCompletedFtue`, configure the FTUE mission and load `GameplayCore` directly instead of showing the menu. |

Anything beyond this list is a scope change and should come back for discussion.

---

## 4. The authored beat list

This is the spec the director's step assets encode. Each row is one `FtueStepDefinition`.

| # | Fires on | Beat | Systems touched |
|---|---|---|---|
| 1 | Run start, after board load + intro hold | Narrator: *"Welcome operator, I've got you set up on the most basic model I could find. In case you forgot, we're here to generate power, and this is about the bare minimum we need to do so."* | Input gate blocked. Mult UI group hidden. Drop-target group hidden. **All three mult targets inactive.** Non-tutorial bumpers disabled. |
| **1a** | Player dismisses #1 | **Naming beat (§8a).** The AI asks the player to name it, takes text entry, then adopts the name and acknowledges it in character. | Writes `ProfileSaveData.aiName`. Every later line substitutes it as `{0}`. |
| 2 | Player dismisses #1a | Camera tweens to the authored launcher point. Narrator: *"Hold `{1}` to launch."* Prompt persists. | **Unblock the gate before showing the prompt** (§2.9). Director-owned tween (§8c); play pose was captured at run start. |
| 3 | `PinballLauncher.BallLaunched` | Camera returns to play pose. Dialogue clears. Ball plays through the top-of-board bumpers. | Camera to play point. |
| 4 | Ball enters the mid-board trigger (below the bumpers, above the flippers) | Hard pause. Narrator: *"I never said this was going to be easy. Use `[LEFT FLIP]` and `[RIGHT FLIP]` to keep the machine going."* | `timeScale = 0` + gate blocked. Trigger disarms itself. |
| 4a | Ball drains at any point | Ball silently returns to the launcher. Narrator acknowledges it (short line — the player must understand this is a tutorial affordance, not a bug). Step 2's launch prompt re-arms. | FTUE failure suppression + forced re-serve. |
| 5 | First `ShopAvailabilityChanged(true)` | Narrator: *"You've generated enough power to activate the cargo beacon. Press `[SHOP]` to activate."* Shop button highlighted. | Suppress `BasicTutorialController`'s level-up panel. |
| **5a** | On `ShopAvailabilityChanged(true)`, **before** `OpenShop()` runs | Activate `MultTarget (2)` only. Silent — no dialogue. | Timing is load-bearing: discovery runs in `UnifiedShopController.OnEnable` (§8b). |
| 6 | `ShopOpened` (visit 1) | Shop shelf contains **only the mult target**. Narrator walks through: click to inspect → drag to place → confirm to buy. Reroll disabled. | Per-visit pool override + unlock bypass. `MultTarget (2)` is the only active target-type component, so it is the only possible drop spot — no marker component needed. |
| 7 | Placement confirmed | Narrator explains multiplier, then: *"I've added a few more to get you off the ground."* → **activate `MultTarget` and `MultTarget (1)`**. **Mult UI group enabled** for the rest of the run. | Enable the serialized mult UI group + the two remaining target GameObjects. |
| 8 | `ShopClosed` → play → next `ShopAvailabilityChanged(true)` | Narrator prompts the shop again. | — |
| 9 | `ShopOpened` (visit 2) | Shelf contains **exactly Red Two and Blue Two — pick one** (§8g). Narrator: balls are your lifeline; you start with 3; you can buy more; you can reorder them by dragging in the hand. Plus the two-colour-choice gag. | Per-visit pool override = the two ball defs. **On `OfferPurchased`, consume the sibling offer** so only one can be taken. |
| 10 | `ShopClosed` → play → next `ShopAvailabilityChanged(true)` → `ShopOpened` (visit 3) | **Drop targets appear.** Narrator introduces Power Surge: money *and* a large score boost, and the existence of components more complex than bumpers and mults. | Enable drop-target bank root. Pool override = empty (nothing new to buy). |
| 11 | `PowerSurgeManager.OnPowerSurgeActivated` ×3 | Count surges. On the third: narrator sign-off, then return to the ship. | `FtueState.Reset()`, set profile flags, `FadeAndLoadScene("MainMenu 1")`. |

**Open design question for you:** step 10 says drop targets appear *at* the third shop. It may read
better if they appear on the *board* at the level-up (so the player sees the board change) and the
shop visit is where the narrator explains them. Cheap to try both once the director is data-driven.

---

## 5. Work breakdown and sequencing

Ordered so that each phase is independently testable. Phases 1–2 are the critical path; 3–5 can be
parallelised across two engineers.

### Phase 0 — Unblock (1 hr, anyone) — **CODE COMPLETE, awaiting in-editor verification**
- ~~Add `Board_FTUE` to Build Settings.~~ **DONE** — verified present in `EditorBuildSettings.asset`.
- ~~Create the three data assets.~~ **DONE** — see §3.1. Cross-references verified:
  `Mission_FTUE.boards` → `Board_FTUE`, `Board_FTUE.missions` empty (§7a A3 rule holds),
  `startingHand` = 3× `Pinball`.
- ~~Temporary debug entry point.~~ **DONE** — `Assets/Scripts/FTUE/FtueDebugBoot.cs`, editor-only,
  menu item `Pinball/FTUE/Play FTUE Board`. Zero shared files modified. Compile gate: 0 errors /
  31 warnings, no new warnings.
- **Exit criterion (needs a human in the editor):** `Board_FTUE` loads, is playable as a normal
  board, and the player starts with exactly 3 balls in hand.

### Phase 1 — Skeleton and safety (1 engineer, ~1 day)
- `FtueState`, `FtueDirector` shell with an empty step list.
- Round-failure suppression + forced re-serve (§2.4). **Test the skip-consumption-only path first**
  — it may make the explicit re-serve unnecessary.
- `BasicTutorialController` suppression (§2.5).
- **Exit criterion:** you cannot lose on `Board_FTUE`; the ball always comes back to the launcher;
  no legacy tutorial panels appear on a wiped profile.

### Phase 2 — Dialogue and director (1 engineer, ~3 days)
- `FtueDialogueView` + prefab (copy the `TutorialPanelView.Bind` pattern), **plus the text-entry
  variant** for the naming beat.
- **Ordered-token formatting in `Bind` from day one** (§8a) — both the AI name and the binding
  strings are runtime substitutions, so concatenation is not an option.
- `ProfileSaveData.aiName` + accessors, with trim / length clamp / fallback.
- `FtueStepDefinition` + the director's step machine, its own camera tween (§8c), input gating.
- Runtime binding-display-string helper for launch / flipper / shop tokens (§2.7).
- Beats 1, **1a**, 2–4a wired.

_Estimate raised from 2 days: the naming beat adds a prefab variant, a step type, a profile field
and the token-formatting requirement._
- **Exit criterion:** a player can boot, hear the intro, launch on prompt, get the save-the-ball
  lesson, drain, and get the ball back — with no dead ends.

### Phase 3 — Shop scripting (1 engineer, ~2 days, parallel with 4)
- **Resolve §2.3a first** — the mult target is not starter-unlocked, so nothing else in this phase
  can be verified until the unlock question is answered.
- `RunPoolFilter` per-visit override **and** the `BuildUnlockedPool` unlock bypass (option (b) is
  confirmed — §8 decision 1).
- Reroll suppression during FTUE.
- **Verify the §8b assumption:** that `MultTarget (2)` is the only *active* target-type component
  when shop visit 1 opens. Only if it is not, fall back to the `FtuePlacementSlot` marker.
- Beats 5, **5a**, 6–9, including the "I've added a few more" reveal.
- **Exit criterion:** shop visit 1 offers exactly the mult target and it can only be dropped on
  `MultTarget (2)`; the other two appear after purchase; visit 2 offers exactly the tutorial balls.

### Phase 4 — Board state and Power Surge (1 engineer, ~1 day, parallel with 3)
- Scene authoring: disable the non-tutorial bumpers, hide the drop-target bank, hide the mult UI at
  start, place the mid-board trigger, author the camera points.
- Mult UI reveal on placement; drop-target reveal; surge counting.
- Beats 10–11 and the return-to-ship completion path.
- **Exit criterion:** three power surges end the tutorial and land the player on the main menu with
  `hasCompletedFtue` persisted.

### Phase 5 — Boot flow and polish (~1 day)
- Real boot integration: `!hasCompletedFtue` → straight to FTUE.
- **Save migration (§7a A4):** bump `currentVersion` to 7 and grandfather existing profiles.
  Ship the boot rule and the migration in the *same* commit — a boot rule without the migration
  forces every existing player through the tutorial.
- Localization keys for all narrator copy (follow the existing `LocalizedUI.Get(key, fallback)`
  pattern — do **not** hardcode strings; every other panel in the project is localized).
- Audio/VFX pass on the narrator; FMOD event for the dialogue blip.
- CHANGELOG entry + version bump per `AGENTS.md`.

---

## 6. Risks and how we retire them

| Risk | Why it bites | Mitigation |
|---|---|---|
| **Ball gets stuck / never reaches the mid-board trigger** | Physics is not deterministic; a bad launch can drain straight down the outlane and skip the lesson. | Trigger arms on first launch and stays armed across re-serves until it fires once. Never a hard requirement to pass through a specific volume. |
| **Player level-ups faster or slower than the beat list expects** | Goal scaling is shared; a lucky surge could trigger two level-ups before the narrator finishes. | Beats fire on the *first* `ShopAvailabilityChanged(true)` after a step, not on an absolute level index. The director queues rather than drops. |
| **Hard-pause at `timeScale = 0` interacts badly with the tally/drain coroutines** | `DrainHandler` and `ScoreTallyAnimator` are coroutine-driven; some yield on scaled time. | Prefer `GameplayInputGate` over `timeScale = 0`. Reserve a true pause for the step-4 beat only, and verify the tally is not mid-flight when it lands. |
| **ScriptableObject mutation leaks into assets** | Editor-time SO writes persist. | The per-visit override is a static in `FtueState`, never a write to the mission asset. Called out in review checklist. |
| **Placement slot marker breaks other boards** | It touches a shared code path. | Marker is opt-in: the guard only applies when at least one `FtuePlacementSlot` exists in the loaded scene. Other boards have none, so behaviour is byte-identical. |
| **Player cannot afford the scripted purchase** | The tutorial *requires* buying the mult target and a ball. `coinsPerLevelUp` is 10, Power Surge pays 1–3, and offer prices vary. A player who reaches shop 1 with too few coins hits a hard dead end — the narrator tells them to buy something they cannot buy. | Set `FTUE_Ship.startingCoins` high enough to cover both scripted purchases outright. Belt-and-braces: the director asserts affordability on `ShopOpened` and tops the player up if short. **Do not** rely on level-up income. |
| **Fresh-profile testing is easy to get wrong** | Every flag is sticky once set. | Add a debug menu item to wipe FTUE + tutorial flags. QA will need it dozens of times. |

---

## 7. Definition of done

- A wiped profile boots directly into `Board_FTUE` with no main menu.
- All 11 beats fire in order, with correct copy and correct binding names.
- The player starts with exactly 3 balls and can always afford every scripted purchase.
- The player cannot lose, cannot get stuck, and cannot buy anything off-script.
- Three power surges return the player to the ship with flags persisted.
- A second launch of the game goes to the main menu as normal.
- **`Board_Alpha`, `Board_NA`, `Board_NA 1` and `Board_Spinners` play identically to before** —
  verified by the full §7a verification protocol, not just a smoke test.
- An existing save file still boots to the main menu.
- No FTUE assets appear on the star map, in ship select, or in any shop on a normal board.
- Compile gate passes: regenerate `Assembly-CSharp.csproj`'s source list and confirm the baseline
  **0 errors / 31 warnings** (Unity cannot run headless here, so this is our only automated check).
- CHANGELOG entry added and version bumped per `AGENTS.md`.

---

## 7a. Isolation audit — will this break `Board_NA`, `Board_NA 1`, `Board_Alpha`, `Board_Spinners`?

Every shared touchpoint was checked against the actual code, not assumed. Results below. **Two real
hazards were found that were not in the first draft of this plan** (A3 and A4), plus one that is the
single most likely way a teammate breaks the main game in practice (A5).

### A1 — Adding `Board_FTUE` to Build Settings: **SAFE, verified**
Adding a scene appends a build index and could in principle renumber things. Verified there is **no
scene-index usage anywhere in `Assets/Scripts`** — zero hits for `LoadScene(<int>)`,
`LoadSceneAsync(<int>)`, `buildIndex`, or `GetSceneByBuildIndex`. Every load is by name. Appending
`Board_FTUE` cannot affect any other scene.

### A2 — Static `FtueState` leaking between runs: **SAFE only if we reset at run start**
The project has fast-enter-play-mode configured (`m_EnterPlayModeOptionsEnabled: 1`,
`m_EnterPlayModeOptions: 0` in `ProjectSettings/EditorSettings.asset`). **Do not build the design on
an assumption about whether domain reload clears statics** — that setting pair is easy to
misread and easy for someone to change later. If you want to know, check the Editor ▸ Enter Play
Mode Settings panel directly.

The requirement holds either way, because domain reload was never the risky case. The risky case is
**FTUE → main menu → start a normal run, all within one session** — statics survive that regardless
of any editor setting.

**RESOLVED IN TICKET 2 — better than the original requirement.** The first draft asked for
`FtueState.Reset()` at the start of every run, which puts the burden on discipline (and would have
meant a shared-code edit to `GameRulesManager.StartRun`). The shipped design instead derives
`Active` from **ownership of a live `FtueDirector`**, mirroring `GameplayInputGate`:

- The director exists only in `Board_FTUE`. `BoardLoader` unloads that scene on the way anywhere
  else, which destroys it.
- Unity reports a destroyed object as null, so `FtueState.Active` falls to false **by itself** —
  including when the tutorial exits through a path nobody wrote cleanup for: a Quit button, an
  exception mid-beat, a scene load, a play-mode stop.

This is strictly stronger than a reset call, because there is no code path that can *forget* to run
it, and it needs **no edit to any shared file**. `Reset()` still exists as an explicit valve for the
completion beat. The domain-reload question is moot either way.

### A3 — Asset placement can silently add the FTUE to live menus: **REAL LEAK — do not put FTUE assets in `Resources`**
`StarMapMissionCatalog` self-populates from Resources:

```csharp
public const string BoardResourcePath = "BoardDefinitions";
public const string ShipResourcePath  = "PlayerShipDefinitions";
Resources.LoadAll<BoardDefinition>(BoardResourcePath);
Resources.LoadAll<PlayerShipDefinition>(ShipResourcePath);
```

`Resources/BoardDefinitions/` currently holds exactly the four real boards;
`Resources/PlayerShipDefinitions/` holds `LoricF1` and `Silverwolf`. **Dropping `FTUE_Board.asset`
or `FTUE_Ship.asset` into those folders would automatically list the tutorial board on the star map
and the tutorial ship in ship selection.** This is exactly the "keep it consistent with the other
assets" move a teammate would make without thinking.

Rules, to be enforced in review:
- FTUE data assets live in **`Assets/Data/FTUE/`** — outside any `Resources` folder. They are reached
  by direct serialized reference from the boot component, never by `LoadAll`.
- `FTUE_Ship.asset` must **not** be added to `ProgressionConfig.starterShips`.
- `FTUE_Mission.asset` must **not** appear in any `BoardDefinition.missions[]`, in
  `MenuUI.quickRunBoards` / `challengeBoards`, or in `Monitor2Controller.availablePlayfields`.

Not a concern: `RunItemCatalog` also does `Resources.LoadAll` on `BallDefinitions` and
`BoardComponentDefinitions`, but we are not adding any ball or component definitions.

### A4 — Existing players get dumped into the tutorial: **REAL REGRESSION — needs a save migration**
`hasCompletedFtue` defaults to `false`. The Phase 5 boot rule is "if `!hasCompletedFtue`, go to
FTUE". **Every existing player with a save file would therefore be forced through the tutorial on
their next launch.**

`ProfileService` already has the machinery to fix this properly — `private const int
currentVersion = 6` and an `UpgradeInPlaceIfNeeded(ProfileSaveData)` with staged
`if (data.version < N)` blocks. The fix:

- Bump `currentVersion` to `7`.
- Add an `if (data.version < 7)` block that sets `hasCompletedFtue = true` **unconditionally**.

Grandfather on *version alone*, not on a "has this player actually played?" predicate. A predicate
like `totalBoardWins > 0 || totalPointsScored > 0 || hasSeenFirstPlayTutorial` looks thorough but
misses the player who created a profile, launched once and quit without finishing a ball — they
would be pushed into the tutorial. Version-based grandfathering has **zero false positives**, which
is the right trade when the stated priority is not breaking the existing game. The cost is that a
handful of barely-started existing profiles never see the FTUE; that is strictly the safer failure.

- Fresh profiles come from `CreateNewProfile()`, which stamps `version = currentVersion` and leaves
  the new bool `false`. So new players get the FTUE, correct by construction, and the migration
  block never runs for them.

**Watch out:** line 624 reads `if (data.version <= 0) { data.version = currentVersion; }` — a
profile with version 0 is stamped current and **skips every migration block below it**, so it would
land with `hasCompletedFtue = false` and be sent to the tutorial. Pre-existing behaviour, but our
change is the first one where skipping a migration is user-visible. Worth handling in the same pass.

### A5 — Prefab edits during scene authoring: **the biggest practical risk**
`Board_FTUE` shares **140 of its 150 asset references with `Board_NA`** — it is a copy. The shared
set includes precisely the objects the FTUE plans to modify:

`Bumper.prefab`, `Flipper.prefab`, `Sling.prefab`, `roll over.prefab`, `Dropper.prefab`,
`Launcher Zone.prefab`, `Portal Entrance.prefab`, `Portal Exit.prefab`, `Pinball.prefab`

**Anything changed at the prefab level while authoring `Board_FTUE` lands on `Board_NA`,
`Board_NA 1` and any other board using it.** Unity makes this a one-click mistake: double-click a
scene instance, tweak it, save.

Rules, to be enforced in review:
- "Disable the non-tutorial bumpers", "hide the drop-target bank", "hide the mult UI at start" are
  **scene-instance overrides only** — toggle the GameObject in the `Board_FTUE` hierarchy. Never
  open the prefab.
- If the FTUE genuinely needs a *behavioural* variant of a shared prefab, make an **FTUE-specific
  prefab variant** in `Assets/Prefabs/FTUE/`, do not edit the base.
- Reviewer check: `git status` on a Phase-4 branch should show changes to `Board_FTUE.unity` and
  **no** changes under `Assets/Prefabs/`. If a prefab is dirty, it is a bug until proven otherwise.

### A6 — The placement-slot guard: **WITHDRAWN — no longer part of the plan**

**Superseded by §8b.** Placement discovery runs on shop *open* and ignores inactive objects, so
activating only `MultTarget (2)` beforehand constrains the drop with **zero** shared-code change.
That is strictly safer than any guard, and it removes `ShopComponentPlacementController` from the
shared-edit list entirely. The original analysis is retained below only as the fallback design, in
case the Phase-3 check in §8b shows other target-type components must stay active.

<details>
<summary>Fallback design (only if §8b's check fails)</summary>
`IsValidPlacementTarget` is `public static` with three callers — twice inside
`ShopComponentPlacementController` (hover paths) and once in `UnifiedShopController.cs:307` (the
actual commit). Putting the guard inside the static method covers all three, which is what we want.

But `UpdateDragHover` runs **every frame during a drag**, so the guard must not do a
`FindObjectsByType` lookup. Implement `FtuePlacementSlot` with a **static registry** it adds itself
to in `OnEnable` and removes itself from in `OnDisable`. The guard becomes:

> if the registry is non-empty, additionally require the hit component to be registered.

On every other board the registry is empty, the guard short-circuits on a count check, and behaviour
is byte-identical. No per-frame allocation, no lookup.

</details>

### A7 — The one change that *would* affect other boards
§2.3a option **(a)** — adding `DefaultTarget` to `ProgressionConfig.starterComponents` — makes the
plain mult target purchasable from the first shop in **every** run, on every board. That is a live
balance change, not a tutorial change. It is the only proposal in this document with intentional
reach outside the FTUE, and it is why the recommendation is option **(b)**, the scoped bypass.

### A8 — Guard style, so isolation is reviewable at a glance
Every shared-file edit must be written so the pre-existing code path is textually unchanged:

```csharp
if (FtueState.Active)
{
    // new tutorial-only behaviour
    return;
}
// ...existing body, untouched...
```

Not interleaved conditionals, not a refactor "while we're in here". A reviewer should be able to
delete the guard block and get the current file back.

### Verification protocol (add to every PR in this effort)
1. Play one full round on `Board_NA`, `Board_NA 1`, `Board_Alpha`, `Board_Spinners`: launch, score,
   level up, enter the shop, buy something, drain out, confirm the round-failed panel appears.
2. Confirm the shop shelf on those boards still offers a normal, varied spread.
3. `git status` shows no unintended changes under `Assets/Prefabs/` or `Assets/Resources/`.
4. Star map and ship select show no FTUE entries. **Record the star map's assignment count before
   Phase 0 and re-check it after** — `StarMapMissionCatalog.BuildAssignments` pairs every board with
   each of its `missions[]`, so a mis-filed FTUE board *or* an FTUE mission wrongly added to a
   board's mission array both show up as a changed count. This is the cheap mechanical check that
   catches A3 regardless of which rule was broken.
5. Load an existing save: confirm it goes to the main menu, not the tutorial.
6. Compile gate: regenerate the csproj source list, confirm **0 errors / 31 warnings**.

---

## 8. Decisions — RESOLVED

_Answered by jjmil, 2026-08-07. Questions 5 and 6 from the original list are closed; the remaining
open items are at the bottom._

1. **Mult-target unlock → the scoped bypass (§2.3a option b).** `ProgressionConfig` is untouched;
   `ShopOfferGenerator.BuildUnlockedPool` gets an FTUE-only bypass. No live balance change.
2. **"The mult target" is `DefaultTarget.asset`.** Confirmed.
3. **Launch stays on Space; dialogue reads the binding display string at runtime.** No rebind. This
   also covers the shop key, so no copy in this tutorial hardcodes an input name.
4. **The player names the narrator.** See §8a — this is new scope, not just a copy decision.
5. **Shop visit 2 offers both Red Two and Blue Two; the player picks one.** Red-pill/blue-pill gag.
   See §8g — the "pick one" framing needs a small hook to actually hold.
6. **Drop-target reveal timing** — still open, cheap to try both once the director is data-driven.
7. **Camera mechanism** — resolved in §8c: director owns its own tween, and only the launcher point
   is authored.

---

## 8a. The player names the narrator

The first dialogue beat asks the player to name the AI that will assist them for the rest of the
game and eventually inhabit the main menu. This is a nice hook, and it is also the largest single
piece of new scope added since the first draft — it is not just a string.

### What it requires

- **A second dialogue prefab variant** with a `TMP_InputField`, and a `FtueStepDefinition` step type
  of `TextEntry`. `FtueDialogueView.Bind` grows an overload that returns the entered string.
- **`ProfileSaveData.aiName`** (string, default `""`). It ships in the **same version-7 bump** as
  `hasCompletedFtue` (§7a A4) — one migration, not two. No migration logic needed for a new string
  field; absent in old JSON means it deserializes to the initializer.
- **Persistence outside the FTUE.** The name lives in the profile, **not** in `FtueState`, precisely
  because the main menu will read it later. `FtueState` is cleared on completion; the name must not be.
- **A fallback: `Al`.** Empty, whitespace-only, or a skipped step yields the name **`Al`** — "A-L",
  which is visually indistinguishable from "AI" in most sans-serif faces. That is the joke.

  **Because it is a joke that looks exactly like a typo, it needs protecting.** Add a comment on the
  localization key and a line in the QA notes saying `Al` is intentional and must not be "corrected"
  to `AI`. Whoever picks the dialogue font should also eyeball it in `Manticore` (the project's
  preferred face, per `BasicTutorialController.preferredFontNameHint`) — if the glyphs are *too*
  identical the gag reads as a rendering bug rather than a pun, and if they are too distinct it does
  not land at all. This is worth two minutes with the actual font before Phase 2 writes the copy.
- **Input hygiene.** Trim, clamp length (suggest 16 characters — it appears in every dialogue box
  and must not blow out the layout), and reject control characters. This string goes into a save
  file and is rendered constantly.
- **Never send it anywhere.** It must not appear in `PinballAnalytics`, Steam leaderboard entries,
  or any log line. It is user-authored personal content.

### The localization rule that matters

Every subsequent line that mentions the narrator must use a **placeholder token**, never string
concatenation:

```
tutorial.ftue.launchPrompt = "{0} here. Hold {1} to launch."   // {0} = AI name, {1} = binding
```

Concatenating (`aiName + " here."`) breaks the moment the game is translated, because word order
differs by language. Since the binding display string (§8, decision 3) is *also* a runtime
substitution, the dialogue system needs ordered-token formatting from day one. Build it into
`FtueDialogueView.Bind` rather than bolting it on later.

### Tone

**Witty, but not obnoxious — it is there to help.** Concretely, for whoever writes the copy: dry
competence over quips; never mock the player for draining; the humour lands on the *situation* and
on the AI's own personality, never on the player's performance. The ball-return line (beat 4a) is
the load-bearing one — it fires exactly when the player has just failed, and it must read as
reassurance with character, not as a joke at their expense.

### Where it goes in the flow

Beat 1 (welcome) → **beat 1a (naming)** → beat 2 (launcher zoom). Naming before the launcher keeps
the identity established before the AI starts giving instructions. Budget one extra step and one
extra prefab variant in Phase 2.

---

## 8b. Mult-target choreography — three targets, one placeable

Board state as authored: three `DefaultTarget` instances, with **`MultTarget (2)`** as the one the
player places into.

**The activation timing does the constraining for us — verified.**
`ShopComponentPlacementController.DiscoverBoardComponents()` runs from `Initialize()`, which is
called from **`UnifiedShopController.OnEnable()`** — i.e. when the shop canvas activates, not at
board load. And it uses `FindObjectsByType` *without* `FindObjectsInactive.Include`.

So the sequence is:

| When | `MultTarget` | `MultTarget (1)` | `MultTarget (2)` |
|---|---|---|---|
| Board load → first level-up | inactive | inactive | **inactive** |
| Just before shop visit 1 opens | inactive | inactive | **active** |
| After placement confirmed | **active** | **active** | (replaced by the purchase) |

Because discovery runs on shop open and only sees active objects, `MultTarget (2)` is the **only**
placeable target in the scene at that moment. The player physically cannot drop it anywhere else.

**This removes a shared-code edit.** `FtuePlacementSlot` and the
`ShopComponentPlacementController.IsValidPlacementTarget` guard (old §7a A6) are **no longer needed**
for the happy path — activation timing is strictly better, because it touches no shared code at all.

**One Phase-3 check before we delete that fallback:** `IsValidPlacementTarget` matches on
`componentType`, not on the specific instance. So the rule we actually depend on is *"at the moment
shop visit 1 opens, `MultTarget (2)` is the only **active** component of that type on the board."*
`Board_FTUE` is a copy of `Board_NA` and may carry other target-type components. Verify that in
Phase 3; if any must stay active, fall back to the `FtuePlacementSlot` marker as originally specced
(the design is still in git history).

**Timing detail:** activate `MultTarget (2)` on the `ShopAvailabilityChanged(true)` beat, *before*
`OpenShop()` runs — not inside `ShopOpened`, which may fire after `OnEnable` has already discovered.

---

## 8c. Camera — author one point, capture the other

Per §3.3a the rig is in `GameplayCore` and there is no waypoint lerper on it. Resolution:

- **`FtueDirector` owns a small unscaled-time tween** over the rig transform. No change to the
  shared `GameplayCore` scene, consistent with §7a.
- **Author only the launcher-zoom point** as an empty in `Board_FTUE`.
- **Do not author a "play pose" point.** Capture the rig's pose at run start (after
  `CameraIntroPan` has landed) and tween back to *that*. An authored return point would silently
  drift from wherever the intro pan actually finishes, and would need re-authoring every time the
  intro is retuned.
- **Yield to `ShopTransitionController`.** Both drive the same transform. The director must finish
  or cancel any camera move before the shop-available beat is allowed to fire.

---

## 8d. QA reset — the existing `R` key works

Confirmed by reading the path: `MainMenuController.Update` → `WasResetPressed()` (`kb.rKey`) →
`ResetActiveProfile()` → `ProfileService.ResetSlot(slot)` → `CreateNewProfile()`, which stamps
`version = currentVersion` and leaves every bool at its default. So `hasCompletedFtue` comes back
`false` and the FTUE re-arms. **No new tooling needed.**

Two caveats for QA:

- `ResetSlot` wipes only the **active slot**. Testing across profile slots means switching slots
  first.
- `R` resets the data but **does not reload the scene**, and you are already sitting in the menu. The
  boot check must therefore run on menu entry / scene load, not once at application start — otherwise
  you must restart play mode to see the FTUE. **Recommended Phase 5 nicety:** have `R` wipe *and*
  immediately route into the FTUE. It costs one line and QA will use it constantly.

---

## 8g. Shop visit 2 — Red Two vs Blue Two

Both are offered; the player takes one. Both are already starter-unlocked (verified in §2.3a), so
the per-visit override just lists the two of them and no unlock work is needed.

### "Pick one" is not free — the shop lets you buy both

Nothing stops a player with enough coins from buying both, which flattens the gag and the framing.
Enforcement options:

- **Starve them of coins.** Fragile — prices move with the module multiplier, and the player may
  have banked level-up income. Rejected.
- **Consume the sibling offer on purchase.** `ShopOfferShelfController.ConsumeOffer(int)` already
  does exactly the right thing: it nulls the slot and destroys the shelf display, and indices stay
  stable because it nulls rather than removes. This is the mechanism.

The gap: `UnifiedShopController` exposes `OfferSelected`, `PlacementCancelled` and `ShopClosed`, but
**no purchase event** — purchases only fire `PinballAnalytics.LogShopItemPurchased` at three sites
(lines 202, 665, 806). So the director has nothing to subscribe to.

**Add `public event Action<ShopOffer> OfferPurchased` to `UnifiedShopController`, invoked at those
same three sites.** This is additive and inert with no subscribers, so it satisfies §7a A8 — other
boards are unaffected because nothing else listens. It is one more shared edit than the plan had a
moment ago, but §8b just removed one, so the total is unchanged.

### If the player buys neither

Do **not** hard-block Continue. A player who somehow cannot afford either would be stuck with the
narrator demanding an impossible purchase — the same dead-end class as the affordability risk in §6.
Let them leave; have the AI note it dryly and move on. Affordability is already a Phase-0
requirement via `FTUE_Ship.startingCoins`, so this should be a path nobody sees.

### Copy notes for whoever writes it

- **Paraphrase, don't quote.** The red/blue choice is a cultural idiom and free to riff on; lifting
  the film's actual dialogue verbatim is copying protected text. The joke does not need the quote,
  and it does not need to name the film either — obliqueness is funnier here.
- **Avoid the bare phrase "red pill."** The term has picked up political baggage well outside the
  film, and this is shipped player-facing copy. The gag works entirely on the *two-coloured choice*
  framing without using the loaded word — and the sideways version lands better anyway.
- **Let Al know it's a hacky joke.** Per the tone rule in §8a, a well-worn reference delivered
  straight is the obnoxious version; the AI being faintly embarrassed about reaching for it is the
  witty one. That also future-proofs the bit as the reference ages.
- **Flag it for localization.** The pun will not survive translation. Add a translator comment on
  the key saying the line is a film reference about a two-colour choice and that a local equivalent
  is preferred over a literal rendering.
- **Neither pick may be a trap.** This is a tutorial; whichever they take, the AI should affirm it.
  No "wrong" answer, no buyer's remorse.

---

## 8e. Still open

_All blocking questions are answered. Everything below is authored content or deferred polish;
see the loose-ends list at the top of this document._

- **Drop-target reveal timing** — at the level-up (visible board change) or at the shop. Needed by
  Phase 4.
- **Narrator portrait art and an FMOD dialogue blip** — explicitly deferred by jjmil; Phase 5.

That is the last of the blocking questions. Everything else is authored content and tuning.

---

## 13. TICKET 13 — boot rule, localization, cleanup (the last ticket)

_Everything needed to execute this is here. Branch: `ftue/phase5-boot-and-localization`._

### 13.1 The boot rule — the point of the whole ticket

Today the FTUE is reachable only via the editor menu item. A real first-time player never sees it.

**Rule:** on entering the main menu, if `!ProfileService.HasCompletedFtue()`, configure the FTUE
session and load `GameplayCore` instead of showing the menu.

- The data assets are `Assets/Data/FTUE/Board_FTUE.asset`, `Mission_FTUE.asset`, `Ship_FTUE.asset`.
  They are **outside `Resources` on purpose** (§7a A3) — reach them by serialized reference from
  whatever component owns the boot check, not by `LoadAll`.
- Configure exactly as `FtueDebugBoot` does:
  `GameSession.Instance.ConfigureChallenge(mission, board, ship, seed)` with a fixed seed. **Do not
  call `GenerateRounds`** — nothing in the shipped flow does, and it would mark every fifth round a
  Devil round.
- Then `SceneFader.Instance.FadeAndLoadScene("GameplayCore")`.
- **`MainMenu 1` is the live ship scene**; `MainMenu` is the legacy one (`MainMenuController.cs`
  comments say so). Put the check in the `MainMenu 1` flow.
- **Run the check on menu entry, not once at application start** (§8d). `R` wipes the profile while
  the player is already sitting in the menu, and QA needs the tutorial to re-arm without restarting
  play mode.

### 13.2 QA shortcut

Extend the existing `R` handler (`MainMenuController.ResetActiveProfile`) to wipe **and** route
straight into the FTUE. One extra line, and it is the loop QA will run dozens of times.

### 13.3 Localization

Every narrator line is currently an inspector fallback string with **no localization key**. They
render fine and will never translate. For each `FtueDialogueLine` on the director and on each
`FtueShopVisit`, add a key to the **Gameplay** string table and fill `Localization Key`.

Suggested key shape: `ftue.<beat>.<n>` — e.g. `ftue.intro`, `ftue.naming`, `ftue.nameAccepted`,
`ftue.launchPrompt`, `ftue.flipperLesson`, `ftue.ballReturned`, `ftue.shopAvailable`,
`ftue.shopVisit1.1`, `ftue.componentPlaced.1`, `ftue.powerSurge.1`, `ftue.completion.1`.

Two translator notes that must travel with the entries:

- **`Al` is intentional** and must not be "corrected" to `AI` (§8a). Put this on the naming beat's
  key and on the fallback-name entry.
- **The Red Two / Blue Two line is a film reference** about a two-colour choice; a local equivalent
  beats a literal rendering (§8g).

Placeholders: `{0}` is always the AI's name; beat-specific arguments start at `{1}`.

### 13.4 Cleanup

- **Delete `Assets/Scripts/FTUE/FtueDebugBoot.cs`** — its whole purpose was to stand in for 13.1.
  Keeping both risks two boot paths disagreeing. (Or keep it deliberately as a dev shortcut and say
  so in its header; do not leave the question open.)
- Confirm no FTUE asset has drifted into `Resources`, and that `Ship_FTUE` is still absent from
  `ProgressionConfig.starterShips` and `Mission_FTUE` from every `BoardDefinition.missions[]`.

### 13.5 Definition of done for this ticket

Beyond the standard compile gate:

1. **Wipe a profile → launch → the FTUE starts with no menu.** Finish it → back at the ship.
2. **Relaunch → the main menu appears normally.** The FTUE does not repeat.
3. **An existing save file boots to the menu, not the tutorial.** This is the §7a A4 grandfather;
   it is the single highest-risk behaviour in the whole feature.
4. `R` wipes and drops straight into the FTUE.
5. Full §7a verification protocol — one round on each of `Board_NA`, `Board_NA 1`, `Board_Alpha`,
   `Board_Spinners`; star map and ship select show no FTUE entries; `git status` clean under
   `Assets/Prefabs/` and `Assets/Resources/`.

---

## 8f. Original question list (superseded by §8 — kept for traceability)

1. **Launch key:** rebind Launch to Enter, or keep Space and fix the copy?
   (Recommendation: keep Space, read the binding string at runtime.)
2. **Which balls in shop visit 2** — Blue Two, Red Two, or both offered as a choice?
3. **Drop-target reveal timing** — at the level-up (visible board change) or at the shop
   (§4, step 10)?
4. **Narrator identity** — does the character have a name yet? It appears in every dialogue box and
   will be a localization key, so it is cheaper to decide now than to rename 20 assets later.
5. **Mult-target unlock (§2.3a — blocks Phase 3).** `DefaultTarget` is not starter-unlocked, so the
   first shop is empty as things stand. Add it to `ProgressionConfig.starterComponents` (data-only,
   but changes live balance for every run), or give the FTUE override an unlock bypass in
   `ShopOfferGenerator` (larger diff, cannot leak)? **Recommendation: the bypass.**
6. **Which board component is "the mult target"?** This plan assumes
   `Resources/BoardComponentDefinitions/DefaultTarget.asset`. Confirm before Phase 3 — if it is a
   different asset, re-run the starter-unlock check in §2.3a against that one.
7. **Camera mechanism for the launcher zoom (§3.3a).** Add `CameraLerpBetweenPoints` to the shared
   `GameplayCore` rig, or give `FtueDirector` its own tween? **Recommendation: its own tween**, to
   keep the mechanism inside FTUE-only code.
