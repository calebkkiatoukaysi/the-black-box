using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The figure across the table: their face, and what they think of you.
/// </summary>
/// <remarks>
/// Nothing animates. Every cell of the sheet is one still and the game switches between them,
/// because a flapping jaw with no audio reads as a puppet. <see cref="Pose"/> is the column.
/// Which sheet is <see cref="Who"/>: every opponent's sheet has the same five columns, so the
/// sprite doesn't care who is in the seat.
/// </remarks>
public class OpponentSprite
{
    /// <summary>Every opponent's sheet, by the content name on the roster.</summary>
    private readonly Dictionary<string, Texture2D> _sheets = new();

    /// <summary>Who is in the seat: which sheet, how big a frame is, and how far it is blown up.</summary>
    /// <remarks>The bottom row of every frame is the table lip.</remarks>
    public Opponent Who { get; set; } = Opponents.First;

    /// <summary>Which picture is on the table.</summary>
    public OpponentPose Pose { get; set; } = OpponentPose.Even;

    /// <summary>Loads every sheet on the roster.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content)
    {
        foreach (Opponent opponent in Opponents.Roster)
            _sheets[opponent.Sheet] = content.Load<Texture2D>(opponent.Sheet);
    }

    /// <summary>Puts them in the temper their disposition says they are in, whatever they were doing.</summary>
    /// <param name="disposition">What they currently think of the player.</param>
    public void SetDisposition(Disposition disposition)
    {
        Pose = disposition.Band switch
        {
            Warmth.Hostile => OpponentPose.Hostile,
            Warmth.Open => OpponentPose.Open,
            _ => OpponentPose.Even,
        };
    }

    /// <summary>Draws whichever picture of them is current.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="bottomCentre">Where the bottom middle of the frame lands, which is the table lip.</param>
    public void Draw(SpriteBatch spriteBatch, Vector2 bottomCentre)
    {
        if (!_sheets.TryGetValue(Who.Sheet, out Texture2D sheet)) return;

        int width = Who.FrameWidth, height = Who.FrameHeight;
        float scale = Who.Scale;

        var source = new Rectangle((int)Pose * width, 0, width, height);

        var position = new Vector2(
            MathF.Round(bottomCentre.X - width * scale / 2f),
            MathF.Round(bottomCentre.Y - height * scale));

        spriteBatch.Draw(sheet, position, source, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Opponent);
    }
}
