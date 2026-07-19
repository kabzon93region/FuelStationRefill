using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using UnityEngine;

namespace FuelStationRefill
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public class PluginCore : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static PluginCore Instance;

        // Config entries
        internal ConfigEntry<bool> ModEnabled;
        internal ConfigEntry<bool> DevMode;
        internal ConfigEntry<float> RefuelRate;
        internal ConfigEntry<float> InteractionRange;
        internal ConfigEntry<string> ZonesFilePath;
        internal ConfigEntry<KeyCode> OverlayToggleKey;
        internal ConfigEntry<KeyCode> SavePositionKey;
        internal ConfigEntry<float> DefaultZoneRadius;
        internal ConfigEntry<float> MinFuel;
        internal ConfigEntry<float> MaxFuel;
        internal ConfigEntry<bool> EnableFikaSync;

        // Runtime state
        private List<FuelZone> _fuelZones = new List<FuelZone>();
        private bool _isInZone;
        private FuelZone _currentZone;
        private bool _isRefueling;
        private Item _refuelTargetItem;
        private float _refuelTargetMaxResource;
        private float _refuelDuration;
        private float _refuelElapsed;
        private float _refuelGivenSoFar;
        private int _refuelGivenUnits;
        private int _refuelTargetUnits;

        // Fika sync
        internal FuelStationFikaSync FikaSync;
        private bool _fuelRandomized;

        // Cached reflection for ResourceComponent
        private PropertyInfo _resourceComponentValue;
        private PropertyInfo _resourceComponentMaxResource;

        // Dev mode
        private bool _showDevOverlay;
        private string _lastSaveMessage = "";
        private float _lastSaveMessageTime;

        // Zones file path (cached)
        private string _zonesFilePath;

        // Interaction sound clip (cached)
        private AudioClip _interactionClip;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            // Bind config
            ModEnabled = Config.Bind("General", "ModEnabled", true,
                new ConfigDescription("Включить/выключить мод.", null,
                    new ConfigurationManagerAttributes { Order = 100 }));

            DevMode = Config.Bind("General", "DevMode", false,
                new ConfigDescription("Режим разработчика: оверлей с координатами.", null,
                    new ConfigurationManagerAttributes { Order = 90 }));

            RefuelRate = Config.Bind("General", "RefuelRate", 2f,
                new ConfigDescription("Скорость заправки (литров в секунду).", null,
                    new ConfigurationManagerAttributes { Order = 80 }));

            InteractionRange = Config.Bind("General", "InteractionRange", 5f,
                new ConfigDescription("Дистанция взаимодействия (метры).", null,
                    new ConfigurationManagerAttributes { Order = 70 }));

            DefaultZoneRadius = Config.Bind("General", "DefaultZoneRadius", 1f,
                new ConfigDescription("Радиус новой зоны по умолчанию (метры).", null,
                    new ConfigurationManagerAttributes { Order = 65 }));

            ZonesFilePath = Config.Bind("General", "ZonesFilePath", "FuelStationZones.json",
                new ConfigDescription("Путь к файлу зон (относительно папки мода).", null,
                    new ConfigurationManagerAttributes { Order = 60 }));

            OverlayToggleKey = Config.Bind("Dev", "OverlayToggleKey", KeyCode.F9,
                new ConfigDescription("Клавиша переключения оверлея.", null,
                    new ConfigurationManagerAttributes { Order = 50 }));

            SavePositionKey = Config.Bind("Dev", "SavePositionKey", KeyCode.F10,
                new ConfigDescription("Клавиша сохранения текущей позиции в JSON.", null,
                    new ConfigurationManagerAttributes { Order = 40 }));

            MinFuel = Config.Bind("Fuel", "MinFuel", 0f,
                new ConfigDescription("Минимум топлива на точке при рандомизации.", null,
                    new ConfigurationManagerAttributes { Order = 30 }));

            MaxFuel = Config.Bind("Fuel", "MaxFuel", 40f,
                new ConfigDescription("Максимум топлива на точке при рандомизации.", null,
                    new ConfigurationManagerAttributes { Order = 25 }));

            EnableFikaSync = Config.Bind("Fika", "EnableFikaSync", true,
                new ConfigDescription("Синхронизация топлива через Fika (хост рассылает клиентам).", null,
                    new ConfigurationManagerAttributes { Order = 20 }));

            // Cache zones file path
            // DLL is flat in BepInEx/plugins/, JSON is in BepInEx/plugins/FuelStationRefill/
            string pluginsFolder = Path.GetDirectoryName(typeof(PluginCore).Assembly.Location);
            string subfolder = Path.Combine(pluginsFolder, "FuelStationRefill");
            if (!Directory.Exists(subfolder))
                Directory.CreateDirectory(subfolder);
            _zonesFilePath = Path.Combine(subfolder, ZonesFilePath.Value);

            // Cache reflection for ResourceComponent
            CacheReflection();

            // Load zones
            LoadFuelZones();

            // Initialize Fika sync
            if (EnableFikaSync.Value)
            {
                FikaSync = new FuelStationFikaSync(this);
                FikaSync.Initialize();
            }

            Log.LogInfo($"{PluginInfo.NAME} v{PluginInfo.VERSION} loaded ({_fuelZones.Count} zones, fika={EnableFikaSync.Value})");
        }

        private void CacheReflection()
        {
            try
            {
                var rcType = typeof(ResourceComponent);
                _resourceComponentValue = rcType.GetProperty("Value");
                _resourceComponentMaxResource = rcType.GetProperty("MaxResource");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to cache ResourceComponent reflection: {ex.Message}");
            }
        }

        private float GetResourceValue(ResourceComponent rc)
        {
            if (rc == null || _resourceComponentValue == null) return 0f;
            return (float)_resourceComponentValue.GetValue(rc);
        }

        private float GetMaxResource(ResourceComponent rc)
        {
            if (rc == null || _resourceComponentMaxResource == null) return 0f;
            return (float)_resourceComponentMaxResource.GetValue(rc);
        }

        private void SetResourceValue(ResourceComponent rc, float value)
        {
            if (rc == null || _resourceComponentValue == null) return;
            _resourceComponentValue.SetValue(rc, value);
        }

        private ResourceComponent GetResourceComponent(Item item)
        {
            if (item == null) return null;
            try
            {
                // ResourceHolderComponent is a public readonly FIELD on BarterItemItemClass
                var field = item.GetType().GetField("ResourceHolderComponent",
                    BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    var rc = field.GetValue(item) as ResourceComponent;
                    if (rc != null) return rc;
                }

                // Fallback: search Components list
                var componentsProp = item.GetType().GetProperty("Components",
                    BindingFlags.Public | BindingFlags.Instance);
                if (componentsProp != null)
                {
                    var components = componentsProp.GetValue(item) as System.Collections.IList;
                    if (components != null)
                    {
                        foreach (var comp in components)
                        {
                            if (comp is ResourceComponent rc)
                                return rc;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"GetResourceComponent error: {ex.Message}");
            }
            return null;
        }

        private string GetItemName(Item item)
        {
            if (item == null) return "null";
            try
            {
                var prop = item.GetType().GetProperty("ShortName");
                if (prop != null)
                    return prop.GetValue(item) as string ?? item.Id;
            }
            catch { }
            return item.Id;
        }

        private void LoadFuelZones()
        {
            try
            {
                if (!File.Exists(_zonesFilePath))
                {
                    Log.LogWarning($"Zones file not found: {_zonesFilePath}. Creating default.");
                    CreateDefaultZonesFile(_zonesFilePath);
                }

                string json = File.ReadAllText(_zonesFilePath);
                var zonesData = JsonConvert.DeserializeObject<FuelZonesData>(json);

                if (zonesData?.Zones != null)
                {
                    _fuelZones = zonesData.Zones;
                    Log.LogInfo($"Loaded {_fuelZones.Count} fuel zones from {Path.GetFileName(_zonesFilePath)}");
                }
                else
                {
                    Log.LogWarning("Failed to parse zones file.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error loading fuel zones: {ex.Message}");
            }
        }

        private void CreateDefaultZonesFile(string filePath)
        {
            var defaultData = new FuelZonesData
            {
                Zones = new List<FuelZone>
                {
                    new FuelZone
                    {
                        Name = "АЗС",
                        Position = new Vector3Serializable { X = 0f, Y = 0f, Z = 0f },
                        Radius = 1f,
                        MapName = ""
                    }
                }
            };
            string json = JsonConvert.SerializeObject(defaultData, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private void SaveCurrentPosition(Player player)
        {
            try
            {
                Vector3 pos = player.Transform.position;
                string mapName = GetCurrentMapName();

                var newZone = new FuelZone
                {
                    Name = $"Зона заправки ({mapName}) - {_fuelZones.Count + 1}",
                    Position = new Vector3Serializable(pos),
                    Radius = DefaultZoneRadius.Value,
                    MapName = mapName
                };

                _fuelZones.Add(newZone);

                SaveZonesToFile();

                _lastSaveMessage = $"СОХРАНЕНО: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) -> {mapName} [{_fuelZones.Count} зон]";
                _lastSaveMessageTime = Time.time;

                Log.LogInfo($"Saved fuel zone: {newZone.Name} at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) map={mapName}");
            }
            catch (Exception ex)
            {
                _lastSaveMessage = $"ОШИБКА: {ex.Message}";
                _lastSaveMessageTime = Time.time;
                Log.LogError($"SaveCurrentPosition error: {ex.Message}");
            }
        }

        private void SaveZonesToFile()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"Zones\": [");
            for (int i = 0; i < _fuelZones.Count; i++)
            {
                var z = _fuelZones[i];
                string comma = i < _fuelZones.Count - 1 ? "," : "";
                sb.AppendLine($"    {{ \"Name\": \"{EscapeJson(z.Name)}\", \"Position\": {{ \"X\": {z.Position.X}, \"Y\": {z.Position.Y}, \"Z\": {z.Position.Z} }}, \"Radius\": {z.Radius}, \"MapName\": \"{EscapeJson(z.MapName)}\" }}{comma}");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(_zonesFilePath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        private void Update()
        {
            if (!ModEnabled.Value) return;

            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (world == null) return;

            var player = world.MainPlayer;
            if (player == null || player.HealthController == null || !player.HealthController.IsAlive) return;

            Vector3 playerPos = player.Transform.position;

            // Dev mode hotkeys
            if (DevMode.Value)
            {
                if (Input.GetKeyDown(OverlayToggleKey.Value))
                {
                    _showDevOverlay = !_showDevOverlay;
                }

                if (Input.GetKeyDown(SavePositionKey.Value))
                {
                    SaveCurrentPosition(player);
                }
            }

            // Randomize fuel on first frame after raid start (singleplayer or host fallback)
            if (!_fuelRandomized && _fuelZones.Count > 0)
            {
                RandomizeFuelForAllZones();
                _fuelRandomized = true;

                // Diagnostic: log current map name and zone match count
                string currentMap = GetCurrentMapName();
                int matchedZones = 0;
                foreach (var z in _fuelZones)
                {
                    if (z.IsActiveInCurrentMap()) matchedZones++;
                }
                Log.LogInfo($"Raid started: currentMap='{currentMap}', totalZones={_fuelZones.Count}, matchedOnMap={matchedZones}");
            }

            // Check if player is in any fuel zone
            FuelZone nearestZone = null;
            float nearestDist = float.MaxValue;

            foreach (var zone in _fuelZones)
            {
                if (!zone.IsActiveInCurrentMap() || !zone.IsActive) continue;
                if (zone.CurrentFuel < 1f) continue; // not enough for a whole unit

                Vector3 zonePos = zone.GetPosition();
                float dist = Vector3.Distance(playerPos, zonePos);

                if (dist <= zone.Radius + InteractionRange.Value && dist < nearestDist)
                {
                    nearestZone = zone;
                    nearestDist = dist;
                }
            }

            bool wasInZone = _isInZone;
            _isInZone = nearestZone != null;
            _currentZone = nearestZone;

            // Show/hide native prompt when entering/leaving zone
            if (_isInZone && !wasInZone)
            {
                ShowZonePrompt(player);
            }
            else if (!_isInZone && wasInZone)
            {
                HideZonePrompt(player);
            }

            // Handle interaction key press (F) — start native Plant interaction
            if (_isInZone && !_isRefueling && _currentZone != null
                && _currentZone.IsActive && _currentZone.CurrentFuel >= 1f
                && Input.GetKeyDown(KeyCode.F))
            {
                TryStartNativeRefuel(player);
            }

            // Incremental fuel application during refueling
            if (_isRefueling && _refuelTargetItem != null)
            {
                UpdateRefuelingIncremental(player, Time.deltaTime);
            }

            // Leaving zone while refueling — cancel
            if (!_isInZone && _isRefueling)
            {
                CancelRefueling(player);
                HideZonePrompt(player);
            }
        }

        #region Native Interaction System

        /// <summary>
        /// Get the GamePlayerOwner component from the player's GameObject.
        /// </summary>
        private GamePlayerOwner GetGamePlayerOwner(Player player)
        {
            try
            {
                return player.GetComponent<GamePlayerOwner>();
            }
            catch (Exception ex)
            {
                Log.LogError($"GetGamePlayerOwner error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Show a persistent hint text on screen (quest-style).
        /// Uses ShowObjectivesPanel with a very long timer for zone-entry hints.
        /// </summary>
        private void ShowZonePrompt(Player player)
        {
            try
            {
                var owner = GetGamePlayerOwner(player);
                if (owner == null) return;

                bool hasFuel = HasRefuelableItem(player);
                string text;
                if (hasFuel)
                    text = "Слить бензин в канистры [F]";
                else
                    text = "Зона заправки — нет канистр";

                // Show for 999s; will be hidden when leaving zone or starting refuel
                owner.ShowObjectivesPanel(text, 999f);
            }
            catch (Exception ex)
            {
                Log.LogError($"ShowZonePrompt error: {ex.Message}");
            }
        }

        /// <summary>
        /// Hide the persistent hint text.
        /// </summary>
        private void HideZonePrompt(Player player)
        {
            try
            {
                var owner = GetGamePlayerOwner(player);
                if (owner == null) return;
                owner.CloseObjectivesPanel();
            }
            catch (Exception ex)
            {
                Log.LogError($"HideZonePrompt error: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to start the native Plant interaction for refueling.
        /// </summary>
        private void TryStartNativeRefuel(Player player)
        {
            try
            {
                var item = FindRefuelableItem(player);
                if (item == null)
                {
                    Log.LogInfo("TryStartNativeRefuel: no refuelable item found");
                    return;
                }

                var rc = GetResourceComponent(item);
                if (rc == null) return;

                // Check if player is in idle state (required for Plant)
                var currentState = player.CurrentManagedState;
                string stateName = currentState?.GetType().Name ?? "null";
                if (!(currentState is IdleStateClass))
                {
                    Log.LogInfo($"TryStartNativeRefuel: player not idle (state={stateName}), cannot plant");
                    return;
                }

                float currentVal = GetResourceValue(rc);
                float maxVal = GetMaxResource(rc);
                float missingFuel = maxVal - currentVal;

                // Calculate whole units needed (round up)
                int missingUnits = Mathf.CeilToInt(missingFuel);
                int stationUnits = Mathf.FloorToInt(_currentZone.CurrentFuel);
                int unitsToRefuel = Mathf.Min(missingUnits, stationUnits);

                if (unitsToRefuel <= 0)
                {
                    Log.LogInfo($"TryStartNativeRefuel: nothing to refuel (missing={missingFuel:F1}, station={_currentZone.CurrentFuel:F1})");
                    return;
                }

                float duration = unitsToRefuel / RefuelRate.Value;
                if (duration < 0.5f) duration = 0.5f;

                Log.LogInfo($"TryStartNativeRefuel: item={GetItemName(item)} current={currentVal:F1}/{maxVal:F1} " +
                            $"missing={missingFuel:F1} units={unitsToRefuel} station={_currentZone.CurrentFuel:F1} duration={duration:F1}s");

                // Save state for callback
                _refuelTargetItem = item;
                _refuelTargetMaxResource = maxVal;
                _refuelDuration = duration;
                _refuelElapsed = 0f;
                _refuelGivenSoFar = 0f;
                _refuelGivenUnits = 0;
                _refuelTargetUnits = unitsToRefuel;
                _isRefueling = true;

                // Hide zone hint, show refuel progress via native panel
                HideZonePrompt(player);
                var owner = GetGamePlayerOwner(player);
                if (owner != null)
                {
                    owner.ShowObjectivesPanel("Заправка канистры {0:F1}", duration);
                }

                // Play interaction sound
                PlayRefuelSound(player);

                // Start native Plant state
                // Plant(enabled, multitool, plantTime, callback)
                currentState.Plant(true, false, duration, (success) =>
                {
                    OnRefuelComplete(player, success);
                });

                Log.LogInfo($"Native refuel started: {GetItemName(item)} ({currentVal:F1}/{maxVal:F1}) " +
                            $"station={_currentZone?.CurrentFuel:F0} duration={duration:F1}s");
            }
            catch (Exception ex)
            {
                Log.LogError($"TryStartNativeRefuel error: {ex.Message}");
                _isRefueling = false;
            }
        }

        /// <summary>
        /// Apply fuel incrementally in whole units (1 unit = 1 liter per tick).
        /// Last partial unit fills canister to max, but station still pays full unit.
        /// </summary>
        private void UpdateRefuelingIncremental(Player player, float deltaTime)
        {
            if (_refuelTargetItem == null || _currentZone == null) return;

            var rc = GetResourceComponent(_refuelTargetItem);
            if (rc == null) return;

            _refuelElapsed += deltaTime;

            // How many whole units should have been delivered by now
            int targetUnits = Mathf.FloorToInt(_refuelElapsed * RefuelRate.Value);
            int deltaUnits = targetUnits - _refuelGivenUnits;
            if (deltaUnits <= 0) return;

            // Cap by remaining target units
            int remainingTarget = _refuelTargetUnits - _refuelGivenUnits;
            if (deltaUnits > remainingTarget)
                deltaUnits = remainingTarget;

            // Cap by station remaining (whole units)
            int stationUnits = Mathf.FloorToInt(_currentZone.CurrentFuel);
            if (deltaUnits > stationUnits)
                deltaUnits = stationUnits;
            if (deltaUnits <= 0) return;

            // Apply each unit — station pays only for what canister actually receives
            float currentVal = GetResourceValue(rc);
            float maxVal = _refuelTargetMaxResource;

            for (int i = 0; i < deltaUnits; i++)
            {
                // Station always pays 1 full unit
                _currentZone.CurrentFuel -= 1f;
                _refuelGivenUnits++;

                // Canister gets min(1, remaining capacity) — last unit may be partial
                float canGive = Mathf.Min(1f, maxVal - currentVal);
                if (canGive <= 0f) break;

                currentVal += canGive;
                _refuelGivenSoFar += canGive;

                // Station exhausted (less than 1 full unit remaining)
                if (_currentZone.CurrentFuel < 1f)
                {
                    _currentZone.CurrentFuel = 0f;
                    _currentZone.IsActive = false;
                    break;
                }
            }

            // Write final value
            SetResourceValue(rc, currentVal);

            // Verify
            float verifyVal = GetResourceValue(rc);
            if (Math.Abs(verifyVal - currentVal) > 0.01f)
            {
                Log.LogWarning($"SetResourceValue FAILED: tried {currentVal:F2}, got {verifyVal:F2}. Forcing.");
                rc.Value = currentVal;
            }

            // Canister full — stop early
            if (currentVal >= maxVal - 0.01f)
            {
                Log.LogInfo($"Canister full ({currentVal:F1}/{maxVal:F1}), stopping incremental refuel");
                CancelRefueling(player);
                return;
            }

            // Station exhausted — stop
            if (!_currentZone.IsActive)
            {
                Log.LogInfo($"Station exhausted during refuel at {_refuelElapsed:F1}s");
                CancelRefueling(player);
                return;
            }
        }

        /// <summary>
        /// Called when the Plant timer completes (success=true) or is cancelled (success=false).
        /// </summary>
        private void OnRefuelComplete(Player player, bool success)
        {
            try
            {
                // Stop interaction sound
                StopRefuelSound(player);

                // Close the objectives panel
                var owner = GetGamePlayerOwner(player);
                owner?.CloseObjectivesPanel();

                if (!success)
                {
                    Log.LogInfo($"Refuel cancelled: {GetItemName(_refuelTargetItem)} (applied {_refuelGivenSoFar:F1}L)");
                }
                else
                {
                    Log.LogInfo($"Refuel timer complete: {GetItemName(_refuelTargetItem)} (applied {_refuelGivenSoFar:F1}L)");
                }

                // Verify final canister state
                if (_refuelTargetItem != null)
                {
                    var verifyRc = GetResourceComponent(_refuelTargetItem);
                    if (verifyRc != null)
                    {
                        float finalVal = GetResourceValue(verifyRc);
                        Log.LogInfo($"Final canister state: {GetItemName(_refuelTargetItem)} = {finalVal:F1}/{_refuelTargetMaxResource:F1}");
                    }
                }

                // Send Fika sync for total consumed
                if (_refuelGivenSoFar > 0.01f && FikaSync != null && EnableFikaSync.Value && _currentZone != null)
                {
                    int zoneIdx = _fuelZones.IndexOf(_currentZone);
                    if (zoneIdx >= 0)
                    {
                        FikaSync.SendConsumeRequest(zoneIdx, _refuelGivenSoFar);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"OnRefuelComplete error: {ex.Message}");
            }
            finally
            {
                _isRefueling = false;
                _refuelTargetItem = null;
                _refuelTargetMaxResource = 0f;
                _refuelDuration = 0f;
                _refuelElapsed = 0f;
                _refuelGivenSoFar = 0f;
                _refuelGivenUnits = 0;
                _refuelTargetUnits = 0;

                // Re-show zone prompt if still in zone
                if (_isInZone && player != null)
                {
                    ShowZonePrompt(player);
                }
            }
        }

        /// <summary>
        /// Cancel refueling externally (e.g., player left zone).
        /// </summary>
        private void CancelRefueling(Player player)
        {
            if (!_isRefueling) return;

            try
            {
                // Exit the Plant state — this will trigger OnRefuelComplete(false)
                var currentState = player?.CurrentManagedState;
                if (currentState != null)
                {
                    currentState.Cancel();
                }
                else
                {
                    // Fallback: manually clean up
                    StopRefuelSound(player);
                    _isRefueling = false;
                    _refuelTargetItem = null;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"CancelRefueling error: {ex.Message}");
                _isRefueling = false;
                _refuelTargetItem = null;
            }
        }

        /// <summary>
        /// Load and play a looping interaction sound during refueling.
        /// Uses generator repair sound as a mechanical fuel pump equivalent.
        /// </summary>
        private void PlayRefuelSound(Player player)
        {
            try
            {
                if (_interactionClip == null)
                {
                    // Try to load the generator repair loop sound
                    _interactionClip = LoadAudioClip("Audio/Events/Runddans/generator_repair_loop");
                }

                if (_interactionClip != null)
                {
                    player.PlayInteractionSound(_interactionClip, 0.7f, true, true);
                    Log.LogInfo("Playing refuel interaction sound");
                }
                else
                {
                    Log.LogWarning("Could not load interaction sound clip");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"PlayRefuelSound error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the interaction sound.
        /// </summary>
        private void StopRefuelSound(Player player)
        {
            try
            {
                player?.StopInteractionSound(0.2f);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"StopRefuelSound error: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to load an AudioClip from the game's asset system by path.
        /// </summary>
        private AudioClip LoadAudioClip(string assetPath)
        {
            try
            {
                // Method 1: Try via IEasyAssets (game's asset loading system)
                var easyAssetsType = Type.GetType("Comfort.Common.MonoBehaviourSingleton`1[IEasyAssets]");
                if (easyAssetsType != null)
                {
                    var instanceProp = easyAssetsType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    var easyAssets = instanceProp?.GetValue(null);
                    if (easyAssets != null)
                    {
                        var getAssetMethod = easyAssets.GetType().GetMethod("GetAsset",
                            new Type[] { typeof(string), typeof(string) });
                        if (getAssetMethod != null)
                        {
                            var clip = getAssetMethod.Invoke(easyAssets,
                                new object[] { assetPath, null }) as AudioClip;
                            if (clip != null) return clip;
                        }
                    }
                }

                // Method 2: Try Resources.Load
                var clip2 = Resources.Load<AudioClip>(assetPath);
                if (clip2 != null) return clip2;

                Log.LogWarning($"LoadAudioClip: could not load '{assetPath}'");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"LoadAudioClip error: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region Inventory Helpers

        private bool HasRefuelableItem(Player player)
        {
            return FindRefuelableItem(player) != null;
        }

        private Item FindRefuelableItem(Player player)
        {
            var inventory = player.Inventory;
            if (inventory == null)
            {
                Log.LogWarning("FindRefuelableItem: inventory is null");
                return null;
            }

            try
            {
                var allItems = inventory.AllRealPlayerItems;
                if (allItems == null)
                {
                    Log.LogWarning("FindRefuelableItem: AllRealPlayerItems is null");
                    return null;
                }

                int totalItems = 0;
                int fuelItems = 0;
                int withResource = 0;
                int needsRefuel = 0;

                foreach (var item in allItems)
                {
                    totalItems++;
                    if (item is FuelItemClass)
                    {
                        fuelItems++;
                        var rc = GetResourceComponent(item);
                        if (rc != null)
                        {
                            withResource++;
                            float val = GetResourceValue(rc);
                            float max = GetMaxResource(rc);
                            string itemName = GetItemName(item);
                            Log.LogInfo($"  FuelItem: {itemName} id={item.Id} val={val:F1}/{max:F1} needsRefuel={val < max - 0.01f}");
                            if (val < max - 0.01f)
                            {
                                needsRefuel++;
                                return item;
                            }
                        }
                        else
                        {
                            Log.LogWarning($"  FuelItem: {GetItemName(item)} id={item.Id} - NO ResourceComponent!");
                        }
                    }
                }

                // Log summary on first call after zone entry
                if (_isInZone)
                {
                    Log.LogInfo($"FindRefuelableItem: total={totalItems}, fuel={fuelItems}, withRC={withResource}, needsRefuel={needsRefuel}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error searching inventory: {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Fika Helpers

        /// <summary>
        /// Randomize fuel for all zones (singleplayer or host fallback).
        /// </summary>
        internal void RandomizeFuelForAllZones()
        {
            float min = MinFuel.Value;
            float max = MaxFuel.Value;
            if (min > max) { float t = min; min = max; max = t; }

            foreach (var zone in _fuelZones)
            {
                zone.MaxFuelGenerated = UnityEngine.Random.Range(min, max);
                zone.CurrentFuel = zone.MaxFuelGenerated;
                zone.IsActive = true;
            }
            Log.LogInfo($"Randomized fuel for {_fuelZones.Count} zones (range {min:F0}-{max:F0})");
        }

        /// <summary>
        /// Apply synced fuel state from host (array of floats per zone).
        /// </summary>
        internal void ApplySyncData(float[] currentFuels, float[] maxFuels, bool[] activeStates)
        {
            int count = Mathf.Min(_fuelZones.Count, Mathf.Min(currentFuels.Length, maxFuels.Length));
            for (int i = 0; i < count; i++)
            {
                _fuelZones[i].CurrentFuel = currentFuels[i];
                _fuelZones[i].MaxFuelGenerated = maxFuels[i];
                _fuelZones[i].IsActive = i < activeStates.Length && activeStates[i];
            }
            _fuelRandomized = true;
            Log.LogInfo($"Applied sync data for {count} zones");
        }

        /// <summary>
        /// Whether the current instance is a Fika host.
        /// </summary>
        internal bool IsFikaHost()
        {
            try
            {
                return Comfort.Common.Singleton<Fika.Core.Networking.FikaServer>.Instantiated;
            }
            catch { return false; }
        }

        /// <summary>
        /// Get the Fika network manager if available.
        /// </summary>
        internal Fika.Core.Networking.IFikaNetworkManager GetFikaNetworkManager()
        {
            try
            {
                var server = Comfort.Common.Singleton<Fika.Core.Networking.FikaServer>.Instance;
                if (server != null) return server;
                return Comfort.Common.Singleton<Fika.Core.Networking.FikaClient>.Instance;
            }
            catch { return null; }
        }

        /// <summary>
        /// Get current fuel state arrays for sync packets.
        /// </summary>
        internal void GetFuelStateArrays(out float[] currentFuels, out float[] maxFuels, out bool[] activeStates)
        {
            currentFuels = new float[_fuelZones.Count];
            maxFuels = new float[_fuelZones.Count];
            activeStates = new bool[_fuelZones.Count];
            for (int i = 0; i < _fuelZones.Count; i++)
            {
                currentFuels[i] = _fuelZones[i].CurrentFuel;
                maxFuels[i] = _fuelZones[i].MaxFuelGenerated;
                activeStates[i] = _fuelZones[i].IsActive;
            }
        }

        #endregion

        private void OnDestroy()
        {
            FikaSync?.Dispose();
        }

        private string GetCurrentMapName()
        {
            try
            {
                var world = Singleton<GameWorld>.Instance;
                if (world?.MainPlayer != null)
                {
                    return world.MainPlayer.Location ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }

        #region Dev Overlay (OnGUI)

        private void OnGUI()
        {
            if (!ModEnabled.Value) return;

            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (world == null) return;

            var player = world.MainPlayer;
            if (player == null) return;

            // Dev mode overlay
            if (DevMode.Value && _showDevOverlay)
            {
                DrawDevOverlay(player);
            }

            // Save message notification
            if (!string.IsNullOrEmpty(_lastSaveMessage) && Time.time - _lastSaveMessageTime < 5f)
            {
                DrawSaveNotification();
            }
        }

        private void DrawDevOverlay(Player player)
        {
            Vector3 pos = player.Transform.position;
            string mapName = GetCurrentMapName();

            // Count zones for current map
            int mapZones = 0;
            foreach (var z in _fuelZones)
            {
                if (z.IsActiveInCurrentMap()) mapZones++;
            }

            string overlayKeyName = OverlayToggleKey.Value.ToString();
            string saveKeyName = SavePositionKey.Value.ToString();

            // Current zone fuel info
            string zoneFuelInfo = "";
            if (_currentZone != null && _isInZone)
            {
                zoneFuelInfo = $"\nЗона: {_currentZone.Name} | Fuel: {_currentZone.CurrentFuel:F0}/{_currentZone.MaxFuelGenerated:F0} | Active: {_currentZone.IsActive}";
            }

            string text = $"[FuelStationRefill Dev Mode]\n" +
                         $"Позиция: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})\n" +
                         $"Карта: {mapName}\n" +
                         $"В зоне: {_isInZone} | Зон на карте: {mapZones}\n" +
                         $"Текущая зона: {(_currentZone?.Name ?? "Нет")}{zoneFuelInfo}\n" +
                         $"Всего зон: {_fuelZones.Count}\n" +
                         $"Заправка: {_isRefueling}\n" +
                         $"\n" +
                         $"[{overlayKeyName}] - скрыть/показать оверлей\n" +
                         $"[{saveKeyName}] - сохранить позицию в JSON";

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = false
            };
            style.normal.textColor = Color.yellow;

            // Shadow
            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(12, 102, 420, 250), text, shadowStyle);
            GUI.Label(new Rect(10, 100, 420, 250), text, style);
        }

        private void DrawSaveNotification()
        {
            float elapsed = Time.time - _lastSaveMessageTime;
            float alpha = elapsed < 4f ? 1f : 1f - (elapsed - 4f);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };

            Color col = _lastSaveMessage.StartsWith("ОШИБКА") ? Color.red : Color.green;
            col.a = alpha;
            style.normal.textColor = col;

            float width = 600;
            float height = 40;
            float x = (Screen.width - width) / 2;
            float y = Screen.height * 0.3f;

            // Shadow
            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0, 0, 0, alpha);
            GUI.Label(new Rect(x + 2, y + 2, width, height), _lastSaveMessage, shadowStyle);
            GUI.Label(new Rect(x, y, width, height), _lastSaveMessage, style);
        }

        #endregion
    }
}
