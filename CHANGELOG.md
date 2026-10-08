# Changelog

## 0.1.2 - Testing build

- Removed the entire `Pond.Harvest` diagnostic Harmony hook. The 0.1.1 test still produced one IL2CPP trampoline null-reference error per pond, including single-pond harvests with no work-list expansion.
- Retained `FarmPond.Work` and `Player.PerformWork` diagnostics. The 0.1.1 log showed six successful vanilla work actions, four of which expanded to matching ponds.
- Requires another in-game test to verify that the red errors are gone.

## 0.1.1 - Testing build

- Removed the `Pond.Harvest` Harmony finalizer. In the first play test, five successful mass harvests produced 15 IL2CPP trampoline null-reference errors immediately after that hook's exit log, one error per harvested pond.
- Kept the pond entry, success, work-list, and candidate diagnostics for the next test.
- Gameplay retest is required to confirm that removing the finalizer eliminates the errors.

## 0.1.0 - Testing build

- First public test build for mass harvesting separate ready ponds containing the selected fish category.
- Added exact-definition matching as an alternate mode.
- Reuses the game's pond harvest and Player.PerformWork flow; adds one representative tile per eligible pond.
- Searches the whole farm with no pond-count limit by default. Added optional on-foot and tractor caps and a radius mode for players who prefer limits.
- Added runtime toggle, visitor-farm option, optional fuel setting, and detailed pond diagnostics.
- The first play test confirmed five successful work-list expansions, but exposed diagnostic hook errors addressed in 0.1.1.
