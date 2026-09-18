using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The player's own arm, going into the box.
/// </summary>
/// <remarks>
/// <para>
/// Seen from behind and above and reaching away from the camera, because it is the player's
/// arm and that is where their arm is. It comes in from the bottom-right corner on a
/// diagonal, the way your own right arm crosses your view when you reach for something in
/// front of you, and it slides out along its own length rather than rising up the screen --
/// the elbow end stays off the bottom edge the whole way, so it is always coming out of
/// somebody. Five frames: a hand still half-curled from the table, through to open and
/// fanned, which is the pose the box is waiting for.
/// </para>
/// <para>
/// The first sheet was a hand on its own, drawn straight up the middle. It floated: a hand
/// with nothing behind it reads as a glove. This one is the forearm as well, with the cuff of
/// a sleeve at the near end, and it is big -- it fills the corner of the screen the way an
/// arm fills the bottom of your own eye. That is what makes the box close rather than far.
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
    private const int FrameWidth = 96;

    /// <summary>The other dimension. See <see cref="FrameWidth"/>.</summary>
    private const int FrameHeight = 96;

    /// <summary>Frames in the sheet, curled through offered.</summary>
    private const int Frames = 5;

    /// <summary>
    /// Where the tip of the middle finger is inside a frame, in sheet pixels.
    /// </summary>
    /// <remarks>
    /// The sprite is placed by its fingertip rather than its corner, because the fingertip
    /// is the only part of it anybody cares about the position of: the game says where the
    /// fingers land and the arm follows. It is <c>HAND_FINGERTIP</c> in
    /// <c>tools/generate_assets.py</c>, computed off the open frame's skeleton; the closed frames are
    /// shorter, which is right, because a curling hand pulls back from where it was reaching.
    /// </remarks>
    private static readonly Vector2 Fingertip = new(28f, 22f);

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
    /// Draws the arm somewhere between resting out of sight and the mouth of the box.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="rest">Where the fingertips wait before the hand is offered, in screen pixels.</param>
    /// <param name="mouth">Where the fingertips end up once the hand is all the way in.</param>
    /// <param name="scale">How far the art is blown up. Whole numbers keep it crisp.</param>
    public void Draw(SpriteBatch spriteBatch, Vector2 rest, Vector2 mouth, float scale)
    {
        if (!IsVisible || _sheet is null) return;

        float reach = Math.Clamp(Reach, 0f, 1f);

        // Eased, so the hand slows as it arrives rather than stopping dead in the opening.
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
