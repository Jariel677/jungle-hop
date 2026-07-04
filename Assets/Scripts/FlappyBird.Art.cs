using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="FlappyBird"/> UI-art loading: pulls the CraftPix jungle sprites from
/// Resources/JungleUI by name (cached), and draws them as aspect-fit images and
/// invisible sprite buttons for the immediate-mode HUD. Same partial class.
/// </summary>
public partial class FlappyBird
{
    Dictionary<string, Texture2D> _texCache;

    /// <summary>Loads and caches a sprite by path under Resources/JungleUI (no extension).</summary>
    Texture2D Tex(string path)
    {
        _texCache ??= new Dictionary<string, Texture2D>();
        if (!_texCache.TryGetValue(path, out var t))
        {
            t = Resources.Load<Texture2D>("JungleUI/" + path);
            _texCache[path] = t; // cache even nulls so a missing sprite isn't reloaded each frame
        }
        return t;
    }

    /// <summary>Draws a sprite scaled to fit the rect, preserving aspect. No-op if missing.</summary>
    void Sprite(Rect r, string path)
    {
        Texture2D t = Tex(path);
        if (t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit);
    }

    /// <summary>An image button: the sprite plus an invisible clickable area over it.</summary>
    bool SpriteButton(Rect r, string path)
    {
        Sprite(r, path);
        return GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    /// <summary>Draws a sprite that fills the rect (cropping overflow) — for backgrounds.</summary>
    void SpriteFill(Rect r, string path)
    {
        Texture2D t = Tex(path);
        if (t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleAndCrop);
    }
}

