using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.Characters;

/// <summary>
/// The hair, outfit and accent presets from character-palettes.png, and the palette swap that
/// recolours a player sheet to whatever they picked.
/// </summary>
/// <remarks>
/// generate_characters.py paints the player sheets in the first preset of each list (the "keys")
/// and writes every preset to the palette PNG, one row each. I read the colours from that PNG
/// instead of copying them in here so they can't get out of sync with the sheets.
/// </remarks>
public class CharacterPalettes
{
    /// <summary>The preset names on the customization screen, in the same order as the PNG.</summary>
    public static readonly string[] HairNames = { "INK", "ASH BROWN", "CHESTNUT", "AUBURN", "BLEACHED", "GREY" };
    public static readonly string[] OutfitNames = { "CANVAS", "SLATE", "OXBLOOD", "CHARCOAL", "BONE", "RUST" };
    public static readonly string[] AccentNames = { "EMBER", "AMBER", "BONE", "TEAL", "MOSS", "VIOLET" };

    private Color[][] _hair;
    private Color[][] _outfit;
    private Color[][] _accent;

    /// <summary>Reads every preset out of character-palettes.png.</summary>
    /// <remarks>Throws if the PNG doesn't have as many rows as there are names. That means the script and this file don't match, and I'd rather find out right away.</remarks>
    /// <param name="content">The content manager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        Texture2D palettes = content.Load<Texture2D>("character-palettes");

        int rows = HairNames.Length + OutfitNames.Length + AccentNames.Length;
        if (palettes.Height != rows)
            throw new InvalidOperationException($"character-palettes.png has {palettes.Height} rows; the names need {rows}.");

        var pixels = new Color[palettes.Width * palettes.Height];
        palettes.GetData(pixels);

        int row = 0;
        _hair = ReadBlock(pixels, palettes.Width, ref row, HairNames.Length);
        _outfit = ReadBlock(pixels, palettes.Width, ref row, OutfitNames.Length);
        _accent = ReadBlock(pixels, palettes.Width, ref row, AccentNames.Length);
    }

    /// <summary>A hair preset's colours, dark to light. Used for the swatches on the customization screen.</summary>
    /// <param name="index">Which preset.</param>
    public IReadOnlyList<Color> Hair(int index) => _hair[Wrap(index, _hair.Length)];

    /// <summary>An outfit preset's colours.</summary>
    /// <param name="index">Which preset.</param>
    public IReadOnlyList<Color> Outfit(int index) => _outfit[Wrap(index, _outfit.Length)];

    /// <summary>An accent preset's colours.</summary>
    /// <param name="index">Which preset.</param>
    public IReadOnlyList<Color> Accent(int index) => _accent[Wrap(index, _accent.Length)];

    /// <summary>Makes a recoloured copy of a player sheet for a look.</summary>
    /// <remarks>
    /// Every key colour gets swapped for the same shade of the picked preset. It only runs when an
    /// option changes, not every frame, and the sheet is small so it's instant. (No shader needed,
    /// just GetData and SetData like the sprite tutorials.)
    /// </remarks>
    /// <param name="device">The device to make the new texture on.</param>
    /// <param name="sheet">The sheet in its key colours.</param>
    /// <param name="look">The colours to put on it.</param>
    /// <returns>A new texture. Whoever calls this has to dispose it.</returns>
    public Texture2D Recolour(GraphicsDevice device, Texture2D sheet, PlayerLook look)
    {
        var swap = new Dictionary<Color, Color>();
        AddSwap(swap, _hair[0], Hair(look.Hair));
        AddSwap(swap, _outfit[0], Outfit(look.Outfit));
        AddSwap(swap, _accent[0], Accent(look.Accent));

        var pixels = new Color[sheet.Width * sheet.Height];
        sheet.GetData(pixels);

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].A == byte.MaxValue && swap.TryGetValue(pixels[i], out Color swapped))
                pixels[i] = swapped;
        }

        var recoloured = new Texture2D(device, sheet.Width, sheet.Height);
        recoloured.SetData(pixels);
        return recoloured;
    }

    /// <summary>Wraps a preset index around, so going left from the first one lands on the last.</summary>
    /// <param name="index">The index, possibly out of range.</param>
    /// <param name="count">How many presets there are.</param>
    public static int Wrap(int index, int count) => (index % count + count) % count;

    /// <summary>Maps each key shade to the same shade of the picked preset.</summary>
    private static void AddSwap(Dictionary<Color, Color> swap, IReadOnlyList<Color> key, IReadOnlyList<Color> chosen)
    {
        for (int i = 0; i < key.Count && i < chosen.Count; i++) swap[key[i]] = chosen[i];
    }

    /// <summary>Reads one block of rows. A transparent pixel ends a ramp since the accents only have three shades.</summary>
    private static Color[][] ReadBlock(Color[] pixels, int width, ref int row, int count)
    {
        var block = new Color[count][];
        for (int i = 0; i < count; i++, row++)
        {
            var ramp = new List<Color>(width);
            for (int x = 0; x < width; x++)
            {
                Color c = pixels[row * width + x];
                if (c.A == 0) break;
                ramp.Add(c);
            }
            block[i] = ramp.ToArray();
        }
        return block;
    }
}
