using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The player's own hand, going into the box.
/// </summary>
/// <remarks>
/// <para>
/// Seen from behind and reaching away from the camera, because it is the player's hand and
/// that is where their hand is. Five frames: curled on the table, through to open and fanned,
/// which is the pose the box is waiting for.
/// </para>
/// <para>
/// Drawn in front of the box rather than clipped into its opening. The box's front face is not
/// a surface, it is a hole, so a hand crossing the lower rim on its way in is close enough to
/// right -- and cutting the sprite against the mouth would mean the hand could never be seen
/// to arrive.
/// </para>
/// </remarks>
public class HandSprite
{
    /// <summary>Width and height of one frame in hand-sheet.png.</summary>
    private const int FrameWidth = 40;

    /// <summary>The other dimension. See <see cref="FrameWidth"/>.</summary>
    private const int FrameHeight = 48;

    /// <summary>Frames in the sheet, curled through offered.</summary>
    private const int Frames = 5;

    private Texture2D _sheet;

    /// <summary>
    /// How far in the hand is, from 0 (down at the table) to 1 (open, inside the mouth).
    /// </summary>
    /// <remarks>
    /// One number drives both the pose and the position, so the fingers finish opening at the
    /// moment the hand finishes arriving. Splitting them would let a fist reach the mouth and
    /// unclench after it got there, which is a different and much less willing gesture.
    /// </remarks>
    public float Reach { get; set; }

    /// <summary>Whether the hand is anywhere the player can see it.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Loads hand-sheet.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => _sheet = content.Load<Texture2D>("hand-sheet");

    /// <summary>
    /// Draws the hand somewhere between the table and the mouth of the box.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="centreX">The line the hand travels up, which is the middle of the box.</param>
    /// <param name="restY">Where the top of the hand sits before it is offered.</param>
    /// <param name="mouthY">Where the top of the hand ends up once it is all the way in.</param>
    /// <param name="scale">How far the art is blown up. Whole numbers keep it crisp.</param>
    public void Draw(SpriteBatch spriteBatch, float centreX, float restY, float mouthY, float scale)
    {
        if (!IsVisible || _sheet is null) return;

        float reach = Math.Clamp(Reach, 0f, 1f);

        // Eased, so the hand slows as it arrives rather than stopping dead in the opening.
        float travel = 1f - (1f - reach) * (1f - reach);

        int frame = Math.Min(Frames - 1, (int)(reach * Frames));
        var source = new Rectangle(frame * FrameWidth, 0, FrameWidth, FrameHeight);

        var position = new Vector2(
            MathF.Round(centreX - FrameWidth * scale / 2f),
            MathF.Round(MathHelper.Lerp(restY, mouthY, travel)));

        spriteBatch.Draw(_sheet, position, source, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Hand);
    }
}
