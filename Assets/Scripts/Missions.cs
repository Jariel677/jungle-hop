using UnityEngine;

/// <summary>
/// Three rolling missions that persist across runs. Progress accumulates after
/// every run; finished missions are claimed for coins on the Missions screen.
/// </summary>
public static class Missions
{
    public enum Kind { Coins, Distance, PowerUps, Runs }

    public class Mission
    {
        public Kind kind;
        public int target, reward, progress;

        public bool Done { get { return progress >= target; } }

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case Kind.Coins: return "Collect " + target + " coins";
                    case Kind.Distance: return "Run " + target + " m";
                    case Kind.PowerUps: return "Grab " + target + " power-ups";
                    default: return "Complete " + target + " runs";
                }
            }
        }
    }

    const int Slots = 3;
    static Mission[] _cache;

    public static Mission[] Current()
    {
        if (_cache == null)
        {
            _cache = new Mission[Slots];
            for (int i = 0; i < Slots; i++)
            {
                Mission m = LoadSlot(i);
                if (m == null) { m = Generate(); SaveSlot(i, m); }
                _cache[i] = m;
            }
            PlayerPrefs.Save();
        }
        return _cache;
    }

    static Mission LoadSlot(int i)
    {
        string s = PlayerPrefs.GetString("gd_mission_" + i, "");
        if (string.IsNullOrEmpty(s)) return null;
        string[] p = s.Split(':');
        if (p.Length != 4) return null;
        return new Mission
        {
            kind = (Kind)int.Parse(p[0]),
            target = int.Parse(p[1]),
            reward = int.Parse(p[2]),
            progress = int.Parse(p[3]),
        };
    }

    static void SaveSlot(int i, Mission m)
    {
        PlayerPrefs.SetString("gd_mission_" + i,
            (int)m.kind + ":" + m.target + ":" + m.reward + ":" + m.progress);
    }

    static Mission Generate()
    {
        Kind k = (Kind)Random.Range(0, 4);
        int target, reward;
        switch (k)
        {
            case Kind.Coins: target = Random.Range(2, 9) * 25; reward = target * 2; break;
            case Kind.Distance: target = Random.Range(3, 13) * 250; reward = target / 3; break;
            case Kind.PowerUps: target = Random.Range(2, 7); reward = target * 70; break;
            default: target = Random.Range(2, 6); reward = target * 90; break;
        }
        return new Mission { kind = k, target = target, reward = reward, progress = 0 };
    }

    /// <summary>Adds a finished run's stats to every unfinished mission.</summary>
    public static void ReportRun(int coins, int distance, int powerUps)
    {
        Mission[] m = Current();
        for (int i = 0; i < Slots; i++)
        {
            if (m[i].Done) continue;
            switch (m[i].kind)
            {
                case Kind.Coins: m[i].progress += coins; break;
                case Kind.Distance: m[i].progress += distance; break;
                case Kind.PowerUps: m[i].progress += powerUps; break;
                case Kind.Runs: m[i].progress += 1; break;
            }
            SaveSlot(i, m[i]);
        }
        PlayerPrefs.Save();
    }

    /// <summary>Claims a finished mission's reward and rolls a fresh one.</summary>
    public static int Claim(int slot)
    {
        Mission[] m = Current();
        if (slot < 0 || slot >= Slots || !m[slot].Done) return 0;
        int reward = m[slot].reward;
        GameData.Coins += reward;
        m[slot] = Generate();
        SaveSlot(slot, m[slot]);
        GameData.Save();
        return reward;
    }
}
