//Author: Jellobeans
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ActorSkinSelector
{
    internal static class VoicePackRouter
    {
        internal static Actor DyingActor;
        internal static Actor KillerActor;
        internal static bool  IsFriendlyFire;

        internal static Actor EventActor;

        internal static int EventTeam = -1;

        internal static readonly Dictionary<Actor, int> ActorVoicePack =
            new Dictionary<Actor, int>();

        internal static readonly Dictionary<SoundBank, int> BankToVoicePack =
            new Dictionary<SoundBank, int>();
        internal static readonly Dictionary<SoundBank, string> BankToName =
            new Dictionary<SoundBank, string>();
        internal static readonly Dictionary<string, SoundBank> VoicePackBanks =
            new Dictionary<string, SoundBank>();

        private static bool _redirecting;

        private static readonly Dictionary<string, string> BankNameMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "DeathBank",        "DeathSoundBank" },
            { "KillBank",         "OnKillSoundBank" },
            { "FriendlyFireBank", "OnKillSoundBank" },
            { "DirectionalBank",  "OnKillSoundBank" },
            { "DeathSoundBank",       "DeathBank" },
            { "OnKillSoundBank",      "KillBank" },
            { "KillStreakSoundBank",   "KillBank" },
            { "RevengeKillSoundBank",  "KillBank" },
        };

        private static readonly string DebugPath =
            Path.Combine(BepInEx.Paths.PluginPath, "voicerouter_debug.txt");
        private static readonly StringBuilder DebugLog = new StringBuilder();

        internal static void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            ActorSkinSelectorPlugin.Log?.LogInfo(line);
            lock (DebugLog) { DebugLog.AppendLine(line); }
        }

        internal static bool TryRedirect(SoundBank __instance)
        {
            if (_redirecting) return false;

            int bankVpId;
            if (!BankToVoicePack.TryGetValue(__instance, out bankVpId))
                return false;

            string bankName;
            if (!BankToName.TryGetValue(__instance, out bankName))
            {
                Log($"SUPPRESS {__instance.name}: no BankToName entry");
                return true;
            }

            Actor actor = ResolveActor(bankName);
            if (actor == null)
            {
                Log($"PASS {bankName} VP#{bankVpId}: no actor context");
                return false;
            }
            int actorVpId;
            if (!ActorVoicePack.TryGetValue(actor, out actorVpId))
            {
                actorVpId = TryAutoAssign(actor);
                if (actorVpId < 0)
                {
                    Log($"PASS {bankName}: actor {actor.name} has no VP assigned");
                    return false;
                }
            }
            else if (!IsVoicePackAllowedForTeam(actorVpId, actor.team))
            {
                ActorVoicePack.Remove(actor);
                Log($"PASS {bankName}: stale actor VP#{actorVpId} not allowed for team={actor.team}");
                return false;
            }

            if (actorVpId == bankVpId)
            {
                return false;
            }

            SoundBank correctBank;
            string lookupKey = $"{actorVpId}:{bankName}";
            if (!VoicePackBanks.TryGetValue(lookupKey, out correctBank))
            {
                string altName;
                if (BankNameMap.TryGetValue(bankName, out altName))
                {
                    lookupKey = $"{actorVpId}:{altName}";
                    VoicePackBanks.TryGetValue(lookupKey, out correctBank);
                }

                if (correctBank == null)
                {
                    Log($"SKIP {bankName}: no bank for VP#{actorVpId}");
                    return false;
                }
            }

            if (correctBank.clips == null || correctBank.clips.Length == 0)
                return false;

            if (__instance.audioSource == null)
            {
                Log($"SKIP {bankName}: audioSource null on __instance");
                return false;
            }

            _redirecting = true;
            try
            {
                int idx = UnityEngine.Random.Range(0, correctBank.clips.Length);
                __instance.audioSource.PlayOneShot(correctBank.clips[idx]);
                Log($"REDIRECT {bankName}: {actor.name} VP#{bankVpId}->VP#{actorVpId} clip#{idx}");
            }
            finally { _redirecting = false; }

            return true;
        }

        private static Actor ResolveActor(string bankName)
        {
            if (DyingActor != null || KillerActor != null)
            {
                if (bankName == "DeathBank")
                    return DyingActor;
                if (bankName == "KillBank")
                    return KillerActor;
                if (bankName == "FriendlyFireBank")
                    return IsFriendlyFire ? KillerActor : null;

                if (bankName == "DeathSoundBank" || bankName == "HeavyDamageSoundBank" ||
                    bankName == "MediumDamageSoundBank" || bankName == "LightDamageSoundBank" ||
                    bankName == "LowHealthSoundBank")
                    return DyingActor;
                if (bankName == "OnKillSoundBank" || bankName == "KillStreakSoundBank" ||
                    bankName == "RevengeKillSoundBank")
                    return KillerActor;
            }

            if (EventActor != null)
                return EventActor;

            if (EventTeam >= 0)
            {
                Actor local = null;
                try { local = LocalPlayer.actor; } catch { local = null; }
                if (local != null && !local.dead && local.team == EventTeam)
                    return local;
            }

            return null;
        }

        internal static void AssignActorVoicePack(Actor actor, int voicePackMutatorId)
        {
            if (actor == null) return;
            if (voicePackMutatorId < 0)
            {
                ActorVoicePack.Remove(actor);
                return;
            }
            if (!IsVoicePackAllowedForTeam(voicePackMutatorId, actor.team))
            {
                ActorVoicePack.Remove(actor);
                Log($"ASSIGN-SKIP {actor.name}: VP#{voicePackMutatorId} not allowed for team={actor.team}");
                return;
            }
            ActorVoicePack[actor] = voicePackMutatorId;
            Log($"ASSIGN {actor.name} → VP#{voicePackMutatorId} (total={ActorVoicePack.Count})");
        }

        private static int TryAutoAssign(Actor actor)
        {
            if (actor == null) return -1;

            ActorSkin skin = actor.overrideActorSkin;
            if (skin == null) return -1;

            int team = actor.team;
            if (team < 0 || team > 1) return -1;

            int vpId = SkinPool.GetWeightedRandomVoicePack(team, skin);
            if (vpId >= 0)
            {
                if (!IsVoicePackAllowedForTeam(vpId, team))
                {
                    Log($"AUTO-ASSIGN-SKIP {actor.name} (skin={skin.name} team={team}) VP#{vpId} not allowed for team");
                    return -1;
                }
                ActorVoicePack[actor] = vpId;
                Log($"AUTO-ASSIGN {actor.name} (skin={skin.name} team={team}) → VP#{vpId}");
                return vpId;
            }

            return -1;
        }

        private static bool IsVoicePackAllowedForTeam(int mutatorId, int team)
        {
            if (team < 0 || team > 1) return false;

            int assignedTeam;
            if (!VoicePackActivator.VoicePackTeamAssignment.TryGetValue(mutatorId, out assignedTeam))
                return true;

            return assignedTeam == 2 || assignedTeam == team;
        }

        internal static void CacheSoundBanks()
        {
            BankToVoicePack.Clear();
            BankToName.Clear();
            VoicePackBanks.Clear();
            lock (DebugLog) { DebugLog.Clear(); }

            if (!VoicePackScanner.ENABLED) return;

            var SBType = typeof(ModManager).Assembly.GetType("Lua.ScriptedBehaviour")
                      ?? typeof(ModManager).Assembly.GetType("ScriptedBehaviour");
            var NTType = typeof(ModManager).Assembly.GetType("Lua.NamedTarget")
                      ?? typeof(ModManager).Assembly.GetType("NamedTarget");

            if (SBType == null || NTType == null)
            {
                Log($"CACHE FAIL: SBType={SBType != null} NTType={NTType != null}");
                return;
            }

            var targetsField       = SBType.GetField("targets");
            var behaviourField     = SBType.GetField("behaviour");
            var sourceField        = SBType.GetField("source");
            var sourceMutatorField = SBType.GetField("sourceMutator");
            var ntNameField        = NTType.GetField("name");
            var ntValueField       = NTType.GetField("value");

            if (targetsField == null || ntNameField == null || ntValueField == null)
            {
                Log($"CACHE FAIL: fields null t={targetsField!=null} n={ntNameField!=null} v={ntValueField!=null}");
                return;
            }

            var vpIds = new HashSet<int>();
            foreach (var vp in VoicePackScanner.DetectedPacks)
                vpIds.Add(vp.mutatorId);

            var allSBs = UnityEngine.Object.FindObjectsOfType(SBType);
            Log($"CACHE: scanning {allSBs.Length} ScriptedBehaviours, {vpIds.Count} VP IDs");

            foreach (var sbObj in allSBs)
            {
                try
                {
                    string bName = behaviourField?.GetValue(sbObj) as string ?? "";
                    string sName = "";
                    try { var ta = sourceField?.GetValue(sbObj) as TextAsset; if (ta != null) sName = ta.name; } catch { }

                    bool isVP = bName.IndexOf("VoicePack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                sName.IndexOf("VoicePack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                bName.IndexOf("PlayerVoiceMutator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                bName.IndexOf("PlayerVoice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                sName.IndexOf("PlayerVoice", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isVP) continue;

                    int mutId = -1;
                    try { var med = sourceMutatorField?.GetValue(sbObj) as MutatorEntryData; if (med != null) mutId = med.uniqueMutatorID; } catch { }

                    if (mutId < 0)
                    {
                        var comp = sbObj as Component;
                        if (comp != null)
                        {
                            Transform tr = comp.transform;
                            while (tr != null && mutId < 0)
                            {
                                string goName = tr.gameObject.name.Replace("(Clone)", "").Trim();
                                foreach (var vp in VoicePackScanner.DetectedPacks)
                                    if (goName == vp.mutatorName || goName.Contains(vp.mutatorName))
                                    { mutId = vp.mutatorId; break; }
                                tr = tr.parent;
                            }
                        }
                    }

                    if (mutId < 0 || !vpIds.Contains(mutId)) continue;

                    var targets = targetsField.GetValue(sbObj) as Array;
                    if (targets == null) continue;

                    foreach (var nt in targets)
                    {
                        string tName = ntNameField.GetValue(nt) as string;
                        var tValue = ntValueField.GetValue(nt) as UnityEngine.Object;
                        if (string.IsNullOrEmpty(tName) || tValue == null) continue;

                        SoundBank bank = null;
                        if (tValue is GameObject go) bank = go.GetComponent<SoundBank>();
                        else if (tValue is SoundBank sb2) bank = sb2;
                        else if (tValue is Component c) bank = c.gameObject.GetComponent<SoundBank>();
                        if (bank == null) continue;

                        BankToVoicePack[bank] = mutId;
                        BankToName[bank] = tName;
                        VoicePackBanks[$"{mutId}:{tName}"] = bank;
                    }
                }
                catch { }
            }

            Log($"CACHE: {BankToVoicePack.Count} banks from ScriptedBehaviours, {VoicePackBanks.Count} entries");

            CachePlayerVoiceBanks();

            Log($"CACHE DONE: {BankToVoicePack.Count} banks total, {VoicePackBanks.Count} entries");
            foreach (var kv in VoicePackBanks)
                Log($"  BANK KEY: '{kv.Key}' clips={kv.Value?.clips?.Length ?? 0}");
            WriteDebug();
        }

        private static void CachePlayerVoiceBanks()
        {
            foreach (var pvGO in VoicePackActivator.SpawnedPlayerVoicePrefabs)
            {
                if (pvGO == null) continue;

                string goName = pvGO.name.Replace("(Clone)", "").Trim();
                int mutId = -1;
                foreach (var vp in VoicePackScanner.DetectedPacks)
                {
                    if (vp.voicePackType != VoicePackScanner.VoicePackType.PlayerVoice) continue;
                    if (goName == vp.mutatorName || goName.Contains(vp.mutatorName) || vp.mutatorName.Contains(goName))
                    {
                        mutId = vp.mutatorId;
                        break;
                    }
                }

                if (mutId < 0)
                {
                    foreach (var vp in VoicePackScanner.DetectedPacks)
                    {
                        if (vp.voicePackType != VoicePackScanner.VoicePackType.PlayerVoice) continue;
                        var med = VoicePackActivator.FindMutatorById(vp.mutatorId);
                        if (med != null && (goName == med.name || med.mutatorPrefab?.name == goName))
                        {
                            mutId = vp.mutatorId;
                            break;
                        }
                    }
                }

                if (mutId < 0)
                {
                    Log($"PV CACHE: could not resolve mutator ID for '{goName}'");
                    continue;
                }

                var banks = pvGO.GetComponentsInChildren<SoundBank>(true);
                foreach (var bank in banks)
                {
                    string bankName = bank.gameObject?.name ?? "";
                    if (string.IsNullOrEmpty(bankName)) continue;

                    BankToVoicePack[bank] = mutId;
                    BankToName[bank] = bankName;
                    string key = $"{mutId}:{bankName}";
                    VoicePackBanks[key] = bank;
                }

                Log($"PV CACHE: '{goName}' -> VP#{mutId}, {banks.Length} banks");
            }
        }

        internal static void WriteDebug()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"═══ VoicePackRouter ═══ {DateTime.Now:HH:mm:ss}");
                sb.AppendLine($"Banks: {VoicePackBanks.Count} | Actors: {ActorVoicePack.Count}");
                sb.AppendLine();

                for (int t = 0; t < 2; t++)
                {
                    sb.AppendLine($"Team {t}:");
                    if (SkinPool.Selected[t] != null)
                        foreach (var kv in SkinPool.Selected[t])
                            sb.AppendLine($"  {kv.Key.name} → vpId={kv.Value.voicePackMutatorId}");
                }

                sb.AppendLine();
                sb.AppendLine("── LIVE ACTORS ──");
                foreach (var kv in ActorVoicePack)
                {
                    try { sb.AppendLine($"  {kv.Key?.name ?? "?"} → VP#{kv.Value}"); }
                    catch { }
                }

                sb.AppendLine();
                sb.AppendLine("── EVENT LOG ──");
                lock (DebugLog) { sb.Append(DebugLog); }
                File.WriteAllText(DebugPath, sb.ToString());
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(Actor), "Kill")]
    internal static class Patch_Actor_Kill_TrackActors
    {
        [HarmonyPrefix]
        static void Prefix(Actor __instance, DamageInfo info)
        {
            try
            {
                VoicePackRouter.DyingActor = __instance;
                VoicePackRouter.KillerActor = info.sourceActor;
                VoicePackRouter.IsFriendlyFire =
                    info.sourceActor != null &&
                    info.sourceActor != __instance &&
                    info.sourceActor.team == __instance.team;
            }
            catch { }
        }

        [HarmonyFinalizer]
        static void Finalizer()
        {
            VoicePackRouter.DyingActor = null;
            VoicePackRouter.KillerActor = null;
            VoicePackRouter.IsFriendlyFire = false;
        }
    }


    [HarmonyPatch]
    internal static class Patch_ScriptEvent_UnsafeInvoke
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = typeof(ModManager).Assembly.GetType("Lua.ScriptEvent")
                    ?? typeof(ModManager).Assembly.GetType("ScriptEvent");
            if (type == null)
            {
                ActorSkinSelectorPlugin.Log?.LogWarning("[VPR] ScriptEvent type NOT FOUND!");
                return null;
            }
            var method = type.GetMethod("UnsafeInvoke",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method == null)
                ActorSkinSelectorPlugin.Log?.LogWarning("[VPR] UnsafeInvoke method NOT FOUND!");
            else
                ActorSkinSelectorPlugin.Log?.LogInfo($"[VPR] ScriptEvent.UnsafeInvoke patch target resolved: {method}");
            return method;
        }

        [HarmonyPrefix]
        static void Prefix(object[] args)
        {
            try
            {
                if (args != null && args.Length > 0)
                {
                    if (args[0] is Actor a)
                    {
                        VoicePackRouter.EventActor = a;
                        VoicePackRouter.EventTeam  = a.team;
                    }
                    else if (args[0] is int t)
                    {
                        VoicePackRouter.EventTeam = t;
                    }
                }
            }
            catch { }
        }

        [HarmonyFinalizer]
        static void Finalizer()
        {
            VoicePackRouter.EventActor = null;
            VoicePackRouter.EventTeam  = -1;
        }
    }


    [HarmonyPatch(typeof(SoundBank), "PlayRandom")]
    internal static class Patch_SoundBank_PlayRandom
    {
        [HarmonyPrefix]
        static bool Prefix(SoundBank __instance)
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return true;
                if (VoicePackRouter.BankToVoicePack.Count == 0) return true;

                return !VoicePackRouter.TryRedirect(__instance);
            }
            catch { return true; }
        }
    }


    [HarmonyPatch(typeof(SoundBank), "PlaySoundBank")]
    internal static class Patch_SoundBank_PlaySoundBank
    {
        [HarmonyPrefix]
        static bool Prefix(SoundBank __instance, int index)
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return true;
                if (VoicePackRouter.BankToVoicePack.Count == 0) return true;

                return !VoicePackRouter.TryRedirect(__instance);
            }
            catch { return true; }
        }
    }


    [HarmonyPatch(typeof(ModManager), "SpawnAllEnabledMutatorPrefabs")]
    internal static class Patch_SpawnMutators_CacheBanks
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            try
            {
                VoicePackRouter.ActorVoicePack.Clear();
                VoicePackRouter.CacheSoundBanks();
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogError($"[VPR] Cache failed: {ex}");
            }
        }
    }


    [HarmonyPatch(typeof(ActorManager), "Update")]
    internal static class Patch_ActorManager_FlushLog
    {
        private static float _last;

        [HarmonyPostfix]
        static void Postfix()
        {
            if (Time.time - _last > 5f)
            {
                _last = Time.time;
                VoicePackRouter.WriteDebug();
            }
        }
    }
}