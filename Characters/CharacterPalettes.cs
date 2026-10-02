using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.Characters;

/// <summary>
/// The hair, outfit and accent presets, read off character-palettes.png, and the swap that turns
/// a player sheet into the colours they picked.
/// </summary>
/// <remarks>
/// tools/generate_characters.py paints the player's sheets in the first preset of each list (the
/// keys) and writes every preset into the palette PNG, one row each. Reading the ramps from that
/// PNG instead of copying them in here means the keys can never drift from what was painted.
/// The names are here because they are only words on a screen.
/// </remarks>
public class CharacterPalettes
{
    /// <summary>What each preset is called on the customization screen, in the order the PNG has them.</summary>
    public static readonly string[] HairNames = { "INK", "ASH BROWN", "CHESTNUT", "AUBURN", "BLEACHED", "GREY" };
    public static readonly string[] OutfitNames = { "CANVAS", "SLATE", "OXBLOOD", "CHARCOAL", "BONE", "RUST" };
    public static readonly string[] AccentNames = { "EMBER", "AMBER", "BONE", "TEAL", "MOSS", "VIOLET" };

    private Color[][] _hair;
    private Color[][] _outfit;
    private Color[][] _accent;

    /// <summary>Reads every preset out of character-palettes.png.</summary>
    /// <remarks>Throws if the PNG does not have the rows the names expect. That is a mismatch between the script and this file, and I would rather hear about it at once.</remarks>
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

    /// <summary>A hair preset's ramp, dark to light. For the swatch on the customization screen.</summary>
    /// <param name="index">Which preset.</param>
    public IReadOnlyList<Color> Hair(int index) => _hair[Wrap(index, _hair.Length)];

    /// <summary>An outfit preset's ramp.</summary>
    /// <param name="index">Which preset.</param>
    public IReadOnlyList<Color> Outfit(int index) => _outfit[Wrap(index, _outfit.Length)];

    /// <summary>An accent preset's ramp.</summary>
    /// <param name="index">Which preset.</param>
    public IReadOnlyList<Color> Accent(int index) => _accent[Wrap(index, _accent.Length)];

    /// <summary>Makes a copy of a player sheet in the colours of a look.</summary>
    /// <remarks>
    /// Every key colour in the sheet is swapped for the same shade of the chosen ramp. Done on
    /// the CPU once per change, not per frame: a sheet is 160x576, so it is instant, and it keeps
    /// to the Texture2D calls the sprite tutorials already use.
    /// </remarks>
    /// <param name="device">The device to make the new texture on.</param>
    /// <param name="sheet">The sheet as painted, in the keys.</param>
    /// <param name="look">The colours to put on it.</param>
    /// <returns>A new texture. The caller owns it and disposes of it.</returns>
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

    /// <summary>Wraps a preset index into range, so stepping left off the first lands on the last.</summary>
    /// <param name="index">The index, possibly out of range.</param>
    /// <param name="count">How many presets there are.</param>
    public static int Wrap(int index, int count) => (index % count + count) % count;

    /// <summary>Maps each shade of the key ramp to the same shade of the chosen one.</summary>
    private static void AddSwap(Dictionary<Color, Color> swap, IReadOnlyList<Color> key, IReadOnlyList<Color> chosen)
    {
        for (int i = 0; i < key.Count && i < chosen.Count; i++) swap[key[i]] = chosen[i];
    }

    /// <summary>Reads one block of rows. A transparent pixel ends a ramp: the accents are only three long.</summary>
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
