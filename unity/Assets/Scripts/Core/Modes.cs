// C# / Assets/Scripts/Core/Modes.cs / Unity 2022.3+ / rule sets for the game modes
using System.Collections.Generic;

namespace Nightfall
{
    public class GameMode
    {
        public string key;
        public string name;
        public string tagline;
        public string[] rules;

        public float budgetMult = 1f;
        public float hpMult = 1f;
        public float speedMult = 1f;
        public float dmgMult = 1f;
        public float waveBreak = 5f;
        public float spawnMult = 1f;     // below 1 means faster spawns
        public float dropMult = 1f;
        public float scoreMult = 1f;
        public int bossEvery = 5;
        public bool continuous = false;  // no wave breaks, pressure never stops
        public float timeLimit = 0f;     // seconds, 0 = none
        public string[] startWeapons = new string[0];
        public int playerHp = 100;
        public int healOnWave = 0;
    }

    public static class Modes
    {
        public static readonly List<GameMode> All = new List<GameMode>
        {
            new GameMode
            {
                key = "survival", name = "SURVIVAL", tagline = "the classic run",
                rules = new[]
                {
                    "waves with a breather between them",
                    "supply drop after every wave",
                    "boss every 5th wave"
                }
            },
            new GameMode
            {
                key = "rush", name = "ENDLESS RUSH", tagline = "no breaks, no mercy",
                rules = new[]
                {
                    "zombies never stop coming",
                    "supplies drop on a timer",
                    "score x1.3"
                },
                waveBreak = 1.5f, spawnMult = 0.55f, budgetMult = 1.15f,
                scoreMult = 1.3f, continuous = true
            },
            new GameMode
            {
                key = "boss", name = "BOSS RUSH", tagline = "every wave is a boss",
                rules = new[]
                {
                    "a boss in every single wave",
                    "few mobs, huge health bars",
                    "score x1.5"
                },
                budgetMult = 0.45f, bossEvery = 1, waveBreak = 7f,
                scoreMult = 1.5f, startWeapons = new[] { "shotgun" }
            },
            new GameMode
            {
                key = "nightmare", name = "NIGHTMARE", tagline = "faster, tougher, meaner",
                rules = new[]
                {
                    "zombies +25% speed, +45% health",
                    "bigger waves, fewer drops",
                    "score x2"
                },
                budgetMult = 1.5f, hpMult = 1.45f, speedMult = 1.25f, dmgMult = 1.2f,
                dropMult = 0.65f, scoreMult = 2f, waveBreak = 4f
            },
            new GameMode
            {
                key = "blitz", name = "TIME ATTACK", tagline = "five minutes, one score",
                rules = new[]
                {
                    "5:00 on the clock",
                    "every kill adds one second",
                    "all four guns from the start"
                },
                timeLimit = 300f, waveBreak = 1f, spawnMult = 0.6f,
                budgetMult = 1.2f, continuous = true, scoreMult = 1.2f,
                startWeapons = new[] { "smg", "shotgun", "rifle" }, healOnWave = 12
            }
        };

        public static GameMode Get(string key)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].key == key) return All[i];
            return All[0];
        }

        public static int IndexOf(string key)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].key == key) return i;
            return 0;
        }
    }
}
