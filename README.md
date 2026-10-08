# AutoFarmFish

AutoFarmFish is a Farm Together 2 BepInEx IL2CPP mod by JBluesword. Harvest a ready pond and the mod adds other ready ponds of the same fish type to the normal game work list. The game still performs each pond harvest, rewards, quests, state changes, and networking.

**Status:** v0.1.1 testing build. The first in-game test confirmed matching pond expansion, but exposed errors in a diagnostic hook. This build removes that hook's finalizer and needs another play test.

## Installation

1. Install BepInEx 6 IL2CPP for Farm Together 2.
2. Close the game.
3. Copy `AutoFarmFish.dll` into `Farm Together 2\BepInEx\plugins`.
4. Launch once to create `Farm Together 2\BepInEx\config\autofarmfish.cfg`.

The ZIP contains only the DLL, this README, and the changelog. No other AutoFarm mod is required.

## How it works

- `FishCategory` is the default match mode. It uses the game's `PondDefinition.PondCategory`, so Salmon matches Salmon, for example. The much broader `FishRaces` value is not used.
- `ExactDefinition` matches the same specific fish definition.
- The game already harvests all fish in one connected pond when one tile is worked. AutoFarmFish searches **all ponds on the farm** by default and adds one tile per additional matching pond, not every tile in each pond.
- A connected pond containing different fish types is skipped during expansion. A selected mixed pond may still be harvested by the vanilla game; the mod cannot separate fish within one connected pond without replacing game harvest behavior.
- Only `WorkType.Harvest` actions starting on a `FarmPond` can expand. Crop, flower, tree, animal, feed, and recycle actions are untouched.
- The mod clones the work list only when it finds additional eligible ponds. Rewards and saves are handled by the game.

## Settings

| Section | Setting | Default | Description |
| --- | --- | --- | --- |
| Settings | `Enabled` | `true` | Starting state each game session. |
| Settings | `MatchMode` | `FishCategory` | `FishCategory` or `ExactDefinition`. |
| Settings | `MaxPondsPerHarvest` | `0` | Maximum separate ponds per on-foot action, including the selected pond; 0 means unlimited. |
| Settings | `MaxPondsPerHarvestUsingVehicle` | `0` | Maximum separate ponds per tractor action, including the selected pond; 0 means unlimited. |
| Settings | `SearchWholeFarm` | `true` | Search every pond on the farm. |
| Settings | `SearchRadiusTiles` | `256` | Radius when whole-farm search is disabled. |
| Settings | `AllowManualTractor` | `true` | Allow expansion in a manually driven tractor. |
| Settings | `AllowAutoTractor` | `true` | Allow expansion in auto-tractor mode. |
| Settings | `ValidateWithVanillaWorkOnFoot` | `true` | Additional read-only game eligibility check per on-foot pond. |
| Settings | `DisableWhenVisitingAnotherFarm` | `false` | Pause expansion while visiting another person's farm. |
| Settings | `PreventFuelUseDuringMassWork` | `false` | Optional fuel suppression for expanded fish actions. |
| Diagnostics | `EnableDebugLogging` | `true` | Log selected fish, pond counts, work flags, expansion and result. |
| Diagnostics | `LogEveryPond` | `false` | Log every candidate pond; noisy on large farms. |
| Diagnostics | `LogExpandedWorkListSnapshot` | `false` | Log every tile in an expanded work list. |
| Diagnostics | `TraceVanillaFishPipeline` | `true` | Trace `FarmPond.Work` and `Pond.Harvest` calls. |
| Runtime Toggle | `EnableToggleHotkey` | `true` | Allow session-only keyboard toggle. |
| Runtime Toggle | `ToggleKey` | `F7` | Toggle the mod. Same default as the other AutoFarm mods. |
| Runtime Toggle | `StatusKey` | `F7` | Press with Shift to show fish status. |
| Runtime Toggle | `EnableControllerToggle` | `true` | Hold left and right bumper to toggle. |
| Runtime Toggle | `ControllerHoldSeconds` | `1.0` | Controller hold time. |

Changing the session toggle does not rewrite the config. Fish, Animals, Flowers, and Trees all respond to `F7` by default. Change `ToggleKey` in this mod's config to control fish separately.

## First play test

1. Start with two separate ready ponds of the same fish type and one ready pond of a different type.
2. Harvest one fish on foot. The second matching pond should harvest; the different type should remain ready.
3. Repeat with one pond that is not ready. It should stay untouched.
4. Test manual tractor and auto-tractor separately.
5. Test a connected pond with mixed species if available. The log should explain why it skipped expansion.
6. Check `BepInEx\LogOutput.log` for `AutoFarmFish` lines beginning `HARVEST BEGIN`, `HARVEST COUNTS`, `HARVEST EXPANDED`, `TRACE`, and any warnings or exceptions.

For a problem report, include the log from game launch through one test action and say which fish types, pond sizes, mode, and vehicle state were involved.

## Build

From this project folder, run `powershell -ExecutionPolicy Bypass -File .\build.ps1`. Add `-Install` to copy the DLL into the local game plugins folder after a successful build. The script uses the installed game's current BepInEx interop assemblies and creates `release\AutoFarmFish-0.1.1.zip`.

## Shout outs

Thanks to the Farm Together 2 modding community, BepInEx, Harmony, and everyone helping test the AutoFarm series.
