//Author: Jellobeans
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ActorSkinSelector
{
    internal static class PluginInfo
    {
        public const string GUID    = "com.ravenfield.jellosuniiversalmultiskin";
        public const string Name    = "JellosUniversalMultiskin";
        public const string Version = "1.0.0";
    }

    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class ActorSkinSelectorPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static BepInEx.Configuration.ConfigEntry<float> CfgChanceDamage;
        internal static BepInEx.Configuration.ConfigEntry<float> CfgChanceReload;
        internal static BepInEx.Configuration.ConfigEntry<float> CfgChanceGrenade;
        internal static BepInEx.Configuration.ConfigEntry<float> CfgReloadCooldown;
        internal static BepInEx.Configuration.ConfigEntry<float> CfgVoiceMinDist;
        internal static BepInEx.Configuration.ConfigEntry<float> CfgVoiceMaxDist;
        internal static BepInEx.Configuration.ConfigEntry<float> CfgVoiceSpatialBlend;

        // Plugin entry point.
        private void Awake()
        {
            Log = Logger;
            SkinPool.Init();

            const string S = "PlayerVoice";
            CfgChanceDamage      = Config.Bind(S, "ChanceDamage",      0.8f,
                "Probability (0–1) that a bot vocalizes when taking damage.");
            CfgChanceReload      = Config.Bind(S, "ChanceReload",      0.4f,
                "Probability (0–1) that a bot vocalizes when reloading. High values may cause noise in large battles.");
            CfgChanceGrenade     = Config.Bind(S, "ChanceGrenade",     0.7f,
                "Probability (0–1) that a bot vocalizes when throwing a grenade.");
            CfgReloadCooldown    = Config.Bind(S, "ReloadCooldown",    8f,
                "Seconds before the same bot can say a reload line again.");
            CfgVoiceMinDist      = Config.Bind(S, "VoiceMinDistance",  40f,
                "Distance in meters at which voice is still at full volume.");
            CfgVoiceMaxDist      = Config.Bind(S, "VoiceMaxDistance",  800f,
                "Distance in meters at which voice becomes inaudible.");
            CfgVoiceSpatialBlend = Config.Bind(S, "VoiceSpatialBlend", 0.6f,
                "0 = fully 2D (always same volume), 1 = fully 3D (directional/distance). 0.6 recommended.");

            new Harmony(PluginInfo.GUID).PatchAll();

            var watcherGO = new GameObject("JellosMultiskin_Watcher");
            DontDestroyOnLoad(watcherGO);
            watcherGO.AddComponent<PlayerSkinWatcher>();

            Log.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version}: ready.");
        }
    }


    [HarmonyPatch(typeof(GameManager), "StartLevel")]
    internal static class Patch_GameManager_StartLevel
    {
        internal static bool AllowPassthrough = false;

        private static readonly HashSet<string> NON_MATCH_SCENES = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            "arcadelobby",
            "conquestlobby",
            "mainmenu",
            "loading",
            "loadingscreen",
            "mapeditor",
            "map editor",
            "results",
            "lobby",
        };

        // Skip the picker when testing maps in the editor.
        private static bool IsMapEditorTestPlay()
        {
            try { return MapEditor.MapEditor.isTestingMap; }
            catch { return false; }
        }

        // Hold level start until the player closes the picker.
        [HarmonyPrefix]
        static bool Prefix(MapEntryData entry, GameModeParameters parameters)
        {
            if (AllowPassthrough) return true;

            if (SkinSelectorUI.IsOpen)
            {
                ActorSkinSelectorPlugin.Log.LogInfo(
                    $"[JellosMultiskin] StartLevel('{entry?.sceneName}') - UI already open, suppressing.");
                return false;
            }

            if (IsMapEditorTestPlay())
            {
                ActorSkinSelectorPlugin.Log.LogInfo(
                    $"[JellosMultiskin] StartLevel('{entry?.sceneName}') - map editor test play, skipping UI.");
                return true;
            }

            if (entry != null && !string.IsNullOrEmpty(entry.sceneName)
                && NON_MATCH_SCENES.Contains(entry.sceneName))
            {
                ActorSkinSelectorPlugin.Log.LogInfo(
                    $"[JellosMultiskin] StartLevel('{entry.sceneName}') - non-match scene, skipping UI.");
                return true;
            }

            ActorSkinSelectorPlugin.Log.LogInfo(
                $"[JellosMultiskin] StartLevel('{entry?.sceneName}') - showing skin selector.");

            SkinSelectorUI.IsOpen = true;

            SkinSelectorUI.ShowOverlay(() =>
            {
                AllowPassthrough = true;
                try   { GameManager.StartLevel(entry, parameters); }
                finally { AllowPassthrough = false; }
            });

            return false;
        }
    }
}
