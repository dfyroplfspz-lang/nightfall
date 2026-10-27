// C# / Assets/Scripts/Util/SaveData.cs / Unity 2022.3+ / records and inventory on disk
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nightfall
{
    [Serializable]
    public class RecordEntry
    {
        public string key;
        public int score;
        public int wave;
        public int kills;
    }

    [Serializable]
    public class CaseEntry
    {
        public string key;
        public int count;
    }

    [Serializable]
    public class SaveData
    {
        public int bestScore;
        public int bestWave;
        public int bestKills;
        public List<RecordEntry> modes = new List<RecordEntry>();
        public List<RecordEntry> maps = new List<RecordEntry>();

        public List<string> owned = new List<string>();
        public List<CaseEntry> cases = new List<CaseEntry>();
        public int scrap;
        public string playerSkin = Skins.DefaultPlayer;
        public string weaponSkin = Skins.DefaultWeapon;

        // ------------------------------------------------------------- disk
        public static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, S.SAVE_FILE); }
        }

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    string json = File.ReadAllText(Path);
                    SaveData d = JsonUtility.FromJson<SaveData>(json);
                    if (d != null)
                    {
                        if (d.modes == null) d.modes = new List<RecordEntry>();
                        if (d.maps == null) d.maps = new List<RecordEntry>();
                        if (d.owned == null) d.owned = new List<string>();
                        if (d.cases == null) d.cases = new List<CaseEntry>();
                        return d;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("save load failed: " + e.Message);
            }
            return new SaveData();
        }

        public void Store()
        {
            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("save write failed: " + e.Message);
            }
        }

        // ---------------------------------------------------------- records
        public RecordEntry Record(List<RecordEntry> table, string key)
        {
            for (int i = 0; i < table.Count; i++)
                if (table[i].key == key) return table[i];
            var e = new RecordEntry { key = key };
            table.Add(e);
            return e;
        }

        public void Submit(List<RecordEntry> table, string key, int score, int wave, int kills)
        {
            RecordEntry e = Record(table, key);
            e.score = Mathf.Max(e.score, score);
            e.wave = Mathf.Max(e.wave, wave);
            e.kills = Mathf.Max(e.kills, kills);
        }

        // -------------------------------------------------------- inventory
        public Inventory ToInventory()
        {
            var inv = new Inventory();
            for (int i = 0; i < owned.Count; i++) inv.owned.Add(owned[i]);
            inv.owned.Add(Skins.DefaultPlayer);
            inv.owned.Add(Skins.DefaultWeapon);

            for (int i = 0; i < cases.Count; i++)
                if (cases[i].count > 0) inv.cases[cases[i].key] = cases[i].count;

            inv.scrap = scrap;
            inv.equippedPlayer = inv.owned.Contains(playerSkin) ? playerSkin : Skins.DefaultPlayer;
            inv.equippedWeapon = inv.owned.Contains(weaponSkin) ? weaponSkin : Skins.DefaultWeapon;
            return inv;
        }

        public void FromInventory(Inventory inv)
        {
            owned = new List<string>(inv.owned);
            owned.Sort(StringComparer.Ordinal);

            cases = new List<CaseEntry>();
            foreach (var kv in inv.cases)
                if (kv.Value > 0) cases.Add(new CaseEntry { key = kv.Key, count = kv.Value });

            scrap = inv.scrap;
            playerSkin = inv.equippedPlayer;
            weaponSkin = inv.equippedWeapon;
        }
    }
}
