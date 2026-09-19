using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The line of bookkeeping along the top of the table: the chapter and the clock in the middle,
/// your name and hearts on the left, theirs on the right, and the way out in the corner.
/// </summary>
/// <remarks>
/// Everything is measured off its neighbour, nothing is at a fixed x. The first draft put THEM
/// at a fixed offset and a long name pushed the player's hearts under it. Now yours are on the
/// left over your pockets and theirs on the right over theirs, and the two can never meet.
/// </remarks>
public class RunHeading
{
    /// <summary>How far in from the screen edge the heading starts. The pocket rows line up under it.</summary>
    public const float Margin = 24f;

    /// <summary>Where the top of the line sits.</summary>
    private const float Y = 14f;

    /// <summary>The gap between a name and its hearts, and between their hearts and the hint.</summary>
    /// <remarks>The hint gap is as small as it can be: the clock in the middle needs the room on the other side of THEM.</remarks>
    private const float HeartsGap = 12f;
    private const float HintGap = 32f;

    private const string ThemLabel = "THEM";
    private const string LeaveHint = "ESC  ·  SAVE AND LEAVE";

    private readonly HeartsSprite _hearts = new();
    private SpriteFont _detailFont;

    /// <summary>Loads the font and the hearts.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _detailFont = content.Load<SpriteFont>("spectral-detail");
        _hearts.LoadContent(content);
    }

    /// <summary>Draws the words. The text batch.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="run">The run on the table.</param>
    public void DrawText(SpriteBatch spriteBatch, SaveData run)
    {
        string heading = string.Format(CultureInfo.InvariantCulture,
            "CHAPTER {0}  ·  ROUND {1}  ·  {2:00}:{3:00}",
            run.Chapter, run.Round + 1,
            (int)run.Playtime.TotalHours, run.Playtime.Minutes);

        Text.DrawCentered(spriteBatch, _detailFont, heading, Y, Palette.DimText, Palette.Shadow, Palette.ShadowOffset);

        Text.Draw(spriteBatch, _detailFont, run.PlayerName.ToUpperInvariant(), Margin, Y, Palette.DimText, Palette.Shadow);
        Text.Draw(spriteBatch, _detailFont, ThemLabel, ThemX(), Y, Palette.DimText, Palette.Shadow);

        // The way out, in the corner with the rest of the bookkeeping.
        Text.Draw(spriteBatch, _detailFont, LeaveHint, BlackBoxGame.ScreenWidth - Margin, Y,
            Palette.DimText, Palette.Shadow, rightAligned: true);
    }

    /// <summary>Draws both sides' hearts after their names. The pixel-art batch, so they stay crisp.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    /// <param name="run">The run on the table.</param>
    public void DrawHearts(SpriteBatch spriteBatch, SaveData run)
    {
        // Centred on the line of text, which is a little shorter than a heart.
        float y = MathF.Round(Y + (_detailFont.LineSpacing - HeartsSprite.Height) / 2f);

        float playerHearts = MathF.Round(Margin + _detailFont.MeasureString(run.PlayerName.ToUpperInvariant()).X + HeartsGap);
        _hearts.Draw(spriteBatch, new Vector2(playerHearts, y), run.PlayerLives, SaveData.StartingLives, Layers.ButtonLabel);
        _hearts.Draw(spriteBatch, new Vector2(MathF.Round(ThemHeartsX()), y), run.OpponentLives, SaveData.StartingLives, Layers.ButtonLabel);
    }

    /// <summary>Where their hearts start: a gap left of the hint, so the whole right-hand block is right-aligned.</summary>
    private float ThemHeartsX() =>
        BlackBoxGame.ScreenWidth - Margin - _detailFont.MeasureString(LeaveHint).X - HintGap - HeartsSprite.WidthOf(SaveData.StartingLives);

    /// <summary>Where THEM starts: its own width and a gap left of its hearts.</summary>
    private float ThemX() => ThemHeartsX() - HeartsGap - _detailFont.MeasureString(ThemLabel).X;
}
