using UnityEngine;

/// <summary>
/// Persistent player profile — coin bank, unlocks, equipped cosmetics, settings,
/// high score and daily-reward state. Backed by <see cref="PlayerPrefs"/>.
/// </summary>
public static class GameData
{
    public static int Coins
    {
        get { return PlayerPrefs.GetInt("gd_coins", 0); }
        set { PlayerPrefs.SetInt("gd_coins", Mathf.Max(0, value)); }
    }

    public static int HighScore
    {
        get { return PlayerPrefs.GetInt("subway_highscore", 0); }
        set { PlayerPrefs.SetInt("subway_highscore", value); }
    }

    public static int EquippedCharacter
    {
        get { return PlayerPrefs.GetInt("gd_char", 0); }
        set { PlayerPrefs.SetInt("gd_char", value); }
    }

    public static int EquippedBoard
    {
        get { return PlayerPrefs.GetInt("gd_board", 0); }
        set { PlayerPrefs.SetInt("gd_board", value); }
    }

    public static bool CharacterOwned(int id)
    {
        return id == 0 || PlayerPrefs.GetInt("gd_chu_" + id, 0) == 1;
    }

    public static void OwnCharacter(int id) { PlayerPrefs.SetInt("gd_chu_" + id, 1); }

    public static bool BoardOwned(int id)
    {
        return id == 0 || PlayerPrefs.GetInt("gd_bou_" + id, 0) == 1;
    }

    public static void OwnBoard(int id) { PlayerPrefs.SetInt("gd_bou_" + id, 1); }

    // ----- settings -----
    public static bool ScreenShake
    {
        get { return PlayerPrefs.GetInt("gd_set_shake", 1) == 1; }
        set { PlayerPrefs.SetInt("gd_set_shake", value ? 1 : 0); }
    }

    public static bool HighContrast
    {
        get { return PlayerPrefs.GetInt("gd_set_contrast", 0) == 1; }
        set { PlayerPrefs.SetInt("gd_set_contrast", value ? 1 : 0); }
    }

    public static bool Music
    {
        get { return PlayerPrefs.GetInt("gd_set_music", 1) == 1; }
        set { PlayerPrefs.SetInt("gd_set_music", value ? 1 : 0); }
    }

    // ----- daily reward -----
    static int Today()
    {
        System.DateTime n = System.DateTime.Now;
        return n.Year * 1000 + n.DayOfYear;
    }

    public static bool DailyAvailable
    {
        get { return PlayerPrefs.GetInt("gd_daily_day", -1) != Today(); }
    }

    /// <summary>Claims the daily reward and returns the coin amount granted.</summary>
    public static int ClaimDaily()
    {
        int streak = PlayerPrefs.GetInt("gd_daily_streak", 0) + 1;
        PlayerPrefs.SetInt("gd_daily_streak", streak);
        PlayerPrefs.SetInt("gd_daily_day", Today());
        int reward = 50 + Mathf.Min(streak, 7) * 25;
        Coins += reward;
        Save();
        return reward;
    }

    public static void Save() { PlayerPrefs.Save(); }
}
