//Author: Jellobeans
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Ravenfield.Mods.Data;
using UnityEngine;

namespace ActorSkinSelector
{
    internal static class VoicePackScanner
    {
        internal static readonly bool ENABLED = true;

        private static readonly string LogPath =
            Path.Combine(BepInEx.Paths.PluginPath, "voicepacks.log");


        internal enum VoicePackType { TeamVoice, PlayerVoice }

        internal class VoicePackInfo
        {
            public string mutatorName;
            public string voicePackName;
            public string announcerName;
            public bool   hasInfantryLines;
            public bool   hasAnnouncerLines;
            public string sourceMod;
            public int    mutatorId;
            public VoicePackType voicePackType = VoicePackType.TeamVoice;
            public List<string> soundBanks = new List<string>();
        }

        internal static List<VoicePackInfo> DetectedPacks = new List<VoicePackInfo>();

        private static readonly Type ScriptedBehaviourType =
            typeof(ModManager).Assembly.GetType("ScriptedBehaviour");


        internal static void ScanAndLog()
        {
            DetectedPacks.Clear();

            if (ModManager.instance == null || ModManager.instance.loadedMutators == null)
            {
                WriteLog("ModManager not ready - nothing to scan.");
                return;
            }

            foreach (var mutator in ModManager.instance.loadedMutators)
            {
                if (mutator?.mutatorPrefab == null) continue;

                var info = TryExtractVoicePack(mutator);
                if (info != null)
                    DetectedPacks.Add(info);
            }

            WriteLog(BuildReport());
        }

        private static readonly HashSet<string> PlayerVoiceBankNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OnKillSoundBank", "DeathSoundBank", "FallingSoundBank",
            "LowHealthSoundBank", "LightDamageSoundBank", "MediumDamageSoundBank",
            "HeavyDamageSoundBank", "OnFirstSpawnSoundBank", "KillStreakSoundBank",
            "VictorySoundBank", "LoseMatchSoundBank", "RevengeKillSoundBank",
            "ReloadingSoundBank", "CaptureSoundBank", "ThrowGrenadeBank",
            "SmokeGrenadeBank", "StunGrenadeSoundBank", "AmmoBagSoundBank",
            "MedicBagSoundBank", "C4SoundBank", "TakingPointBank", "LosingPointBank",
        };

