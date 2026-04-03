//Author: Jellobeans
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ActorSkinSelector //Track actor roles & apply skins on triggers
{
    internal static class ActorRoleTracker
    {
        private static readonly Dictionary<Actor, SkinRole> _roles =
            new Dictionary<Actor, SkinRole>();

        internal static void Set(Actor actor, SkinRole role)
        {
            if (actor == null) return;
            _roles[actor] = role;
        }

        internal static void Clear(Actor actor)
        {
            if (actor != null) _roles.Remove(actor);
        }

        internal static bool TryGet(Actor actor, out SkinRole role)
        {
            role = SkinRole.None;
            return actor != null && _roles.TryGetValue(actor, out role);
        }
    }


    [HarmonyPatch(typeof(ActorManager), "Register")]
    internal static class Patch_ActorManager_Register
    {
        [HarmonyPostfix]
        static void Postfix(Actor actor)
        {
            try
            {
                if (actor == null) return;
                int team = actor.team;
                if (team < 0 || team > 1) return;
                if (!SkinPool.HasAny(team)) return;
                SkinHookHelper.ApplySkin(actor, team);
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(Actor), "ResetModelSkin")]
    internal static class Patch_Actor_ResetModelSkin
    {
        internal static readonly HashSet<Actor> Applying = new HashSet<Actor>();

        [HarmonyPostfix]
        static void Postfix(Actor __instance)
        {
            try
            {
                if (__instance == null) return;
                if (Applying.Contains(__instance)) return;
                int team = __instance.team;
                if (team < 0 || team > 1) return;
                if (!SkinPool.HasAny(team)) return;
                SkinHookHelper.ApplySkin(__instance, team);
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(Actor), "Kill")]
    internal static class Patch_Actor_Kill_ClearRole
    {
        [HarmonyPrefix]
        static void Prefix(Actor __instance)
        {
            try
            {
                if (!SkinPool.KeepVehicleSkinOnDeath)
                    ActorRoleTracker.Clear(__instance);
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(Seat), "SetOccupant")]
    internal static class Patch_Seat_SetOccupant
    {
        [HarmonyPostfix]
        static void Postfix(Seat __instance, Actor actor)
        {
            try
            {
                if (actor == null || __instance == null) return;
                int team = actor.team;
                if (team < 0 || team > 1) return;
                if (!SkinPool.HasAny(team)) return;

                bool isPlayer = false;
                try { isPlayer = (LocalPlayer.actor == actor); } catch { }
                if (isPlayer && !SkinPool.VehicleSkinsAffectPlayer) return;

                SkinRole role = SkinHookHelper.DetectRole(__instance);
                if (role == SkinRole.None)
                {
                    ActorRoleTracker.Clear(actor);
                    return;
                }

                ActorRoleTracker.Set(actor, role);

                ActorSkin skin = SkinPool.GetRandomForRole(team, role);
                if (skin == null) return;

                SkinHookHelper.ApplySkinGuarded(actor, skin);

                if (VoicePackScanner.ENABLED)
                {
                    int vpId = SkinPool.GetWeightedRandomVoicePack(team, skin);
                    VoicePackRouter.AssignActorVoicePack(actor, vpId);
                }
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(Seat), "OccupantLeft")]
    internal static class Patch_Seat_OccupantLeft
    {
        [HarmonyPrefix]
        static void Prefix(Seat __instance, ref Actor __state)
        {
            try { __state = __instance?.occupant; } catch { __state = null; }
        }

        [HarmonyPostfix]
        static void Postfix(Actor __state)
        {
            try
            {
                if (__state == null) return;
                Actor actor = __state;

                ActorRoleTracker.Clear(actor);

                if (SkinPool.PreserveSkinAfterExit) return;

                int team = actor.team;
                if (team < 0 || team > 1) return;
                if (!SkinPool.HasAny(team)) return;

                bool isPlayer = false;
                try { isPlayer = (LocalPlayer.actor == actor); } catch { }
                if (isPlayer && !SkinPool.VehicleSkinsAffectPlayer) return;

                ActorSkin skin = SkinPool.GetRandom(team);
                if (skin != null)
                {
                    SkinHookHelper.ApplySkinGuarded(actor, skin);

                    if (VoicePackScanner.ENABLED)
                    {
                        int vpId = SkinPool.GetWeightedRandomVoicePack(team, skin);
                        VoicePackRouter.AssignActorVoicePack(actor, vpId);
                    }
                }
            }
            catch { }
        }
    }


    internal static class SkinHookHelper
    {
        internal static void ApplySkin(Actor actor, int team)
        {
            ActorSkin skin;
            if (ActorRoleTracker.TryGet(actor, out SkinRole currentRole) && currentRole != SkinRole.None)
            {
                skin = SkinPool.GetRandomForRole(team, currentRole);
                if (skin == null) skin = SkinPool.GetRandom(team);
            }
            else
            {
                skin = SkinPool.GetRandom(team);
            }

            if (skin == null) return;
            ApplySkinGuarded(actor, skin);

            if (VoicePackScanner.ENABLED)
            {
                int vpId = SkinPool.GetWeightedRandomVoicePack(team, skin);
                VoicePackRouter.AssignActorVoicePack(actor, vpId);
                VoicePackRouter.Log($"[SkinHook] ApplySkin {actor.name} skin={skin.name} team={team} role={currentRole} → VP#{vpId}");
            }
        }

        internal static void ApplySkinGuarded(Actor actor, ActorSkin skin)
        {
            Patch_Actor_ResetModelSkin.Applying.Add(actor);
            try   { actor.SetModelSkin(skin); }
            catch (System.Exception ex)
            {
                ActorSkinSelectorPlugin.Log?.LogWarning(
                    $"[JellosMultiskin] SetModelSkin failed: {ex.Message}");
            }
            finally { Patch_Actor_ResetModelSkin.Applying.Remove(actor); }
        }

        internal static SkinRole DetectRole(Seat seat)
        {
            if (seat == null || seat.vehicle == null) return SkinRole.None;

            Vehicle v = seat.vehicle;

            if (v.isTurret) return SkinRole.None;

            bool isDriver = seat.IsDriverSeat();
            bool hasGuns  = !isDriver && seat.HasAnyMountedWeapons();

            if (v is Helicopter)
                return isDriver ? SkinRole.HeliPilot
                     : hasGuns  ? SkinRole.HeliGunner
                                : SkinRole.HeliPassenger;

            if (v is Airplane || v is Plane)
                return isDriver ? SkinRole.PlanePilot
                     : hasGuns  ? SkinRole.PlaneGunner
                                : SkinRole.PlanePassenger;

            if (v is AnimationDrivenVehicle adv)
            {
                if (adv.planeInput)
                    return isDriver ? SkinRole.PlanePilot
                         : hasGuns  ? SkinRole.PlaneGunner
                                    : SkinRole.PlanePassenger;
                else
                    return isDriver ? SkinRole.HeliPilot
                         : hasGuns  ? SkinRole.HeliGunner
                                    : SkinRole.HeliPassenger;
            }

            if (v is Boat)
                return isDriver ? SkinRole.BoatDriver
                     : hasGuns  ? SkinRole.BoatGunner
                                : SkinRole.BoatPassenger;

            return isDriver ? SkinRole.GroundDriver
                 : hasGuns  ? SkinRole.GroundGunner
                            : SkinRole.GroundPassenger;
        }
    }


    internal class PlayerSkinWatcher : MonoBehaviour
    {
        private bool  _wasDead   = true;
        private Actor _lastActor = null;

        private void Update()
        {
            try
            {
                if (SkinPool.DontRandomizePlayer) return;

                Actor actor = null;
                try { actor = LocalPlayer.actor; } catch { return; }
                if (actor == null) return;

                int team = actor.team;
                if (team < 0 || team > 1) { _lastActor = actor; return; }
                if (!SkinPool.HasAny(team)) { _lastActor = actor; _wasDead = actor.dead; return; }

                bool isDead       = actor.dead;
                bool actorChanged = (actor != _lastActor);
                bool respawned    = (_wasDead && !isDead);

                if ((actorChanged || respawned) && !isDead)
                    StartCoroutine(ApplySkinNextFrame(actor, team));

                _wasDead  = isDead;
                _lastActor = actor;
            }
            catch { }
        }

        private static IEnumerator ApplySkinNextFrame(Actor actor, int team)
        {
            yield return null;
            try
            {
                if (actor == null || actor.dead) yield break;
                SkinHookHelper.ApplySkin(actor, team);
            }
            catch { }
        }
    }
}