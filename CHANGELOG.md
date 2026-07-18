# Changelog — Fuel Station Refill

## 1.2.3 (2026-07-19)

- Increased tolerance from 0.01f to 0.5f — canisters within 0.5 units of max are considered full
- Prevents refuel attempt on nearly-full canisters that would waste station fuel
- `UpdateRefuelingIncremental` stops early when canister reaches max

## 1.2.2 (2026-07-19)

- Fixed `_refuelDuration` not accounting for canister max capacity (caused overfill)
- Added `SetResourceValue` verification with fallback to direct `rc.Value` assignment
- Detailed per-item logging in `FindRefuelableItem` for diagnostics
- Final canister state logged after refuel completes

## 1.2.1 (2026-07-19)

- **Fixed refuel rate**: fuel now applied incrementally at `RefuelRate` units/sec (was applied all at once)
- Default `DefaultZoneRadius` changed from 5m to 1m
- Default `MinFuel` changed from 50 to 0
- Default `MaxFuel` changed from 200 to 40
- Station exhaustion during refuel now properly cancels Plant state

## 1.2.0 (2026-07-19)

- **Native game interaction system**: replaced custom OnGUI overlay with game's built-in Plant state
  - Uses `PlantStateClass` for timed refueling (like quest item planting)
  - Native objectives panel shows refuel progress with timer
  - Interaction sound via `Player.PlayInteractionSound` (generator repair loop)
  - Proper cancel handling: leave zone or move to abort refuel
- Zone hint text shown via `ShowObjectivesPanel` when entering fuel zone
- Added `UnityEngine.AudioModule` and `Sirenix.Serialization` references
- Refactored interaction flow: `TryStartNativeRefuel` → `Plant()` → callback `OnRefuelComplete`
- Removed custom `OnGUI` progress bar and interaction prompt (kept dev overlay only)

## 1.1.2 (2026-07-18)

- Fixed `GetResourceComponent`: `ResourceHolderComponent` is a field, not a property — use `GetField` instead of `GetProperty`

## 1.1.1 (2026-07-18)

- Fixed JSON parsing: switched from `JsonUtility` to `Newtonsoft.Json` (zones now load correctly)
- Interaction prompt always shows in zone (with/without canisters)
- Added detailed inventory diagnostic logging for debugging canister detection

## 1.1.0 (2026-07-18)

- **Fika synchronization**: fuel state synced across all Fika clients
  - Host randomizes fuel at raid start, broadcasts to all clients
  - Fuel consumption synced — if one player drains a station, others see the update
  - Configurable: `EnableFikaSync` (bool, default true)
- **Fuel randomization**: each station gets a random fuel amount per raid
  - Configurable range: `MinFuel` (default 50), `MaxFuel` (default 200)
- **Station deactivation**: when fuel reaches 0, station becomes inactive for the rest of the raid
- **Mid-refuel exhaustion**: if fuel runs out during refueling, action stops and partial fuel is applied
- **Dev overlay upgrade**: shows current zone name, fuel remaining/max, active status
- New files: `FuelStationPackets.cs` (Fika network packets), `FuelStationFikaSync.cs` (sync manager)
- Added `Fika.Core.dll` reference for network integration

## 1.0.0 (2026-07-18)

- Initial release
- Refuel fuel canisters at predefined fuel station zones
- Configurable refuel rate (fuel units per second)
- Configurable interaction range (meters)
- JSON-based zone configuration with map filtering
- Dev mode with configurable hotkeys:
  - Overlay toggle key (default F9)
  - Save position key (default F10) — auto-saves current coordinates to FuelStationZones.json
- Partial refuel support (progress saved if interrupted)
- Visual progress bar during refueling
- On-screen notification when position is saved
- Pre-configured zones for Customs gas station (3 points)
