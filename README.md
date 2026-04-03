# JUM
rf multiskin

HOW TO MODIFY THE PLUGIN:
This plugin was made with BepInEx & Harmony libraries. Requires BepInEx installed for compilation.


Plugin entry and runtime config: ActorSkinSelectorPlugin.cs //Write your own extra modules and link it here
UI layout/colors/buttons: SkinSelectorUI.cs //All in-game UI related functionalities. Modifying it is not recommended unless you want to add new features.
Skin selection logic/rarity/voice roll: SkinPool.cs //Edit hardcoded chance% per tier here. You can make the plugin read the chance% from Ravenfield\BepInEx\config\ and apply at runtime.
Save/load preset format: SkinConfig.cs
Spawn/vehicle skin application: SkinHook.cs //Allows for adding custom vehicle types, Assembly patching mod compatibility. A highly requested feature is skins for boats but you can add it yourself.
Voice scanning/routing/activation: //These are made to interact with the game's workshop system to grep and redirect voice pack instances. Highly delicate, not recommended to modify unless the current modding API for mutators get deprecated in the future.
  VoicePackScanner.cs
  VoicePackRouter.cs
  VoicePackActivator.cs
  PlayerVoiceEventBridge.cs

