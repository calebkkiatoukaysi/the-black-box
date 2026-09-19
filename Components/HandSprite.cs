using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The player's own arm, going into the box.
/// </summary>
/// <remarks>
/// Comes in from the bottom-right on a diagonal, like your own arm reaching for something, and
/// slides along its own length so the elbow never leaves the bottom edge. Five frames, curled
/// through open. The first sheet was a hand on its own and it floated like a glove, so this one
/// has the forearm too. Drawn in front of the box, not clipped into it.
/// </remarks>
public class HandSprite
{
    /// <summary>Width and height of one frame in hand-sheet.png.</summary>
    private const int FrameWidth = 96;

    /// <summary>The other dimension. See <see cref="FrameWidth"/>.</summary>
    private const int FrameHeight = 96;

    /// <summary>Frames in the sheet, curled through offered.</summary>
    private const int Frames = 5;

    /// <summary>Where the tip of the middle finger is inside a frame, in sheet pixels.</summary>
    /// <remarks>The sprite is placed by its fingertip, since that's the part the game cares about. Matches HAND_FINGERTIP in tools/generate_assets.py.</remarks>
    private static readonly Vector2 Fingertip = new(17f, 25f);

    private Texture2D _sheet;

    /// <summary>How far in the hand is, from 0 (down at the table) to 1 (open, inside the mouth).</summary>
    /// <remarks>One number drives both the pose and the position, so the fingers finish opening as the hand arrives.</remarks>
    public float Reach { get; set; }

    /// <summary>Whether the hand is anywhere the player can see it.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Loads hand-sheet.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => _sheet = content.Load<Texture2D>("hand-sheet");

    /// <summary>Draws the arm somewhere between resting out of sight and the mouth of the box.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="rest">Where the fingertips wait before the hand is offered, in screen pixels.</param>
    /// <param name="mouth">Where the fingertips end up once the hand is all the way in.</param>
    /// <param name="scale">How far the art is blown up. Whole numbers keep it crisp.</param>
    public void Draw(SpriteBatch spriteBatch, Vector2 rest, Vector2 mouth, float scale)
    {
        if (!IsVisible || _sheet is null) return;

        float reach = Math.Clamp(Reach, 0f, 1f);

        // Eased, so the hand slows as it arrives instead of stopping dead.
        float travel = 1f - (1f - reach) * (1f - reach);

        int frame = Math.Min(Frames - 1, (int)(reach * Frames));
        var source = new Rectangle(frame * FrameWidth, 0, FrameWidth, FrameHeight);

        Vector2 tip = Vector2.Lerp(rest, mouth, travel);
        var position = new Vector2(
            MathF.Round(tip.X - Fingertip.X * scale),
            MathF.Round(tip.Y - Fingertip.Y * scale));

        spriteBatch.Draw(_sheet, position, source, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Hand);
    }
}
