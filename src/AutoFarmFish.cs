using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Logic;
using Logic.Definition;
using Logic.Farm;
using Logic.Farm.Contents;
using UnityEngine;

[assembly: AssemblyCompany("JBluesword")]
[assembly: AssemblyProduct("AutoFarmFish")]
[assembly: AssemblyTitle("AutoFarmFish")]
[assembly: AssemblyDescription("Mass harvest ready ponds with matching fish using the vanilla work pipeline.")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace AutoFarmFish
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AutoFarmFish : BasePlugin
    {
        private const string PluginGuid = "autofarmfish";
        private const string PluginName = "AutoFarmFish";
        private const string PluginVersion = "0.1.0";

        private Harmony _harmony;
        private GameObject _controlsObject;
        private static ManualLogSource _log;

        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<string> _matchMode;
        private static ConfigEntry<int> _maxPonds;
        private static ConfigEntry<int> _maxVehiclePonds;
        private static ConfigEntry<bool> _searchWholeFarm;
        private static ConfigEntry<int> _searchRadius;
        private static ConfigEntry<bool> _allowManualTractor;
        private static ConfigEntry<bool> _allowAutoTractor;
        private static ConfigEntry<bool> _preflightOnFoot;
        private static ConfigEntry<bool> _disableOnVisitorFarm;
        private static ConfigEntry<bool> _preventFuelUse;
        private static ConfigEntry<bool> _debug;
        private static ConfigEntry<bool> _logEveryPond;
        private static ConfigEntry<bool> _logWorkList;
        private static ConfigEntry<bool> _traceVanilla;
        private static ConfigEntry<bool> _enableToggle;
        private static ConfigEntry<KeyCode> _toggleKey;
        private static ConfigEntry<KeyCode> _statusKey;
        private static ConfigEntry<bool> _controllerToggle;
        private static ConfigEntry<float> _controllerHoldSeconds;

        private static bool _runtimeEnabled;
        private static float _controllerStart = -1f;
        private static bool _controllerLatched;
        private static string _message;
        private static float _messageUntil;
        private static int _workSequence;
        private static int _fuelSuppressionDepth;
        private static IntPtr _fuelPlayer;

        public override void Load()
        {
            _log = Log;
            BindConfig();
            _runtimeEnabled = _enabled.Value;
            _log.LogInfo($"{PluginName} {PluginVersion} by JBluesword loaded. Fish matching={_matchMode.Value}; runtimeEnabled={_runtimeEnabled}; detailedDiagnostics={_debug.Value}.");

            try
            {
                _harmony = new Harmony(PluginGuid);
                PatchGameMethods();
                ClassInjector.RegisterTypeInIl2Cpp<FishControls>();
                _controlsObject = new GameObject("AutoFarmFish Controls");
                UnityEngine.Object.DontDestroyOnLoad(_controlsObject);
                _controlsObject.hideFlags = HideFlags.HideAndDontSave;
                _controlsObject.AddComponent<FishControls>();
                _log.LogInfo($"Controls ready: F7 toggles all installed AutoFarm mods using their default key; Shift+F7 reports fish status; controller LB+RB hold enabled={_controllerToggle.Value}.");
            }
            catch (Exception ex)
            {
                _log.LogError($"AutoFarmFish setup failed: {ex}");
            }
        }

        private void BindConfig()
        {
            _enabled = Config.Bind("Settings", "Enabled", true, "Start each game session with fish mass harvesting enabled.");
            _matchMode = Config.Bind("Settings", "MatchMode", "FishCategory", "FishCategory matches the selected fish type, such as Salmon. ExactDefinition matches only the selected definition. Fish race is never used because it groups unrelated species.");
            _maxPonds = Config.Bind("Settings", "MaxPondsPerHarvest", 0, new ConfigDescription("Maximum separate ponds in one on-foot harvest, including the selected pond. 0 means unlimited.", new AcceptableValueRange<int>(0, 100000)));
            _maxVehiclePonds = Config.Bind("Settings", "MaxPondsPerHarvestUsingVehicle", 0, new ConfigDescription("Maximum separate ponds in one vehicle harvest, including the selected pond. 0 means unlimited.", new AcceptableValueRange<int>(0, 100000)));
            _searchWholeFarm = Config.Bind("Settings", "SearchWholeFarm", true, "Search all ponds on the farm. Set false to use SearchRadiusTiles around the selected pond.");
            _searchRadius = Config.Bind("Settings", "SearchRadiusTiles", 256, new ConfigDescription("Maximum tile distance from the selected pond when SearchWholeFarm is false.", new AcceptableValueRange<int>(1, 4096)));
            _allowManualTractor = Config.Bind("Settings", "AllowManualTractor", true, "Expand fish harvests while manually driving a tractor.");
            _allowAutoTractor = Config.Bind("Settings", "AllowAutoTractor", true, "Expand fish harvests while using auto-tractor.");
            _preflightOnFoot = Config.Bind("Settings", "ValidateWithVanillaWorkOnFoot", true, "Call FarmPond.Work with performWork=false on each additional on-foot pond. Vehicle work skips this extra probe.");
            _disableOnVisitorFarm = Config.Bind("Settings", "DisableWhenVisitingAnotherFarm", false, "Temporarily leave fish harvests unchanged on another player's farm.");
            _preventFuelUse = Config.Bind("Settings", "PreventFuelUseDuringMassWork", false, "Suppress fuel use only while processing a mass-expanded fish harvest.");

            _debug = Config.Bind("Diagnostics", "EnableDebugLogging", true, "Log selection, pond filtering, work flags, list sizes, and exceptions. Enabled for the first test build.");
            _logEveryPond = Config.Bind("Diagnostics", "LogEveryPond", false, "Log each matching or rejected pond individually. Very noisy on large farms.");
            _logWorkList = Config.Bind("Diagnostics", "LogExpandedWorkListSnapshot", false, "Log every final work-list tile before vanilla Player.PerformWork continues.");
            _traceVanilla = Config.Bind("Diagnostics", "TraceVanillaFishPipeline", true, "Trace FarmPond.Work and Pond.Harvest calls and exceptions. Enabled for the first test build.");

            _enableToggle = Config.Bind("Runtime Toggle", "EnableToggleHotkey", true, "Allow a session-only keyboard toggle.");
            _toggleKey = Config.Bind("Runtime Toggle", "ToggleKey", KeyCode.F7, "F7 toggles AutoFarmFish for the current game session. Change this to control fish separately from the other AutoFarm mods.");
            _statusKey = Config.Bind("Runtime Toggle", "StatusKey", KeyCode.F7, "Shift plus this key displays current fish status without toggling.");
            _controllerToggle = Config.Bind("Runtime Toggle", "EnableControllerToggle", true, "Hold controller left bumper and right bumper to toggle this mod.");
            _controllerHoldSeconds = Config.Bind("Runtime Toggle", "ControllerHoldSeconds", 1f, new ConfigDescription("Seconds to hold both controller bumpers.", new AcceptableValueRange<float>(0.25f, 3f)));
        }

        private void PatchGameMethods()
        {
            MethodInfo performWork = AccessTools.Method(typeof(Player), "PerformWork", new[]
            {
                typeof(WorkType), typeof(FarmItemDefinition), typeof(Il2CppSystem.Collections.Generic.List<FarmTile>), typeof(int), typeof(WorkFlags)
            });
            if (performWork == null)
                throw new MissingMethodException("Player.PerformWork(WorkType, FarmItemDefinition, List<FarmTile>, int, WorkFlags)");

            _harmony.Patch(performWork,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(PerformWorkPrefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(PerformWorkPostfix))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(PerformWorkFinalizer))));
            _log.LogInfo($"Patched {performWork.DeclaringType?.FullName}.{performWork.Name} for fish-only work-list expansion.");

            MethodInfo payFuel = AccessTools.Method(typeof(Player), "PayFuel", new[] { typeof(uint) });
            if (payFuel != null)
            {
                _harmony.Patch(payFuel, prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(PayFuelPrefix))));
                _log.LogInfo("Patched Player.PayFuel(uint) for optional mass-work fuel setting.");
            }
            else
                _log.LogWarning("Player.PayFuel(uint) unavailable; optional fuel suppression cannot work.");

            if (!_traceVanilla.Value) return;
            MethodInfo pondWork = AccessTools.Method(typeof(FarmPond), "Work", new[]
            {
                typeof(Player), typeof(bool), typeof(WorkType), typeof(WorkFlags), typeof(FailedAction).MakeByRefType(), typeof(int), typeof(FarmItemDefinition)
            });
            MethodInfo pondHarvest = AccessTools.Method(typeof(Pond), "Harvest", new[]
            {
                typeof(Player), typeof(FarmPond), typeof(FailedAction).MakeByRefType()
            });
            if (pondWork != null)
            {
                _harmony.Patch(pondWork,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(FarmPondWorkPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(FarmPondWorkPostfix))),
                    finalizer: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(FarmPondWorkFinalizer))));
                _log.LogInfo("Patched FarmPond.Work for fish pipeline diagnostics.");
            }
            else _log.LogWarning("FarmPond.Work diagnostic target unavailable.");
            if (pondHarvest != null)
            {
                _harmony.Patch(pondHarvest,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(PondHarvestPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(PondHarvestPostfix))),
                    finalizer: new HarmonyMethod(AccessTools.Method(typeof(AutoFarmFish), nameof(PondHarvestFinalizer))));
                _log.LogInfo("Patched Pond.Harvest for vanilla result diagnostics.");
            }
            else _log.LogWarning("Pond.Harvest diagnostic target unavailable.");
        }

        private static void PerformWorkPrefix(Player __instance, WorkType work, FarmItemDefinition itemDefinition,
            ref Il2CppSystem.Collections.Generic.List<FarmTile> farmTiles, int rotation, WorkFlags workFlags)
        {
            if (!_runtimeEnabled || work != WorkType.Harvest || __instance == null || farmTiles == null || farmTiles.Count == 0)
                return;

            try
            {
                FarmTile selectedTile = farmTiles[0];
                FarmPond selectedFish = GetFish(selectedTile);
                if (selectedFish == null) return;

                int sequence = ++_workSequence;
                bool vehicle = (workFlags & WorkFlags.UsingVehicle) == WorkFlags.UsingVehicle;
                bool autoTractor = (workFlags & WorkFlags.IsAutoTractor) == WorkFlags.IsAutoTractor;
                Info($"HARVEST BEGIN #{sequence}: player={Ptr(__instance)} selectedTile={TileLabel(selectedTile)} selectedFish={FishLabel(selectedFish)} definitionArgument={DefinitionLabel(itemDefinition)} flags={workFlags} rotation={rotation} originalCount={farmTiles.Count} vehicle={vehicle} autoTractor={autoTractor}.");

                if (autoTractor && !_allowAutoTractor.Value) { Info($"HARVEST SKIP #{sequence}: auto-tractor disabled in config."); return; }
                if (vehicle && !autoTractor && !_allowManualTractor.Value) { Info($"HARVEST SKIP #{sequence}: manual tractor disabled in config."); return; }
                if (_disableOnVisitorFarm.Value && !__instance.IsFarmOwner) { Info($"HARVEST SKIP #{sequence}: visiting another farm."); return; }

                Pond selectedPond = selectedFish.Pond;
                PondDefinition selectedDefinition = selectedFish.Definition;
                FarmData farm = selectedTile?.FarmData;
                if (selectedPond == null || selectedDefinition == null || farm == null || farm.Ponds == null)
                {
                    Warn($"HARVEST SKIP #{sequence}: null reference pond={selectedPond == null} definition={selectedDefinition == null} farm={farm == null} farmPonds={farm?.Ponds == null}.");
                    return;
                }

                string mode = GetMatchMode();
                int configuredMax = vehicle || autoTractor ? _maxVehiclePonds.Value : _maxPonds.Value;
                int max = configuredMax == 0 ? int.MaxValue : configuredMax;
                var existingPonds = new HashSet<uint>();
                var tileIds = new HashSet<long>();
                for (int i = 0; i < farmTiles.Count; i++)
                {
                    FarmTile original = farmTiles[i];
                    FarmPond contents = GetFish(original);
                    if (original == null || contents == null || contents.Pond == null)
                    {
                        Warn($"HARVEST SKIP #{sequence}: original work list contains a null/non-fish tile at index={i}; leaving vanilla list untouched.");
                        return;
                    }
                    existingPonds.Add(contents.Pond.Id);
                    tileIds.Add(TileKey(original));
                }

                FailedAction selectedFailure;
                if (!selectedPond.CanBeHarvested(__instance, out selectedFailure))
                {
                    Info($"HARVEST SKIP #{sequence}: selected pond is not harvestable, reason={selectedFailure}.");
                    return;
                }
                if (!PondMatches(selectedPond, selectedDefinition, mode, out string selectedReason))
                {
                    Warn($"HARVEST SKIP #{sequence}: selected pond contains mixed or missing fish definitions ({selectedReason}). Vanilla selected-pond harvest continues.");
                    return;
                }

                var addedTiles = new List<FarmTile>();
                int checkedPonds = 0, differentFish = 0, mixedPonds = 0, notReady = 0, invalid = 0, preflightFailed = 0, duplicates = 0, candidateErrors = 0, outsideRadius = 0;
                int pondCount = farm.Ponds.Count;
                for (int i = 0; i < pondCount && existingPonds.Count + addedTiles.Count < max; i++)
                {
                    try
                    {
                        Pond candidate = farm.Ponds[i];
                        if (candidate == null) { invalid++; continue; }
                        checkedPonds++;
                        if (existingPonds.Contains(candidate.Id)) { duplicates++; continue; }
                        if (!_searchWholeFarm.Value && !WithinRadius(selectedTile, candidate.MainTile, _searchRadius.Value))
                        {
                            outsideRadius++;
                            continue;
                        }
                        if (!Same(candidate.Data, farm)) { invalid++; Candidate($"#{sequence} skip pond={candidate.Id}: farm mismatch."); continue; }
                        if (!TryRepresentative(candidate, out FarmTile representative, out FarmPond candidateFish))
                        {
                            invalid++; Candidate($"#{sequence} skip pond={candidate.Id}: no valid representative tile."); continue;
                        }
                        if (!PondMatches(candidate, selectedDefinition, mode, out string reason))
                        {
                            if (reason.StartsWith("mixed", StringComparison.Ordinal)) mixedPonds++; else differentFish++;
                            Candidate($"#{sequence} skip pond={candidate.Id}: {reason}.");
                            continue;
                        }
                        FailedAction failed;
                        if (!candidate.CanBeHarvested(__instance, out failed))
                        {
                            notReady++; Candidate($"#{sequence} skip pond={candidate.Id}: CanBeHarvested=False reason={failed}."); continue;
                        }
                        if (!vehicle && !autoTractor && _preflightOnFoot.Value &&
                            !candidateFish.Work(__instance, false, WorkType.Harvest, workFlags, out failed, rotation, null))
                        {
                            preflightFailed++; Candidate($"#{sequence} skip pond={candidate.Id}: vanilla Work(false) rejected reason={failed}."); continue;
                        }
                        long tileKey = TileKey(representative);
                        if (!tileIds.Add(tileKey)) { duplicates++; continue; }
                        addedTiles.Add(representative);
                        Candidate($"#{sequence} add pond={candidate.Id}, fish={FishLabel(candidateFish)}, tile={TileLabel(representative)}.");
                    }
                    catch (Exception ex)
                    {
                        candidateErrors++;
                        Warn($"HARVEST candidate failure #{sequence} index={i}: {ex}");
                    }
                }

                if (addedTiles.Count > 0)
                {
                    var expanded = new Il2CppSystem.Collections.Generic.List<FarmTile>();
                    for (int i = 0; i < farmTiles.Count; i++) expanded.Add(farmTiles[i]);
                    foreach (FarmTile tile in addedTiles) expanded.Add(tile);
                    int originalCount = farmTiles.Count;
                    farmTiles = expanded;
                    if (_preventFuelUse.Value && (vehicle || autoTractor))
                    {
                        _fuelSuppressionDepth++;
                        _fuelPlayer = Ptr(__instance);
                    }
                    Info($"HARVEST EXPANDED #{sequence}: selectedPond={selectedPond.Id}, fish={DefinitionLabel(selectedDefinition)}, mode={mode}, addedPonds={addedTiles.Count}, originalCount={originalCount}, finalCount={farmTiles.Count}, list={Ptr(farmTiles)}.");
                    if (_logWorkList.Value) LogList(sequence, farmTiles);
                }
                else Info($"HARVEST NO MATCH #{sequence}: selectedPond={selectedPond.Id}, fish={DefinitionLabel(selectedDefinition)}, mode={mode}; vanilla list unchanged.");

                Info($"HARVEST COUNTS #{sequence}: farmPonds={pondCount}, checked={checkedPonds}, added={addedTiles.Count}, differentFish={differentFish}, mixedPonds={mixedPonds}, notReady={notReady}, invalid={invalid}, preflightFailed={preflightFailed}, duplicates={duplicates}, outsideRadius={outsideRadius}, candidateErrors={candidateErrors}, cap={(configuredMax == 0 ? "unlimited" : configuredMax.ToString())}, wholeFarm={_searchWholeFarm.Value}, flags={workFlags}.");
            }
            catch (Exception ex)
            {
                _log.LogError($"Player.PerformWork fish prefix failed; vanilla action remains available: {ex}");
            }
        }

        private static bool TryRepresentative(Pond pond, out FarmTile tile, out FarmPond fish)
        {
            tile = null;
            fish = null;
            var tiles = pond?.Tiles;
            if (tiles == null) return false;
            for (int i = 0; i < tiles.Count; i++)
            {
                FarmPond contents = tiles[i];
                FarmTile candidate = contents?.Tile;
                if (candidate != null && Same(contents.Pond, pond) && Same(GetFish(candidate), contents))
                {
                    tile = candidate;
                    fish = contents;
                    return true;
                }
            }
            return false;
        }

        private static bool PondMatches(Pond pond, PondDefinition selected, string mode, out string reason)
        {
            reason = "missing pond/definition/tiles";
            if (pond == null || selected == null || pond.Tiles == null || pond.Tiles.Count == 0) return false;
            bool anyMatch = false, anyDifferent = false;
            for (int i = 0; i < pond.Tiles.Count; i++)
            {
                PondDefinition definition = pond.Tiles[i]?.Definition;
                if (definition == null) { reason = "mixed pond with missing definition"; return false; }
                bool match = mode == "ExactDefinition"
                    ? Same(definition, selected)
                    : definition.PondCategory == selected.PondCategory;
                if (match) anyMatch = true; else anyDifferent = true;
                if (anyMatch && anyDifferent) { reason = "mixed fish types in one connected pond"; return false; }
            }
            reason = anyDifferent ? "different fish type" : "matched";
            return anyMatch && !anyDifferent;
        }

        private static string GetMatchMode()
        {
            if (string.Equals(_matchMode.Value, "ExactDefinition", StringComparison.OrdinalIgnoreCase)) return "ExactDefinition";
            if (!string.Equals(_matchMode.Value, "FishCategory", StringComparison.OrdinalIgnoreCase))
                Warn($"Unknown MatchMode='{_matchMode.Value}'; using FishCategory.");
            return "FishCategory";
        }

        private static FarmPond GetFish(FarmTile tile)
        {
            try { return tile?.Contents?.TryCast<FarmPond>(); }
            catch { return null; }
        }

        private static long TileKey(FarmTile tile)
        {
            FarmTileId id = tile.TileId;
            return ((long)(ushort)id._TileX << 16) | (ushort)id._TileY;
        }

        private static bool WithinRadius(FarmTile selected, FarmTileId candidate, int radius)
        {
            return Math.Abs((int)selected.TileId._TileX - candidate._TileX) <= radius &&
                   Math.Abs((int)selected.TileId._TileY - candidate._TileY) <= radius;
        }

        private static bool Same(Il2CppObjectBase a, Il2CppObjectBase b) => a != null && b != null && Ptr(a) != IntPtr.Zero && Ptr(a) == Ptr(b);
        private static IntPtr Ptr(Il2CppObjectBase value)
        {
            try { return value == null ? IntPtr.Zero : IL2CPP.Il2CppObjectBaseToPtr(value); }
            catch { return IntPtr.Zero; }
        }

        private static string TileLabel(FarmTile tile)
        {
            try { return tile == null ? "<null>" : $"({tile.TileId._TileX},{tile.TileId._TileY}) ptr={Ptr(tile)}"; }
            catch (Exception ex) { return $"<tile unavailable: {ex.Message}>"; }
        }

        private static string DefinitionLabel(PondDefinition definition)
        {
            try { return definition == null ? "<null>" : $"{definition.Id} name={definition.Name} category={definition.PondCategory} race={definition.Race} ptr={Ptr(definition)}"; }
            catch (Exception ex) { return $"<definition unavailable: {ex.Message}>"; }
        }

        private static string DefinitionLabel(FarmItemDefinition definition)
        {
            try { return definition == null ? "<null>" : $"{definition.Id} ptr={Ptr(definition)}"; }
            catch (Exception ex) { return $"<definition unavailable: {ex.Message}>"; }
        }

        private static string FishLabel(FarmPond fish)
        {
            try { return fish == null ? "<null>" : $"pond={fish.Pond?.Id.ToString() ?? "<null>"} {DefinitionLabel(fish.Definition)} ptr={Ptr(fish)}"; }
            catch (Exception ex) { return $"<fish unavailable: {ex.Message}>"; }
        }

        private static void LogList(int sequence, Il2CppSystem.Collections.Generic.List<FarmTile> tiles)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                try { Info($"HARVEST LIST #{sequence} [{i}]: tile={TileLabel(tiles[i])} fish={FishLabel(GetFish(tiles[i]))}"); }
                catch (Exception ex) { Warn($"HARVEST LIST #{sequence} [{i}] inspection failed: {ex}"); }
            }
        }

        private static void PerformWorkPostfix(WorkType work, Il2CppSystem.Collections.Generic.List<FarmTile> farmTiles, bool __result)
        {
            try
            {
                if (work == WorkType.Harvest && farmTiles != null && farmTiles.Count > 0 && GetFish(farmTiles[0]) != null)
                    Info($"HARVEST VANILLA RESULT: success={__result}, finalListCount={farmTiles.Count}.");
            }
            catch (Exception ex) { Warn($"Fish result diagnostic failed: {ex}"); }
            finally { ClearFuelSuppression(); }
        }

        private static Exception PerformWorkFinalizer(Exception __exception, WorkType work, Il2CppSystem.Collections.Generic.List<FarmTile> farmTiles)
        {
            if (__exception != null)
                _log.LogError($"HARVEST VANILLA EXCEPTION: work={work}, fish={FishLabel(farmTiles != null && farmTiles.Count > 0 ? GetFish(farmTiles[0]) : null)}, exception={__exception}");
            ClearFuelSuppression();
            return __exception;
        }

        private static bool PayFuelPrefix(Player __instance, uint value, ref bool __result)
        {
            if (!_preventFuelUse.Value || _fuelSuppressionDepth <= 0 || Ptr(__instance) != _fuelPlayer) return true;
            Info($"FUEL SUPPRESSED: fish mass harvest player={_fuelPlayer}, requested={value}.");
            __result = true;
            return false;
        }

        private static void ClearFuelSuppression() { _fuelSuppressionDepth = 0; _fuelPlayer = IntPtr.Zero; }

        private static void FarmPondWorkPrefix(FarmPond __instance, bool performWork, WorkType workType, WorkFlags workFlags)
        {
            if (workType == WorkType.Harvest) Info($"TRACE FarmPond.Work ENTER: pond={FishLabel(__instance)}, performWork={performWork}, flags={workFlags}.");
        }

        private static void FarmPondWorkPostfix(FarmPond __instance, bool performWork, WorkType workType, bool __result)
        {
            if (workType == WorkType.Harvest) Info($"TRACE FarmPond.Work EXIT: pond={FishLabel(__instance)}, performWork={performWork}, success={__result}.");
        }

        private static Exception FarmPondWorkFinalizer(Exception __exception, FarmPond __instance)
        {
            if (__exception != null) _log.LogError($"TRACE FarmPond.Work EXCEPTION pond={FishLabel(__instance)}: {__exception}");
            return __exception;
        }

        private static void PondHarvestPrefix(Pond __instance, FarmPond contents)
        {
            Info($"TRACE Pond.Harvest ENTER: pondId={__instance?.Id.ToString() ?? "<null>"}, fish={FishLabel(contents)}, pondTiles={__instance?.Tiles?.Count.ToString() ?? "<null>"}.");
        }

        private static void PondHarvestPostfix(Pond __instance, bool __result)
        {
            Info($"TRACE Pond.Harvest EXIT: pondId={__instance?.Id.ToString() ?? "<null>"}, success={__result}.");
        }

        private static Exception PondHarvestFinalizer(Exception __exception, Pond __instance)
        {
            if (__exception != null) _log.LogError($"TRACE Pond.Harvest EXCEPTION pondId={__instance?.Id.ToString() ?? "<null>"}: {__exception}");
            return __exception;
        }

        private static void Info(string message) { if (_debug.Value) _log.LogInfo(message); }
        private static void Candidate(string message) { if (_logEveryPond.Value) _log.LogInfo(message); }
        private static void Warn(string message) { _log.LogWarning(message); }

        internal static void UpdateControls()
        {
            try
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                if (_enableToggle.Value && !control && !alt)
                {
                    if (shift && Input.GetKeyDown(_statusKey.Value)) ShowStatus();
                    else if (!shift && Input.GetKeyDown(_toggleKey.Value)) Toggle("keyboard");
                }

                if (!_controllerToggle.Value) return;
                bool held = Input.GetKey(KeyCode.JoystickButton4) && Input.GetKey(KeyCode.JoystickButton5);
                if (!held) { _controllerStart = -1f; _controllerLatched = false; return; }
                if (_controllerStart < 0f) _controllerStart = Time.unscaledTime;
                if (!_controllerLatched && Time.unscaledTime - _controllerStart >= _controllerHoldSeconds.Value)
                {
                    _controllerLatched = true;
                    Toggle("controller");
                }
            }
            catch (Exception ex) { _log.LogError($"Fish controls failed: {ex}"); }
        }

        private static void Toggle(string source)
        {
            _runtimeEnabled = !_runtimeEnabled;
            string status = _runtimeEnabled ? "ON" : "OFF";
            ShowMessage($"AutoFarm Fish: {status}");
            _log.LogInfo($"Runtime toggle: {status}, source={source}. Vanilla fish work continues when OFF.");
        }

        private static void ShowStatus()
        {
            string status = _runtimeEnabled ? "ON" : "OFF";
            ShowMessage($"AutoFarm Fish: {status}");
            _log.LogInfo($"Runtime status: {status}, matchMode={GetMatchMode()}.");
        }

        private static void ShowMessage(string message) { _message = message; _messageUntil = Time.unscaledTime + 3f; }

        internal static void DrawMessage()
        {
            if (!string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil)
                GUI.Box(new Rect(20f, 196f, 260f, 35f), _message);
        }
    }

    public sealed class FishControls : MonoBehaviour
    {
        public FishControls(IntPtr pointer) : base(pointer) { }
        public void Update() { AutoFarmFish.UpdateControls(); }
        public void OnGUI() { AutoFarmFish.DrawMessage(); }
    }
}
