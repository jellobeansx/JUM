//Author: Jellobeans
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace ActorSkinSelector
{

    [Serializable]
    public class SkinConfigData
    {
        public string   configName               = "Default";
        public bool     dontRandomizePlayer      = false;
        public bool     preserveSkinAfterExit    = false;
        public bool     vehicleSkinsAffectPlayer = true;
        public bool     keepVehicleSkinOnDeath   = false;
        public string[] eagle                   = new string[0];
        public string[] eagleRarity             = new string[0];
        public string[] eagleRoles              = new string[0];
        public string[] eagleVehicleOnly        = new string[0];
        public string[] eagleVoicePack          = new string[0];
        public string[] raven                   = new string[0];
        public string[] ravenRarity             = new string[0];
        public string[] ravenRoles              = new string[0];
        public string[] ravenVehicleOnly        = new string[0];
        public string[] ravenVoicePack          = new string[0];
    }


    public static class SkinConfigManager
    {
        public static string ConfigDir
        {
            get
            {
                string dir = Path.Combine(Paths.ConfigPath, "actorskinselector");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string FilePath(string name) =>
            Path.Combine(ConfigDir, Sanitize(name) + ".json");


        public static string[] GetSavedConfigNames()
        {
            try
            {
                var files = Directory.GetFiles(ConfigDir, "*.json");
                var names = new List<string>();
                foreach (var f in files)
                    names.Add(Path.GetFileNameWithoutExtension(f));
                names.Sort(StringComparer.OrdinalIgnoreCase);
                return names.ToArray();
            }
            catch { return new string[0]; }
        }

        // Grab in-memory selections & write to file
        public static bool Save(string name, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(name)) { error = "Config name cannot be empty."; return false; }
            try
            {
                SkinPool.Init();
                var data = new SkinConfigData
                {
                    configName               = name,
                    dontRandomizePlayer      = SkinPool.DontRandomizePlayer,
                    preserveSkinAfterExit    = SkinPool.PreserveSkinAfterExit,
                    vehicleSkinsAffectPlayer = SkinPool.VehicleSkinsAffectPlayer,
                    keepVehicleSkinOnDeath   = SkinPool.KeepVehicleSkinOnDeath,
                };
                BuildParallelArrays(0, out data.eagle, out data.eagleRarity, out data.eagleRoles, out data.eagleVehicleOnly, out data.eagleVoicePack);
                BuildParallelArrays(1, out data.raven, out data.ravenRarity, out data.ravenRoles, out data.ravenVehicleOnly, out data.ravenVoicePack);
                File.WriteAllText(FilePath(name), JsonUtility.ToJson(data, prettyPrint: true));
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        // TODO: show skipped skin names in the UI
        public static int Load(string name, out string message)
        {
            message = "";
            try
            {
                string path = FilePath(name);
                if (!File.Exists(path)) { message = "Config file not found."; return 0; }

                string json = File.ReadAllText(path);

                SkinPool.Init();
                SkinPool.ClearAll();

                var data = JsonUtility.FromJson<SkinConfigData>(json);
                if (data == null) { message = "Could not parse config."; return 0; }

                SkinPool.DontRandomizePlayer      = data.dontRandomizePlayer;
                SkinPool.PreserveSkinAfterExit     = data.preserveSkinAfterExit;
                SkinPool.VehicleSkinsAffectPlayer  = data.vehicleSkinsAffectPlayer;
                SkinPool.KeepVehicleSkinOnDeath    = data.keepVehicleSkinOnDeath;

                int skipped  = ApplyParallel(0, data.eagle, data.eagleRarity, data.eagleRoles, data.eagleVehicleOnly, data.eagleVoicePack);
                    skipped += ApplyParallel(1, data.raven, data.ravenRarity, data.ravenRoles, data.ravenVehicleOnly, data.ravenVoicePack);
                message = skipped > 0
                    ? $"Loaded - {skipped} missing skin(s) skipped."
                    : "Loaded OK.";
                return skipped;
            }
            catch (Exception ex) { message = "Load error: " + ex.Message; return 0; }
        }

        public static bool Delete(string name)
        {
            try { File.Delete(FilePath(name)); return true; }
            catch { return false; }
        }


        private static void BuildParallelArrays(int team,
            out string[] names, out string[] rarities, out string[] roles,
            out string[] vehOnly, out string[] voicePacks)
        {
            SkinPool.Init();
            var pool = SkinPool.Selected[team];
            if (pool == null || pool.Count == 0)
            {
                names = new string[0]; rarities = new string[0]; roles = new string[0];
                vehOnly = new string[0]; voicePacks = new string[0];
                return;
            }
            var nList = new List<string>();
            var rList = new List<string>();
            var oList = new List<string>();
            var vList = new List<string>();
            var vpList = new List<string>();
            foreach (var kv in pool)
            {
                nList.Add(kv.Key?.name ?? "");
                rList.Add(kv.Value.rarity.ToString());
                oList.Add(((int)kv.Value.roles).ToString());
                vList.Add(kv.Value.vehicleOnly ? "1" : "0");

                var entries = kv.Value.voiceEntries;
                if (entries == null || entries.Count == 0)
                {
                    vpList.Add("-1");
                }
                else
                {
                    var parts = new List<string>();
                    foreach (var ve in entries)
                        parts.Add($"{ve.mutatorId}:{ve.rarity}");
                    vpList.Add(string.Join(";", parts.ToArray()));
                }
            }
            //Convert to arrays
            names      = nList.ToArray();
            rarities   = rList.ToArray();
            roles      = oList.ToArray();
            vehOnly    = vList.ToArray();
            voicePacks = vpList.ToArray();
        }

        // Backwards compatibility with older save versions
        private static int ApplyParallel(int team,
            string[] names, string[] rarities, string[] roles,
            string[] vehOnly, string[] voicePacks)
        {
            if (names == null || names.Length == 0) return 0;
            int skipped = 0;
            for (int i = 0; i < names.Length; i++)
            {
                var skin = FindByName(names[i]);
                if (skin == null) { skipped++; continue; }

                SkinRarityTier tier = (rarities != null && i < rarities.Length)
                    ? ParseTier(rarities[i])
                    : SkinRarityTier.Standard;

                SkinRole role = SkinRole.None;
                if (roles != null && i < roles.Length)
                {
                    if (int.TryParse(roles[i], out int rv))
                        role = (SkinRole)rv;
                }

                bool vo = false;
                if (vehOnly != null && i < vehOnly.Length)
                    vo = (vehOnly[i] == "1");

                var veList = new List<VoiceEntry>();
                if (voicePacks != null && i < voicePacks.Length)
                {
                    string vpStr = voicePacks[i];
                    if (!string.IsNullOrEmpty(vpStr) && vpStr != "-1")
                    {
                        if (vpStr.Contains(":"))
                        {
                            string[] entries = vpStr.Split(';');
                            foreach (string entry in entries)
                            {
                                string[] parts = entry.Split(':');
                                if (parts.Length >= 2 && int.TryParse(parts[0], out int eid))
                                {
                                    SkinRarityTier eRarity = ParseTier(parts[1]);
                                    veList.Add(new VoiceEntry(eid, eRarity));
                                }
                                else if (parts.Length == 1 && int.TryParse(parts[0], out int eid2))
                                {
                                    veList.Add(new VoiceEntry(eid2, SkinRarityTier.Standard));
                                }
                            }
                        }
                        else
                        {
                            if (int.TryParse(vpStr, out int vpId) && vpId != -1)
                                veList.Add(new VoiceEntry(vpId, SkinRarityTier.Standard));
                        }
                    }
                }

                SkinPool.Selected[team][skin] = new SkinData(tier, role, vo, veList);
            }
            return skipped;
        }


        //TODO: Optimize with Array.Sort
        private static SkinRarityTier ParseTier(string s)
        {
            if (string.IsNullOrEmpty(s)) return SkinRarityTier.Standard;
            for (int i = 0; i < SkinRarityInfo.TierCount; i++)
                if (string.Equals(SkinRarityInfo.Names[i], s, StringComparison.OrdinalIgnoreCase))
                    return (SkinRarityTier)i;
            return SkinRarityTier.Standard;
        }

        private static ActorSkin FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var def = ActorManager.instance?.defaultActorSkin;
            if (def != null && def.name == name) return def;
            if (ModManager.instance?.actorSkins != null)
                foreach (var s in ModManager.instance.actorSkins)
                    if (s != null && s.name == name) return s;
            return null;
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
