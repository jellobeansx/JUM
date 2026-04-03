//Author: Jellobeans
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ravenfield.Configuration;
using UnityEngine;

namespace ActorSkinSelector
{
    internal static class VoicePackActivator
    {
        internal static readonly Dictionary<int, int> VoicePackTeamAssignment =
            new Dictionary<int, int>();

        private static readonly string[] TeamVoicesNames =
        {
            "Team Voices Eagle", "Team Voices Raven",
            "TeamVoicesEagle",   "TeamVoicesRaven",
            "Team Voices 3.0",   "Team Voices"
        };

        internal static readonly List<GameObject> SpawnedPlayerVoicePrefabs = new List<GameObject>();

        internal static void ActivateAssignedVoicePacks()
        {
            if (!VoicePackScanner.ENABLED) return;

            var gameInfo = GameManager.instance?.gameInfo;
            if (gameInfo == null) return;

            SkinPool.Init();

            var teamPacks = new HashSet<int>[2];
            teamPacks[0] = new HashSet<int>();
            teamPacks[1] = new HashSet<int>();

            for (int team = 0; team < 2; team++)
            {
                if (SkinPool.Selected[team] == null) continue;
                foreach (var kv in SkinPool.Selected[team])
                {
                    var entries = kv.Value.voiceEntries;
                    if (entries == null || entries.Count == 0) continue;

                    foreach (var ve in entries)
                    {
                        if (ve.mutatorId == -2)
                        {
                            foreach (var vp in VoicePackScanner.DetectedPacks)
                                teamPacks[team].Add(vp.mutatorId);
                        }
                        else if (ve.mutatorId >= 0)
                        {
                            teamPacks[team].Add(ve.mutatorId);
                        }
                    }
                }
            }

            var allIds = new HashSet<int>(teamPacks[0]);
            allIds.UnionWith(teamPacks[1]);
            VoicePackTeamAssignment.Clear();

            if (allIds.Count == 0)
            {
                ActorSkinSelectorPlugin.Log?.LogInfo(
                    "[VoicePackActivator] No voice packs assigned - skipping.");
                return;
            }

            ActorSkinSelectorPlugin.Log?.LogInfo(
                $"[VoicePackActivator] Activating {allIds.Count} voice pack(s)...");

            bool needsTeamVoicesMaster = false;
            foreach (int vpId in allIds)
            {
                foreach (var vp in VoicePackScanner.DetectedPacks)
                {
                    if (vp.mutatorId == vpId &&
                        vp.voicePackType != VoicePackScanner.VoicePackType.PlayerVoice)
                    { needsTeamVoicesMaster = true; break; }
                }
                if (needsTeamVoicesMaster) break;
            }

            if (needsTeamVoicesMaster)
                ActivateTeamVoicesMutators(gameInfo);
            else
                ActorSkinSelectorPlugin.Log?.LogInfo(
                    "[VoicePackActivator] All packs are PlayerVoice type - skipping Team Voices master activation.");

            foreach (int vpId in allIds)
            {
                bool onEagle = teamPacks[0].Contains(vpId);
                bool onRaven = teamPacks[1].Contains(vpId);
                int assignedTeam;
                if (onEagle && onRaven) assignedTeam = 2;
                else if (onRaven)       assignedTeam = 1;
                else                    assignedTeam = 0;

                VoicePackTeamAssignment[vpId] = assignedTeam;

                bool isPlayerVoice = false;
                foreach (var vp in VoicePackScanner.DetectedPacks)
                    if (vp.mutatorId == vpId && vp.voicePackType == VoicePackScanner.VoicePackType.PlayerVoice)
                    { isPlayerVoice = true; break; }

                if (isPlayerVoice) continue;

                var mutator = FindMutatorById(vpId);
                if (mutator == null)
                {
                    ActorSkinSelectorPlugin.Log?.LogWarning(
                        $"[VoicePackActivator] Could not find mutator ID {vpId} - skipping.");
                    continue;
                }

                if (!gameInfo.activeMutators.Contains(mutator))
                {
                    gameInfo.activeMutators.Add(mutator);
                    ActorSkinSelectorPlugin.Log?.LogInfo(
                        $"[VoicePackActivator]   + Activated: {mutator.name}");
                }

                try
                {
                    var config = ModManager.GetMutatorConfiguration(mutator);
                    if (config != null)
                    {
                        config.SetDropdown("assignedTeam", assignedTeam);
                        ActorSkinSelectorPlugin.Log?.LogInfo(
                            $"[VoicePackActivator]   -> assignedTeam={assignedTeam} for {mutator.name}");
                    }
                }
                catch (Exception ex)
                {
                    ActorSkinSelectorPlugin.Log?.LogWarning(
                        $"[VoicePackActivator]   Could not set assignedTeam for {mutator.name}: {ex.Message}");
                }
            }
        }

