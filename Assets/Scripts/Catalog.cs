using UnityEngine;

/// <summary>A purchasable runner skin (procedural color scheme).</summary>
public struct CharacterDef
{
    public int id;
    public string name;
    public int cost;
    public Color shirt, pants, skin;
}

/// <summary>A purchasable hoverboard skin.</summary>
public struct BoardDef
{
    public int id;
    public string name;
    public int cost;
    public Color deck, trim;
}

/// <summary>Static shop catalog of characters and hoverboards.</summary>
public static class Catalog
{
    public static readonly CharacterDef[] Characters =
    {
        new CharacterDef { id = 0, name = "Dash",  cost = 0,
            shirt = new Color(0.93f, 0.36f, 0.16f), pants = new Color(0.17f, 0.21f, 0.4f),  skin = new Color(0.96f, 0.79f, 0.62f) },
        new CharacterDef { id = 1, name = "Nova",  cost = 250,
            shirt = new Color(0.2f, 0.5f, 0.95f),   pants = new Color(0.14f, 0.15f, 0.2f),  skin = new Color(0.85f, 0.68f, 0.55f) },
        new CharacterDef { id = 2, name = "Volt",  cost = 500,
            shirt = new Color(0.3f, 0.85f, 0.32f),  pants = new Color(0.2f, 0.2f, 0.22f),   skin = new Color(0.72f, 0.56f, 0.43f) },
        new CharacterDef { id = 3, name = "Blaze", cost = 1000,
            shirt = new Color(0.9f, 0.16f, 0.2f),   pants = new Color(0.1f, 0.1f, 0.12f),   skin = new Color(0.95f, 0.8f, 0.66f) },
        new CharacterDef { id = 4, name = "Mint",  cost = 2000,
            shirt = new Color(0.18f, 0.8f, 0.7f),   pants = new Color(0.9f, 0.9f, 0.93f),   skin = new Color(0.6f, 0.45f, 0.36f) },
        new CharacterDef { id = 5, name = "Pixel", cost = 3500,
            shirt = new Color(0.62f, 0.3f, 0.95f),  pants = new Color(0.12f, 0.13f, 0.22f), skin = new Color(0.9f, 0.74f, 0.6f) },
    };

    public static readonly BoardDef[] Boards =
    {
        new BoardDef { id = 0, name = "No Board", cost = 0,
            deck = Color.white, trim = Color.white },
        new BoardDef { id = 1, name = "Surfer",  cost = 300,
            deck = new Color(0.95f, 0.85f, 0.2f),  trim = new Color(0.2f, 0.2f, 0.25f) },
        new BoardDef { id = 2, name = "Comet",   cost = 700,
            deck = new Color(0.2f, 0.7f, 0.95f),   trim = new Color(0.95f, 0.96f, 1f) },
        new BoardDef { id = 3, name = "Magma",   cost = 1500,
            deck = new Color(0.95f, 0.35f, 0.1f),  trim = new Color(0.14f, 0.14f, 0.15f) },
        new BoardDef { id = 4, name = "Aurora",  cost = 2400,
            deck = new Color(0.35f, 0.9f, 0.6f),   trim = new Color(0.6f, 0.45f, 0.95f) },
        new BoardDef { id = 5, name = "Phantom", cost = 4000,
            deck = new Color(0.16f, 0.17f, 0.22f), trim = new Color(0.55f, 0.85f, 1f) },
    };

    public static CharacterDef Character(int id)
    {
        foreach (CharacterDef c in Characters)
            if (c.id == id) return c;
        return Characters[0];
    }

    public static BoardDef Board(int id)
    {
        foreach (BoardDef b in Boards)
            if (b.id == id) return b;
        return Boards[0];
    }
}
