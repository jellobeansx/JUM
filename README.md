# JUM
Ravenfield multiskin plugin.

## How To Modify The Plugin
This plugin is built with **BepInEx** + **Harmony**.  
You need BepInEx references available to compile.

### Core Files
- `ActorSkinSelectorPlugin.cs`  
  Plugin entry point and runtime config bindings.  
  Add any extra module bootstrap calls here.

- `SkinSelectorUI.cs`  
  In-game UI layout, colors, buttons, and interactions.  
  Edit only if you want UI/UX changes or new UI features.

- `SkinPool.cs`  
  Skin selection logic, rarity weights, and voice roll behavior.  
  This is where hardcoded chance values live.

- `SkinConfig.cs`  
  Save/load preset structure and JSON serialization logic.

- `SkinHook.cs`  
  Spawn hooks, vehicle seat hooks, and role-based skin application.  
  Good place to extend behavior for custom vehicle/mod compatibility.

### Voice System Files
These files scan workshop mutators, cache sound banks, and route voice playback.  
They are tightly coupled to current mutator/runtime behavior, so only edit if needed.

- `VoicePackScanner.cs`
- `VoicePackRouter.cs`
- `VoicePackActivator.cs`
- `PlayerVoiceEventBridge.cs`

## Build
From Ravenfield root:

```powershell
dotnet build "<YourSourcePath>\\ActorSkinSelector.csproj" -c Release
