//Author: Jellobeans
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ActorSkinSelector
{
    public enum SkinRarityTier
    {
        Standard  = 0,
        Common    = 1,
        Uncommon  = 2,
        Rare      = 3,
        Epic      = 4,
    }

    public static class SkinRarityInfo
    {
        public static readonly string[] Names   = { "Standard", "Common", "Uncommon", "Rare", "Epic" };
        public static readonly int[]    Weights = { 50, 25, 15, 9, 1 };
        public static readonly Color[]  Colors  =
        {
            Color.white,
            new Color(0.30f, 0.85f, 0.30f, 1f),
            new Color(0.35f, 0.55f, 1.00f, 1f),
            new Color(1.00f, 0.70f, 0.15f, 1f),
            new Color(0.70f, 0.30f, 0.90f, 1f),
        };
        public const int TierCount = 5;
    }

    [Flags]
    public enum SkinRole
    {
        None           = 0,
        GroundDriver   = 1 << 0,
        GroundGunner   = 1 << 1,
        GroundPassenger= 1 << 2,
        HeliPilot      = 1 << 3,
        HeliGunner     = 1 << 4,
        HeliPassenger  = 1 << 5,
        PlanePilot     = 1 << 6,
        PlaneGunner    = 1 << 7,
        PlanePassenger = 1 << 8,
        BoatDriver     = 1 << 9,
        BoatGunner     = 1 << 10,
        BoatPassenger  = 1 << 11,
    }

    public static class SkinRoleInfo
    {
        public static readonly SkinRole[] AllRoles =
        {
            SkinRole.GroundDriver, SkinRole.GroundGunner, SkinRole.GroundPassenger,
            SkinRole.HeliPilot,    SkinRole.HeliGunner,   SkinRole.HeliPassenger,
            SkinRole.PlanePilot,   SkinRole.PlaneGunner,  SkinRole.PlanePassenger,
            SkinRole.BoatDriver,   SkinRole.BoatGunner,   SkinRole.BoatPassenger,
        };
        public static readonly string[] Names =
        {
            "Driver", "Gunner", "Passenger",
            "Pilot",  "Gunner", "Passenger",
            "Pilot",  "Gunner", "Passenger",
            "Driver", "Gunner", "Passenger",
        };
        public static readonly string[] GroupHeaders =
        {
            "Ground Vehicles", "Helicopter", "Planes", "Boats"
        };
        public static int GroupOf(int roleIndex) => roleIndex / 3;
        public const int RoleCount = 12;
    }

    public struct VoiceEntry
    {
        public int           mutatorId;
        public SkinRarityTier rarity;

        public VoiceEntry(int mutatorId, SkinRarityTier rarity = SkinRarityTier.Standard)
        {
            this.mutatorId = mutatorId;
            this.rarity    = rarity;
        }
    }

    public struct SkinData
    {
        public SkinRarityTier    rarity;
        public SkinRole          roles;
        public bool              vehicleOnly;
        public List<VoiceEntry>  voiceEntries;

        public int voicePackMutatorId
        {
            get
            {
                if (voiceEntries != null && voiceEntries.Count > 0)
                    return voiceEntries[0].mutatorId;
                return -1;
            }
            set
            {
                if (voiceEntries == null) voiceEntries = new List<VoiceEntry>();
                voiceEntries.Clear();
                if (value != -1)
                    voiceEntries.Add(new VoiceEntry(value, SkinRarityTier.Standard));
            }
        }

        public SkinData(SkinRarityTier rarity, SkinRole roles = SkinRole.None,
                        bool vehicleOnly = false, int voicePackMutatorId = -1)
        {
            this.rarity       = rarity;
            this.roles        = roles;
            this.vehicleOnly  = vehicleOnly;
            this.voiceEntries = new List<VoiceEntry>();
            if (voicePackMutatorId != -1)
                this.voiceEntries.Add(new VoiceEntry(voicePackMutatorId, SkinRarityTier.Standard));
        }

        public SkinData(SkinRarityTier rarity, SkinRole roles, bool vehicleOnly,
                        List<VoiceEntry> voiceEntries)
        {
            this.rarity       = rarity;
            this.roles        = roles;
            this.vehicleOnly  = vehicleOnly;
            this.voiceEntries = voiceEntries ?? new List<VoiceEntry>();
        }
    }

    public static class SkinPool
    {
        public static readonly Dictionary<ActorSkin, SkinData>[] Selected =
            new Dictionary<ActorSkin, SkinData>[2];

        public static bool DontRandomizePlayer       = false;
        public static bool PreserveSkinAfterExit      = false;
        public static bool VehicleSkinsAffectPlayer   = true;
        public static bool KeepVehicleSkinOnDeath     = false;

        private static System.Random _rng = new System.Random();

        public static void Init()
        {
            if (Selected[0] == null) Selected[0] = new Dictionary<ActorSkin, SkinData>();
            if (Selected[1] == null) Selected[1] = new Dictionary<ActorSkin, SkinData>();
        }

        public static bool HasAny(int team)
        {
            Init();
            return Selected[team] != null && Selected[team].Count > 0;
        }

        public static ActorSkin GetRandom(int team)
        {
            Init();
            var pool = Selected[team];
            if (pool == null || pool.Count == 0) return null;

            var infantry = new Dictionary<ActorSkin, SkinData>();
            foreach (var kv in pool)
                if (!kv.Value.vehicleOnly)
                    infantry[kv.Key] = kv.Value;

            if (infantry.Count == 0) return null;
            return WeightedPick(infantry);
        }

        public static ActorSkin GetRandomForRole(int team, SkinRole role)
        {
            Init();
            var pool = Selected[team];
            if (pool == null || pool.Count == 0) return null;

            var filtered = new Dictionary<ActorSkin, SkinData>();
            foreach (var kv in pool)
                if ((kv.Value.roles & role) != 0)
                    filtered[kv.Key] = kv.Value;

            if (filtered.Count == 0) return null;
            return WeightedPick(filtered);
        }

        // Roll rarity tier first, then pick one skin from that tier.
        private static ActorSkin WeightedPick(Dictionary<ActorSkin, SkinData> pool)
        {
            var tierSkins = new Dictionary<SkinRarityTier, List<ActorSkin>>();
            foreach (var kv in pool)
            {
                if (!tierSkins.ContainsKey(kv.Value.rarity))
                    tierSkins[kv.Value.rarity] = new List<ActorSkin>();
                tierSkins[kv.Value.rarity].Add(kv.Key);
            }

            float totalBaseWeight = 0f;
            foreach (var tier in tierSkins.Keys)
                totalBaseWeight += SkinRarityInfo.Weights[(int)tier];

            if (totalBaseWeight <= 0f)
            {
                int idx = _rng.Next(pool.Count);
                int i = 0;
                foreach (var kv in pool) { if (i++ == idx) return kv.Key; }
                return null;
            }

            float roll = (float)_rng.NextDouble() * totalBaseWeight;
            SkinRarityTier chosenTier = SkinRarityTier.Standard;
            float acc = 0f;
            foreach (var kv in tierSkins)
            {
                acc += SkinRarityInfo.Weights[(int)kv.Key];
                if (roll < acc) { chosenTier = kv.Key; break; }
                chosenTier = kv.Key;
            }

            var candidates = tierSkins[chosenTier];
            return candidates[_rng.Next(candidates.Count)];
        }


        public static void Toggle(int team, ActorSkin skin)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
                Selected[team].Remove(skin);
            else
                Selected[team][skin] = new SkinData(SkinRarityTier.Standard);
        }

        public static void ToggleVehicleOnly(int team, ActorSkin skin)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
            {
                var d = Selected[team][skin];
                d.vehicleOnly = !d.vehicleOnly;
                Selected[team][skin] = d;
            }
        }

        public static bool GetVehicleOnly(int team, ActorSkin skin)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
                return Selected[team][skin].vehicleOnly;
            return false;
        }

        public static void SetRarity(int team, ActorSkin skin, SkinRarityTier tier)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
            {
                var d = Selected[team][skin];
                d.rarity = tier;
                Selected[team][skin] = d;
            }
        }

        public static SkinRarityTier GetRarity(int team, ActorSkin skin)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
                return Selected[team][skin].rarity;
            return SkinRarityTier.Standard;
        }

        public static void SetRoles(int team, ActorSkin skin, SkinRole roles)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
            {
                var d = Selected[team][skin];
                d.roles = roles;
                Selected[team][skin] = d;
            }
        }

        public static SkinRole GetRoles(int team, ActorSkin skin)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
                return Selected[team][skin].roles;
            return SkinRole.None;
        }

        public static void ToggleRole(int team, ActorSkin skin, SkinRole role)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
            {
                var d = Selected[team][skin];
                d.roles ^= role;
                Selected[team][skin] = d;
            }
        }


        public static void SetVoicePack(int team, ActorSkin skin, int mutatorId)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
            {
                var d = Selected[team][skin];
                d.voicePackMutatorId = mutatorId;
                Selected[team][skin] = d;
            }
        }

        public static int GetVoicePack(int team, ActorSkin skin)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
                return Selected[team][skin].voicePackMutatorId;
            return -1;
        }


        public static List<VoiceEntry> GetVoiceEntries(int team, ActorSkin skin)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
            {
                var entries = Selected[team][skin].voiceEntries;
                return entries != null ? new List<VoiceEntry>(entries) : new List<VoiceEntry>();
            }
            return new List<VoiceEntry>();
        }

        public static void SetVoiceEntries(int team, ActorSkin skin, List<VoiceEntry> entries)
        {
            Init();
            if (Selected[team].ContainsKey(skin))
            {
                var d = Selected[team][skin];
                d.voiceEntries = entries ?? new List<VoiceEntry>();
                Selected[team][skin] = d;
            }
        }

        public static void ToggleVoiceEntry(int team, ActorSkin skin, int mutatorId)
        {
            Init();
            if (!Selected[team].ContainsKey(skin)) return;
            var d = Selected[team][skin];
            if (d.voiceEntries == null) d.voiceEntries = new List<VoiceEntry>();

            int idx = d.voiceEntries.FindIndex(e => e.mutatorId == mutatorId);
            if (idx >= 0)
                d.voiceEntries.RemoveAt(idx);
            else
                d.voiceEntries.Add(new VoiceEntry(mutatorId, SkinRarityTier.Standard));

            Selected[team][skin] = d;
        }

        public static void SetVoiceEntryRarity(int team, ActorSkin skin, int mutatorId, SkinRarityTier rarity)
        {
            Init();
            if (!Selected[team].ContainsKey(skin)) return;
            var d = Selected[team][skin];
            if (d.voiceEntries == null) return;

            for (int i = 0; i < d.voiceEntries.Count; i++)
            {
                if (d.voiceEntries[i].mutatorId == mutatorId)
                {
                    d.voiceEntries[i] = new VoiceEntry(mutatorId, rarity);
                    break;
                }
            }
            Selected[team][skin] = d;
        }

        public static bool HasVoiceEntry(int team, ActorSkin skin, int mutatorId)
        {
            Init();
            if (!Selected[team].ContainsKey(skin)) return false;
            var entries = Selected[team][skin].voiceEntries;
            if (entries == null) return false;
            return entries.Exists(e => e.mutatorId == mutatorId);
        }

        public static SkinRarityTier GetVoiceEntryRarity(int team, ActorSkin skin, int mutatorId)
        {
            Init();
            if (!Selected[team].ContainsKey(skin)) return SkinRarityTier.Standard;
            var entries = Selected[team][skin].voiceEntries;
            if (entries == null) return SkinRarityTier.Standard;
            var found = entries.Find(e => e.mutatorId == mutatorId);
            return found.mutatorId == mutatorId ? found.rarity : SkinRarityTier.Standard;
        }

        // Filter by team first, then do the weighted voice roll.
        public static int GetWeightedRandomVoicePack(int team, ActorSkin skin)
        {
            Init();
            if (!Selected[team].ContainsKey(skin)) return -1;
            var entries = Selected[team][skin].voiceEntries;
            if (entries == null || entries.Count == 0) return -1;

            var filtered = new List<VoiceEntry>();

            if (entries.Exists(e => e.mutatorId == -2))
            {
                for (int i = 0; i < VoicePackScanner.DetectedPacks.Count; i++)
                {
                    int vpId = VoicePackScanner.DetectedPacks[i].mutatorId;
                    if (IsVoicePackAllowedForTeam(vpId, team))
                        filtered.Add(new VoiceEntry(vpId, SkinRarityTier.Standard));
                }
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.mutatorId < 0) continue;
                if (!IsVoicePackAllowedForTeam(e.mutatorId, team)) continue;
                filtered.Add(e);
            }

            var pvFiltered = new List<VoiceEntry>();
            for (int i = 0; i < filtered.Count; i++)
            {
                var e = filtered[i];
                if (IsPlayerVoicePack(e.mutatorId))
                    pvFiltered.Add(e);
            }
            if (pvFiltered.Count > 0)
                filtered = pvFiltered;

            if (filtered.Count == 0) return -1;
            if (filtered.Count == 1) return filtered[0].mutatorId;

            var tierEntries = new Dictionary<SkinRarityTier, List<int>>();
            foreach (var e in filtered)
            {
                if (!tierEntries.ContainsKey(e.rarity))
                    tierEntries[e.rarity] = new List<int>();
                tierEntries[e.rarity].Add(e.mutatorId);
            }

            float totalWeight = 0f;
            foreach (var tier in tierEntries.Keys)
                totalWeight += SkinRarityInfo.Weights[(int)tier];

            if (totalWeight <= 0f)
                return filtered[_rng.Next(filtered.Count)].mutatorId;

            float roll = (float)_rng.NextDouble() * totalWeight;
            SkinRarityTier chosenTier = SkinRarityTier.Standard;
            float acc = 0f;
            foreach (var kv in tierEntries)
            {
                acc += SkinRarityInfo.Weights[(int)kv.Key];
                if (roll < acc) { chosenTier = kv.Key; break; }
                chosenTier = kv.Key;
            }

            var candidates = tierEntries[chosenTier];
            return candidates[_rng.Next(candidates.Count)];
        }

        private static bool IsVoicePackAllowedForTeam(int mutatorId, int team)
        {
            if (team < 0 || team > 1) return false;

            int assignedTeam;
            if (!VoicePackActivator.VoicePackTeamAssignment.TryGetValue(mutatorId, out assignedTeam))
                return true;

            return assignedTeam == 2 || assignedTeam == team;
        }

        // TODO: cache this lookup if we ever track lots of packs.
        private static bool IsPlayerVoicePack(int mutatorId)
        {
            for (int i = 0; i < VoicePackScanner.DetectedPacks.Count; i++)
            {
                var vp = VoicePackScanner.DetectedPacks[i];
                if (vp.mutatorId == mutatorId)
                    return vp.voicePackType == VoicePackScanner.VoicePackType.PlayerVoice;
            }
            return false;
        }

        public static bool IsSelected(int team, ActorSkin skin)
        {
            Init();
            return Selected[team] != null && Selected[team].ContainsKey(skin);
        }

        public static void Clear(int team) { Init(); Selected[team].Clear(); }
        public static void ClearAll() { Clear(0); Clear(1); }

        public static List<ActorSkin> GetSelected(int team)
        {
            Init();
            return new List<ActorSkin>(Selected[team].Keys);
        }
    }
}
