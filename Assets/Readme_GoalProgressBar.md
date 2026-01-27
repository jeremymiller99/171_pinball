# Round Goal Progress Bar (3D Meter)

This project includes a 3D “goal progress” meter that fills as the player gets closer to the **round goal**.

## What it shows

- **Live progress**: \(ScoreManager.roundTotal + (ScoreManager.points * ScoreManager.mult)\)
- **Goal**: `ScoreManager.Goal` (set via `ScoreManager.SetGoal(...)` by `GameRulesManager`)

## How to wire it in the scene

1. **Create/duplicate a 3D bar fill object**
   - Use a Cube (or any mesh) as the “fill” piece.
   - This fill mesh should be the thing that scales along an axis.

2. **Add the script**
   - Add `RoundGoalProgressHUD` to a HUD/controller GameObject (or directly onto the bar root).

3. **Assign references**
   - **`Meter Fill`**: assign the Transform of the fill mesh you want to scale.
   - **`Score Manager`**: optional; if left blank it will auto-find the first `ScoreManager` in the scene.

4. **Match your bar orientation**
   - **`Meter Axis`**: pick X/Y/Z depending on which local axis your bar extends along.
   - **`Meter Positive Direction`**: toggle if your bar should grow “backwards”.
   - **`Meter Max Units`**: how long the bar should be at 100% fill (in local units).
   - **`Meter Smoothing`**: 0 for instant, higher for smoother.

## Notes

- This does **not** change the existing tally animation or TMP score UI behavior; it only adds a live progress meter.\n

## Tiered / “Stacking” meter (when you pass the goal)

`RoundGoalProgressHUD` now supports a tiered mode where the bar **fills again** each time the player crosses another multiple of the round goal:

- **Tier**: \(tier = \lfloor LiveRoundTotal / Goal \rfloor\)
- **Fill within tier**: \(fill01 = (LiveRoundTotal / Goal) - tier\)

### How to get the stacked look

1. **Create a background bar mesh**
   - Duplicate your fill mesh (Cube) and place it directly behind/under the fill.
   - Name it something like **`Round Total Progress Background`** (or include “Background” / “BG” in the name).
   - Keep it at **full length** (the bar’s “100%” size).

2. **Assign it (optional)**
   - In `RoundGoalProgressHUD`, assign **`Meter Background`** to that background mesh.
   - If you don’t assign it, the script will try to auto-find a sibling background near the fill by name.

3. **Customize colors per tier**
   - **Auto mode (default)**: keeps your current tier-0 colors, then hue-shifts them each tier.
   - **Explicit palettes**: enable **`Use Palettes By Tier`** and fill in **`Palettes By Tier`**.