        internal static void SpawnPlayerVoicePrefabs()
        {
            foreach (var go in SpawnedPlayerVoicePrefabs)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            SpawnedPlayerVoicePrefabs.Clear();

            var SBType = typeof(ModManager).Assembly.GetType("Lua.ScriptedBehaviour")
                      ?? typeof(ModManager).Assembly.GetType("ScriptedBehaviour");

            foreach (var vp in VoicePackScanner.DetectedPacks)
            {
                if (vp.voicePackType != VoicePackScanner.VoicePackType.PlayerVoice) continue;

                var mutator = FindMutatorById(vp.mutatorId);
                if (mutator == null || mutator.mutatorPrefab == null) continue;

                var instance = UnityEngine.Object.Instantiate(mutator.mutatorPrefab);
                instance.name = mutator.mutatorPrefab.name + "(Clone)";
                UnityEngine.Object.DontDestroyOnLoad(instance);

                if (SBType != null)
                {
                    var scripts = instance.GetComponentsInChildren(SBType, true);
                    foreach (var sb in scripts)
                    {
                        try
                        {
                            var smField = SBType.GetField("sourceMutator");
                            if (smField != null)
                            {
                                var med = smField.GetValue(sb) as MutatorEntryData;
                                if (med != null)
                                {
                                    instance.name = med.name + "(Clone)";
                                }
                            }
                        }
                        catch { }

                        UnityEngine.Object.Destroy(sb as Component);
                    }
                }

                var audioSources = instance.GetComponentsInChildren<AudioSource>(true);
                foreach (var src in audioSources)
                {
                    src.spatialBlend = 1f;
                }

                SpawnedPlayerVoicePrefabs.Add(instance);
                ActorSkinSelectorPlugin.Log?.LogInfo(
                    $"[VoicePackActivator]   + Spawned PV prefab (scripts stripped): {instance.name}");
            }
        }

        private static void ActivateTeamVoicesMutators(GameInfoContainer gameInfo)
        {
            if (ModManager.instance?.loadedMutators == null) return;

            foreach (var mutator in ModManager.instance.loadedMutators)
            {
                if (mutator == null) continue;
                string name = mutator.name ?? "";

                bool isTeamVoices = false;
                foreach (var tvName in TeamVoicesNames)
                {
                    if (name.IndexOf(tvName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isTeamVoices = true;
                        break;
                    }
                }

                if (isTeamVoices && !gameInfo.activeMutators.Contains(mutator))
                {
                    gameInfo.activeMutators.Add(mutator);
                    ActorSkinSelectorPlugin.Log?.LogInfo(
                        $"[VoicePackActivator]   + Activated Team Voices master: {mutator.name}");
                }
            }
        }

        internal static MutatorEntryData FindMutatorById(int id)
        {
            if (ModManager.instance?.loadedMutators == null) return null;
            foreach (var m in ModManager.instance.loadedMutators)
                if (m != null && m.uniqueMutatorID == id) return m;
            return null;
        }
    }

    [HarmonyPatch(typeof(ModManager), "SpawnAllEnabledMutatorPrefabs")]
    internal static class Patch_SpawnMutators_ActivateVoicePacks
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            try
            {
                VoicePackActivator.ActivateAssignedVoicePacks();
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogError(
                    $"[VoicePackActivator] Failed: {ex}");
            }
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            try
            {
                VoicePackActivator.SpawnPlayerVoicePrefabs();
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogError(
                    $"[VoicePackActivator] PV spawn failed: {ex}");
            }
        }
    }
}