        private static VoicePackInfo TryExtractVoicePack(MutatorEntryData mutator)
        {
            GameObject prefab = mutator.mutatorPrefab;
            string mutatorNameLower = (mutator.name ?? "").ToLowerInvariant();

            var dc = prefab.GetComponentInChildren<DataContainer>(true);
            bool hasVoicePackName = false;
            string vpName = "";
            string announcerName = "";
            bool hasInfantry = true;
            bool hasAnnouncer = true;

            if (dc != null)
            {
                try
                {
                    hasVoicePackName = dc.HasString("voicePackName");
                    if (hasVoicePackName)
                        vpName = dc.GetString("voicePackName");
                }
                catch { }

                try { announcerName = dc.GetString("announcerName"); } catch { }
                try { hasInfantry   = dc.GetBool("hasInfantryLines"); } catch { }
                try { hasAnnouncer  = dc.GetBool("hasAnnouncerLines"); } catch { }
            }

            bool   hasVoicePackScript   = false;
            bool   hasPlayerVoiceScript = false;
            var    targetNames          = new List<string>();

            if (ScriptedBehaviourType != null)
            {
                var scripts = prefab.GetComponentsInChildren(ScriptedBehaviourType, true);
                foreach (var sb in scripts)
                {
                    string bName = "";
                    try
                    {
                        var bField = ScriptedBehaviourType.GetField("behaviour");
                        if (bField != null) bName = bField.GetValue(sb) as string ?? "";
                    }
                    catch { }

                    string sName = "";
                    string sText = "";
                    try
                    {
                        var sField = ScriptedBehaviourType.GetField("source");
                        if (sField != null)
                        {
                            var ta = sField.GetValue(sb) as TextAsset;
                            if (ta != null)
                            {
                                sName = ta.name;
                                sText = ta.text ?? "";
                            }
                        }
                    }
                    catch { }

                    if (bName.IndexOf("VoicePack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sName.IndexOf("VoicePack", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasVoicePackScript = true;
                    }

                    if (bName.IndexOf("PlayerVoiceMutator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        bName.IndexOf("PlayerVoice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sName.IndexOf("PlayerVoiceMutator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sName.IndexOf("PlayerVoice", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasPlayerVoiceScript = true;
                    }

                    if (!hasPlayerVoiceScript && sText.Length > 0)
                    {
                        if (sText.IndexOf("PlayerVoiceMutator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            sText.IndexOf("PlayerVoice", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasPlayerVoiceScript = true;
                        }
                    }

                    try
                    {
                        var tField = ScriptedBehaviourType.GetField("targets");
                        if (tField != null)
                        {
                            var targets = tField.GetValue(sb) as Array;
                            if (targets != null)
                            {
                                foreach (var t in targets)
                                {
                                    var nameField = t.GetType().GetField("name");
                                    if (nameField != null)
                                    {
                                        string tName = nameField.GetValue(t) as string;
                                        if (!string.IsNullOrEmpty(tName))
                                            targetNames.Add(tName);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            if (!hasPlayerVoiceScript)
            {
                if (mutatorNameLower.Contains("playervoice") ||
                    mutatorNameLower.Contains("player voice"))
                {
                    hasPlayerVoiceScript = true;
                }
            }

            if (!hasPlayerVoiceScript)
            {
                int pvBankHits = 0;
                var allBanks = prefab.GetComponentsInChildren<SoundBank>(true);
                foreach (var bank in allBanks)
                {
                    if (bank.gameObject != null && PlayerVoiceBankNames.Contains(bank.gameObject.name))
                        pvBankHits++;
                }
                if (pvBankHits >= 3)
                {
                    hasPlayerVoiceScript = true;
                    if (targetNames.Count == 0)
                    {
                        foreach (var bank in allBanks)
                        {
                            string goName = bank.gameObject?.name ?? "";
                            if (!string.IsNullOrEmpty(goName) && !targetNames.Contains(goName))
                                targetNames.Add(goName);
                        }
                    }
                }
            }

            if (!hasVoicePackName && !hasVoicePackScript && !hasPlayerVoiceScript) return null;

            VoicePackType packType = hasPlayerVoiceScript ? VoicePackType.PlayerVoice : VoicePackType.TeamVoice;

            if (packType == VoicePackType.PlayerVoice && string.IsNullOrEmpty(vpName))
                vpName = mutator.name ?? "(unnamed)";

            var info = new VoicePackInfo
            {
                mutatorName       = mutator.name,
                voicePackName     = string.IsNullOrEmpty(vpName) ? "(unnamed)" : vpName,
                announcerName     = string.IsNullOrEmpty(announcerName) ? "(none)" : announcerName,
                hasInfantryLines  = hasInfantry,
                hasAnnouncerLines = hasAnnouncer,
                sourceMod         = mutator.sourceMod != null ? mutator.sourceMod.title : "(unknown)",
                mutatorId         = mutator.uniqueMutatorID,
                voicePackType     = packType,
                soundBanks        = targetNames,
            };

            ActorSkinSelectorPlugin.Log?.LogInfo(
                $"[VoicePackScanner] Detected {packType}: '{info.mutatorName}' (ID={info.mutatorId}, banks={targetNames.Count})");

            return info;
        }


        private static string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine("           VOICE PACK SCAN REPORT");
            sb.AppendLine($"  Scanned: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Total mutators: {ModManager.instance.loadedMutators.Count}");
            sb.AppendLine($"  Voice packs found: {DetectedPacks.Count}");
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            if (DetectedPacks.Count == 0)
            {
                sb.AppendLine("  No voice packs detected among subscribed mutators.");
                sb.AppendLine("  Make sure you have Team Voices 3.0 voice pack mods subscribed.");
                return sb.ToString();
            }

            for (int i = 0; i < DetectedPacks.Count; i++)
            {
                var vp = DetectedPacks[i];
                sb.AppendLine($"── Voice Pack #{i + 1} ──────────────────────────────────────────");
                sb.AppendLine($"  Mutator Name     : {vp.mutatorName}");
                sb.AppendLine($"  Voice Pack Name  : {vp.voicePackName}");
                sb.AppendLine($"  Announcer Name   : {vp.announcerName}");
                sb.AppendLine($"  Infantry Lines   : {(vp.hasInfantryLines ? "Yes" : "No")}");
                sb.AppendLine($"  Announcer Lines  : {(vp.hasAnnouncerLines ? "Yes" : "No")}");
                sb.AppendLine($"  Source Mod       : {vp.sourceMod}");
                sb.AppendLine($"  Mutator ID       : {vp.mutatorId}");

                if (vp.soundBanks.Count > 0)
                {
                    sb.AppendLine($"  Sound Banks ({vp.soundBanks.Count}):");
                    foreach (var bank in vp.soundBanks)
                        sb.AppendLine($"    - {bank}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }


        private static void WriteLog(string content)
        {
            try
            {
                File.WriteAllText(LogPath, content);
                ActorSkinSelectorPlugin.Log?.LogInfo(
                    $"[VoicePackScanner] Report written to {LogPath}  ({DetectedPacks.Count} packs found)");
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogError(
                    $"[VoicePackScanner] Failed to write log: {ex.Message}");
            }
        }
    }


    [HarmonyPatch(typeof(ModManager), "FinalizeLoadedModContent")]
    internal static class Patch_ModManager_FinalizeContent
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            try
            {
                ActorSkinSelectorPlugin.Log?.LogInfo(
                    "[VoicePackScanner] Mod content finalized - scanning for voice packs...");
                VoicePackScanner.ScanAndLog();
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogError(
                    $"[VoicePackScanner] Scan failed: {ex}");
            }
        }
    }
}