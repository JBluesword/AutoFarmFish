# Changelog

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
