//Author: Jellobeans
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ActorSkinSelector
{
    internal static class PlayerVoiceEventBridge
    {
        private static readonly Dictionary<Actor, float> LastPlayTime = new Dictionary<Actor, float>();
        private const float COOLDOWN = 0.3f;

        internal static float CHANCE_DAMAGE      => ActorSkinSelectorPlugin.CfgChanceDamage?.Value      ?? 0.8f;
        internal static float CHANCE_RELOAD      => ActorSkinSelectorPlugin.CfgChanceReload?.Value      ?? 0.4f;
        internal static float CHANCE_GRENADE     => ActorSkinSelectorPlugin.CfgChanceGrenade?.Value     ?? 0.7f;
        internal static float RELOAD_COOLDOWN    => ActorSkinSelectorPlugin.CfgReloadCooldown?.Value    ?? 8f;

        internal static readonly Dictionary<Actor, float> LastReloadTime = new Dictionary<Actor, float>();
        internal static readonly Dictionary<Actor, int> KillCounts = new Dictionary<Actor, int>();
        internal static readonly Dictionary<Actor, Actor> LastDamageSource = new Dictionary<Actor, Actor>();
        internal static readonly Dictionary<Actor, bool> WasReloading = new Dictionary<Actor, bool>();
        internal static readonly HashSet<Actor> HasSpawned = new HashSet<Actor>();
        internal static readonly HashSet<Actor> IsFalling = new HashSet<Actor>();
        internal static readonly HashSet<Actor> DeathVoicePlayed = new HashSet<Actor>();

        internal static void ClearState()
        {
            LastPlayTime.Clear();
            LastReloadTime.Clear();
            KillCounts.Clear();
            LastDamageSource.Clear();
            WasReloading.Clear();
            HasSpawned.Clear();
            IsFalling.Clear();
            DeathVoicePlayed.Clear();
        }

        internal static void ClearActorState(Actor actor)
        {
            KillCounts.Remove(actor);
            IsFalling.Remove(actor);
            WasReloading.Remove(actor);
            LastDamageSource.Remove(actor);
            DeathVoicePlayed.Remove(actor);
        }

        internal static void TryPlayDeathVoice(Actor actor)
        {
            if (actor == null) return;
            if (DeathVoicePlayed.Contains(actor)) return;

            int vpId;
            if (!HasPVAssigned(actor, out vpId))
            {
                VoicePackRouter.Log($"[PVBridge] DEATH-SKIP {actor.name}: no PV assigned");
                return;
            }

            bool played = PlayBankForActor(actor, vpId, "DeathSoundBank", bypassCooldown: true)
                       || PlayBankForActor(actor, vpId, "DeathBank", bypassCooldown: true);

            if (played)
            {
                DeathVoicePlayed.Add(actor);
            }
            else
            {
                VoicePackRouter.Log($"[PVBridge] DEATH-MISS {actor.name}: VP#{vpId} had no playable death bank");
            }
        }

        internal static bool HasPVAssigned(Actor actor, out int vpId)
        {
            vpId = -1;
            if (actor == null) return false;

            if (!VoicePackRouter.ActorVoicePack.TryGetValue(actor, out vpId))
                vpId = TryLazyAssign(actor);

            if (vpId < 0) return false;
            if (IsBotVoiceCapable(vpId)) return true;

            int reassigned = TryReassignPlayerVoicePack(actor);
            if (reassigned < 0) return false;

            vpId = reassigned;
            return true;
        }

        internal static int TryLazyAssign(Actor actor)
        {
            var skin = actor.overrideActorSkin;
            if (skin == null) return -1;
            int team = actor.team;
            if (team < 0 || team > 1) return -1;

            int vpId = SkinPool.GetWeightedRandomVoicePack(team, skin);
            if (vpId >= 0)
            {
                VoicePackRouter.ActorVoicePack[actor] = vpId;
                VoicePackRouter.Log($"[PVBridge] LAZY-ASSIGN {actor.name} skin={skin.name} → VP#{vpId}");
                return vpId;
            }
            return -1;
        }

        private static int TryReassignPlayerVoicePack(Actor actor)
        {
            if (actor == null) return -1;

            ActorSkin skin = actor.overrideActorSkin;
            if (skin == null) return -1;

            int team = actor.team;
            if (team < 0 || team > 1) return -1;

            var entries = SkinPool.GetVoiceEntries(team, skin);
            if (entries == null || entries.Count == 0) return -1;

            var candidates = new List<int>();

            if (entries.Exists(e => e.mutatorId == -2))
            {
                foreach (var vp in VoicePackScanner.DetectedPacks)
                {
                    if (vp == null) continue;
                    if (!IsVoicePackAllowedForTeam(vp.mutatorId, team)) continue;
                    if (!IsBotVoiceCapable(vp.mutatorId)) continue;
                    candidates.Add(vp.mutatorId);
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int id = entries[i].mutatorId;
                if (id < 0) continue;
                if (!IsVoicePackAllowedForTeam(id, team)) continue;
                if (!IsBotVoiceCapable(id)) continue;
                candidates.Add(id);
            }

            if (candidates.Count == 0)
            {
                foreach (var vp in VoicePackScanner.DetectedPacks)
                {
                    if (vp == null) continue;
                    if (!IsVoicePackAllowedForTeam(vp.mutatorId, team)) continue;
                    if (!IsBotVoiceCapable(vp.mutatorId)) continue;
                    candidates.Add(vp.mutatorId);
                }
            }

            if (candidates.Count == 0) return -1;

            int vpId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            VoicePackRouter.ActorVoicePack[actor] = vpId;
            VoicePackRouter.Log($"[PVBridge] REASSIGN {actor.name} skin={skin.name} team={team} -> VP#{vpId}");
            return vpId;
        }

        private static bool IsVoicePackAllowedForTeam(int mutatorId, int team)
        {
            int assignedTeam;
            if (!VoicePackActivator.VoicePackTeamAssignment.TryGetValue(mutatorId, out assignedTeam))
                return true;
            return assignedTeam == 2 || assignedTeam == team;
        }

        private static bool IsBotVoiceCapable(int mutatorId)
        {
            foreach (var vp in VoicePackScanner.DetectedPacks)
            {
                if (vp == null || vp.mutatorId != mutatorId) continue;
                if (vp.voicePackType == VoicePackScanner.VoicePackType.PlayerVoice) return true;
            }

            string[] requiredNames =
            {
                "DeathSoundBank", "DeathBank",
                "ReloadingSoundBank",
                "ThrowGrenadeBank", "ThrowGrenadeSoundBank",
                "SmokeGrenadeBank", "SmokeGrenadeSoundBank"
            };
            for (int i = 0; i < requiredNames.Length; i++)
            {
                SoundBank dummy;
                if (TryGetBank(vpId: mutatorId, bankName: requiredNames[i], bank: out dummy))
                    return true;
            }
            return false;
        }

        private static bool OnCooldown(Actor actor, bool bypassCooldown)
        {
            if (bypassCooldown) return false;
            float last;
            if (LastPlayTime.TryGetValue(actor, out last) && Time.time - last < COOLDOWN)
                return true;
            return false;
        }

        private static bool TryGetBank(int vpId, string bankName, out SoundBank bank)
        {
            bank = null;
            if (vpId < 0 || string.IsNullOrEmpty(bankName)) return false;

            string exact = $"{vpId}:{bankName}";
            if (VoicePackRouter.VoicePackBanks.TryGetValue(exact, out bank))
                return bank != null;

            string prefix = vpId + ":";
            foreach (var kv in VoicePackRouter.VoicePackBanks)
            {
                if (kv.Key == null || kv.Value == null) continue;
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string suffix = kv.Key.Substring(prefix.Length);
                if (string.Equals(suffix, bankName, StringComparison.OrdinalIgnoreCase))
                {
                    bank = kv.Value;
                    return true;
                }
            }
            return false;
        }

        internal static bool PlayBankForActor(Actor actor, int vpId, string bankName, bool bypassCooldown = false)
        {
            if (actor == null) return false;
            if (OnCooldown(actor, bypassCooldown)) return false;

            SoundBank bank;
            if (!TryGetBank(vpId, bankName, out bank))
            {
                VoicePackRouter.Log($"[PVBridge] MISS key='{vpId}:{bankName}' (VPBanks has {VoicePackRouter.VoicePackBanks.Count} entries)");
                return false;
            }
            if (bank == null || bank.clips == null || bank.clips.Length == 0) return false;

            int idx = UnityEngine.Random.Range(0, bank.clips.Length);
            PlayVoiceClip(bank.clips[idx], actor.transform.position);
            LastPlayTime[actor] = Time.time;
            VoicePackRouter.Log($"[PVBridge] PLAY {bankName} for {actor.name} VP#{vpId} clip#{idx}");
            return true;
        }

        private static void PlayVoiceClip(AudioClip clip, Vector3 position)
        {
            if (clip == null) return;
            var go = new GameObject("PVBridge_Voice");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip          = clip;
            float cfgBlend = ActorSkinSelectorPlugin.CfgVoiceSpatialBlend?.Value ?? 1f;
            float cfgMin   = ActorSkinSelectorPlugin.CfgVoiceMinDist?.Value ?? 12f;
            float cfgMax   = ActorSkinSelectorPlugin.CfgVoiceMaxDist?.Value ?? 140f;
            const float RANGE_SCALE  = 0.4f;
            const float VOLUME_SCALE = 0.7f;
            src.spatialBlend  = Mathf.Clamp(cfgBlend, 0.95f, 1f);
            src.minDistance   = Mathf.Clamp(cfgMin * RANGE_SCALE, 2f, 12f);
            src.maxDistance   = Mathf.Clamp(cfgMax * RANGE_SCALE, src.minDistance + 3f, 120f);
            src.rolloffMode   = AudioRolloffMode.Logarithmic;
            src.volume        = 0.65f * VOLUME_SCALE;
            src.Play();
            UnityEngine.Object.Destroy(go, clip.length + 0.5f);
        }

        internal static bool PlayBankForActorWithFallback(Actor actor, int vpId, string primary, string fallback)
        {
            if (PlayBankForActor(actor, vpId, primary)) return true;
            SoundBank bank;
            if (!TryGetBank(vpId, fallback, out bank)) return false;
            if (bank == null || bank.clips == null || bank.clips.Length == 0) return false;
            if (OnCooldown(actor, false)) return false;
            int idx = UnityEngine.Random.Range(0, bank.clips.Length);
            PlayVoiceClip(bank.clips[idx], actor.transform.position);
            LastPlayTime[actor] = Time.time;
            VoicePackRouter.Log($"[PVBridge] PLAY(fb) {fallback} for {actor.name} VP#{vpId} clip#{idx}");
            return true;
        }
    }

    [HarmonyPatch(typeof(Actor), "Kill")]
    internal static class Patch_PVBridge_ActorKill
    {
        [HarmonyPostfix]
        static void Postfix(Actor __instance, DamageInfo info)
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return;

                PlayerVoiceEventBridge.TryPlayDeathVoice(__instance);
                PlayerVoiceEventBridge.ClearActorState(__instance);

                Actor killer = info.sourceActor;
                if (killer != null && killer != __instance && killer.team != __instance.team && !killer.dead)
                {
                    int killerVp;
                    if (PlayerVoiceEventBridge.HasPVAssigned(killer, out killerVp))
                    {
                        int kills;
                        if (!PlayerVoiceEventBridge.KillCounts.TryGetValue(killer, out kills))
                            kills = 0;
                        kills++;
                        PlayerVoiceEventBridge.KillCounts[killer] = kills;

                        Actor lastDmgSrc;
                        bool isRevenge = PlayerVoiceEventBridge.LastDamageSource.TryGetValue(killer, out lastDmgSrc)
                                      && lastDmgSrc == __instance;

                        if (kills >= 4)
                        {
                            PlayerVoiceEventBridge.PlayBankForActor(killer, killerVp, "KillStreakSoundBank");
                            PlayerVoiceEventBridge.KillCounts[killer] = 0;
                        }
                        else if (isRevenge)
                        {
                            PlayerVoiceEventBridge.PlayBankForActor(killer, killerVp, "RevengeKillSoundBank");
                        }
                        else
                        {
                            PlayerVoiceEventBridge.PlayBankForActor(killer, killerVp, "OnKillSoundBank");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogWarning($"[PVBridge] Kill: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Actor), "Damage", new Type[] { typeof(DamageInfo) })]
    internal static class Patch_PVBridge_ActorDamage
    {
        [HarmonyPostfix]
        static void Postfix(Actor __instance, DamageInfo info)
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return;
                if (__instance.dead)
                {
                    PlayerVoiceEventBridge.TryPlayDeathVoice(__instance);
                    return;
                }

                int vpId;
                if (!PlayerVoiceEventBridge.HasPVAssigned(__instance, out vpId)) return;

                if (info.sourceActor != null && info.sourceActor.team != __instance.team)
                    PlayerVoiceEventBridge.LastDamageSource[__instance] = info.sourceActor;

                if (UnityEngine.Random.value > PlayerVoiceEventBridge.CHANCE_DAMAGE) return;

                float hp = __instance.health;
                float maxHp = __instance.maxHealth;
                float dmg = info.healthDamage;

                if (dmg >= 60f || hp < maxHp * 0.5f)
                    PlayerVoiceEventBridge.PlayBankForActorWithFallback(__instance, vpId,
                        "HeavyDamageSoundBank", "HeavyHurtSoundBank");
                else if (dmg >= 20f)
                    PlayerVoiceEventBridge.PlayBankForActorWithFallback(__instance, vpId,
                        "MediumDamageSoundBank", "MediumHurtSoundBank");
                else
                    PlayerVoiceEventBridge.PlayBankForActorWithFallback(__instance, vpId,
                        "LightDamageSoundBank", "LightHurtSoundBank");
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogWarning($"[PVBridge] Damage: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "OnWin")]
    internal static class Patch_PVBridge_MatchEnd
    {
        [HarmonyPostfix]
        static void Postfix(int winner)
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return;

                foreach (var kv in VoicePackRouter.ActorVoicePack)
                {
                    Actor actor = kv.Key;
                    if (actor == null || actor.dead) continue;

                    int vpId;
                    if (!PlayerVoiceEventBridge.HasPVAssigned(actor, out vpId)) continue;

                    if (actor.team == winner)
                        PlayerVoiceEventBridge.PlayBankForActor(actor, vpId, "VictorySoundBank");
                    else
                        PlayerVoiceEventBridge.PlayBankForActor(actor, vpId, "LoseMatchSoundBank");
                }
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogWarning($"[PVBridge] MatchEnd: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(CapturePoint), "SetOwner")]
    internal static class Patch_PVBridge_CapturePoint
    {
        [HarmonyPostfix]
        static void Postfix(CapturePoint __instance, int team, bool initialOwner)
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return;
                if (initialOwner) return;

                if (ActorManager.instance?.actors == null) return;

                foreach (var actor in ActorManager.instance.actors)
                {
                    if (actor == null || actor.dead) continue;
                    if (actor.GetCurrentCapturePoint() != __instance) continue;

                    int vpId;
                    if (!PlayerVoiceEventBridge.HasPVAssigned(actor, out vpId)) continue;

                    if (team >= 0)
                    {
                        if (actor.team == team)
                            PlayerVoiceEventBridge.PlayBankForActor(actor, vpId, "CaptureSoundBank");
                    }
                    else
                    {
                        PlayerVoiceEventBridge.PlayBankForActor(actor, vpId, "TakingPointBank");
                    }
                }
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogWarning($"[PVBridge] Capture: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Actor), "SpawnAt")]
    internal static class Patch_PVBridge_ActorSpawn
    {
        [HarmonyPostfix]
        static void Postfix(Actor __instance)
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return;

                int vpId;
                if (!PlayerVoiceEventBridge.HasPVAssigned(__instance, out vpId)) return;

                PlayerVoiceEventBridge.KillCounts[__instance] = 0;
                PlayerVoiceEventBridge.DeathVoicePlayed.Remove(__instance);

                if (!PlayerVoiceEventBridge.HasSpawned.Contains(__instance))
                {
                    PlayerVoiceEventBridge.HasSpawned.Add(__instance);

                    bool isLocalPlayer = false;
                    try { isLocalPlayer = (LocalPlayer.actor == __instance); } catch { }
                    if (isLocalPlayer)
                        PlayerVoiceEventBridge.PlayBankForActor(__instance, vpId, "OnFirstSpawnSoundBank");
                }
            }
            catch (Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogWarning($"[PVBridge] Spawn: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ActorManager), "Update")]
    internal static class Patch_PVBridge_Update
    {
        private static float _lastCheckTime;

        [HarmonyPostfix]
        static void Postfix()
        {
            try
            {
                if (!VoicePackScanner.ENABLED) return;
                if (Time.time - _lastCheckTime < 0.25f) return;
                _lastCheckTime = Time.time;

                if (ActorManager.instance == null) return;
                var actors = ActorManager.instance.actors;
                if (actors == null) return;

                for (int i = 0; i < actors.Count; i++)
                {
                    var actor = actors[i];
                    if (actor == null || actor.dead) continue;

                    int vpId;
                    if (!PlayerVoiceEventBridge.HasPVAssigned(actor, out vpId)) continue;

                    bool isReloading = actor.activeWeapon != null
                                    && actor.activeWeapon.reloading
                                    && !actor.IsSeated();
                    bool wasReloading;
                    PlayerVoiceEventBridge.WasReloading.TryGetValue(actor, out wasReloading);

                    if (isReloading && !wasReloading)
                    {
                        float lastRld;
                        PlayerVoiceEventBridge.LastReloadTime.TryGetValue(actor, out lastRld);
                        if (Time.time - lastRld > PlayerVoiceEventBridge.RELOAD_COOLDOWN
                            && UnityEngine.Random.value < PlayerVoiceEventBridge.CHANCE_RELOAD)
                        {
                            if (PlayerVoiceEventBridge.PlayBankForActor(actor, vpId, "ReloadingSoundBank"))
                                PlayerVoiceEventBridge.LastReloadTime[actor] = Time.time;
                        }
                    }

                    PlayerVoiceEventBridge.WasReloading[actor] = isReloading;

                    bool isFallingNow = actor.fallenOver
                                     && actor.Velocity().y <= -20f
                                     && !actor.parachuteDeployed;

                    bool wasFalling = PlayerVoiceEventBridge.IsFalling.Contains(actor);

                    if (isFallingNow && !wasFalling)
                    {
                        PlayerVoiceEventBridge.IsFalling.Add(actor);
                        PlayerVoiceEventBridge.PlayBankForActor(actor, vpId, "FallingSoundBank");
                    }
                    else if (!isFallingNow && wasFalling)
                    {
                        PlayerVoiceEventBridge.IsFalling.Remove(actor);
                    }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GrenadeProjectile), "StartTravelling")]
    internal static class Patch_PVBridge_GrenadeThrow
    {
        [HarmonyPostfix]
        static void Postfix(GrenadeProjectile __instance)
        {
            try
            {
                if (!VoicePackScanner.ENABLED)
                {
                    VoicePackRouter.Log("[PVBridge:Nade] SKIP - ENABLED=false");
                    return;
                }

                Actor thrower = __instance.killCredit;
                if (thrower == null)
                {
                    VoicePackRouter.Log("[PVBridge:Nade] SKIP - killCredit null");
                    return;
                }

                ActorSkin skin = thrower.overrideActorSkin;
                VoicePackRouter.Log($"[PVBridge:Nade] thrower={thrower.name} skin={(skin?.name ?? "null")} team={thrower.team}");

                int vpId;
                bool hasPV = PlayerVoiceEventBridge.HasPVAssigned(thrower, out vpId);
                VoicePackRouter.Log($"[PVBridge:Nade] HasPVAssigned={hasPV} vpId={vpId}");
                if (!hasPV) return;

                if (UnityEngine.Random.value >= PlayerVoiceEventBridge.CHANCE_GRENADE) return;

                bool played = PlayerVoiceEventBridge.PlayBankForActor(thrower, vpId, "ThrowGrenadeBank");
                VoicePackRouter.Log($"[PVBridge:Nade] ThrowGrenadeBank played={played}");
                if (!played)
                    played = PlayerVoiceEventBridge.PlayBankForActor(thrower, vpId, "ThrowGrenadeSoundBank");

                if (!played)
                    played = PlayerVoiceEventBridge.PlayBankForActor(thrower, vpId, "SmokeGrenadeBank");

                if (!played)
                    played = PlayerVoiceEventBridge.PlayBankForActor(thrower, vpId, "SmokeGrenadeSoundBank");

                if (!played)
                    VoicePackRouter.Log($"[PVBridge:Nade] No grenade bank found for VP#{vpId}");
            }
            catch (Exception ex)
            {
                VoicePackRouter.Log($"[PVBridge:Nade] EXCEPTION: {ex.Message}");
            }
        }
    }
}