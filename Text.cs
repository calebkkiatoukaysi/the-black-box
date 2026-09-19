using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Drawing a line of text over its shadow, and breaking a long one into lines. Shared by every screen that writes on the wall.
/// </summary>
public static class Text
{
    /// <summary>Draws a line centred across the screen, over a hard offset shadow.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="font">The SpriteFont to measure and render with.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="y">Top of the line, in screen pixels.</param>
    /// <param name="color">Colour of the text.</param>
    /// <param name="shadow">Colour of the shadow behind it.</param>
    /// <param name="shadowOffset">How far the shadow is thrown, in screen pixels.</param>
    public static void DrawCentered(SpriteBatch spriteBatch, SpriteFont font, string text, float y,
        Color color, Color shadow, Vector2 shadowOffset)
    {
        Vector2 size = font.MeasureString(text);
        var position = new Vector2(MathF.Round((BlackBoxGame.ScreenWidth - size.X) / 2f), y);

        spriteBatch.DrawString(font, text, position + shadowOffset, shadow, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
        spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);
    }

    /// <summary>Draws a line centred, scaled down if it would not fit in the width given.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="font">The face to set it in.</param>
    /// <param name="text">The line.</param>
    /// <param name="y">Where the top of it goes, at full size; a scaled line is centred on the same middle.</param>
    /// <param name="maxWidth">The widest it may be, in screen pixels.</param>
    /// <param name="color">The colour of the text.</param>
    /// <param name="shadow">The colour of its shadow.</param>
    /// <param name="shadowOffset">How far the shadow sits from the text.</param>
    public static void DrawFitted(SpriteBatch spriteBatch, SpriteFont font, string text, float y, float maxWidth,
        Color color, Color shadow, Vector2 shadowOffset)
    {
        Vector2 size = font.MeasureString(text);
        float scale = size.X > maxWidth ? maxWidth / size.X : 1f;
        var position = new Vector2(
            MathF.Round((BlackBoxGame.ScreenWidth - size.X * scale) / 2f),
            MathF.Round(y + size.Y * (1f - scale) / 2f));

        spriteBatch.DrawString(font, text, position + shadowOffset, shadow, 0f, Vector2.Zero, scale, SpriteEffects.None, Layers.TextShadow);
        spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, Layers.Text);
    }

    /// <summary>Draws a line against a point, over a hard offset shadow.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="font">The SpriteFont to measure and render with.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">The left edge, the centre, or the right edge, depending on the flags.</param>
    /// <param name="y">Top of the line, in screen pixels.</param>
    /// <param name="color">Colour of the text.</param>
    /// <param name="shadow">Colour of the shadow behind it.</param>
    /// <param name="centred">True to centre the line on <paramref name="x"/>.</param>
    /// <param name="rightAligned">True to end the line at <paramref name="x"/>.</param>
    public static void Draw(SpriteBatch spriteBatch, SpriteFont font, string text, float x, float y,
        Color color, Color shadow, bool centred = false, bool rightAligned = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        Vector2 size = font.MeasureString(text);
        float left = centred ? x - size.X / 2f : rightAligned ? x - size.X : x;
        var position = new Vector2(MathF.Round(left), MathF.Round(y));

        spriteBatch.DrawString(font, text, position + Palette.ShadowOffset, shadow, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
        spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);
    }

    /// <summary>Breaks a line into as many lines as it takes to fit.</summary>
    /// <remarks>Measured with the font rather than by character count, since the font is proportional.</remarks>
    /// <param name="font">The SpriteFont the text will be drawn in.</param>
    /// <param name="text">The line to break.</param>
    /// <param name="width">How wide a line may be, in screen pixels.</param>
    /// <returns>One entry per line, in order.</returns>
    public static string[] Wrap(SpriteFont font, string text, float width)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;

            if (line.Length > 0 && font.MeasureString(candidate).X > width)
            {
                lines.Add(line.ToString());
                line.Clear();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }

        if (line.Length > 0) lines.Add(line.ToString());
        return lines.ToArray();
    }
}
