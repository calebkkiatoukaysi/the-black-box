using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// A row of hearts: what a side has left to lose.
/// </summary>
/// <remarks>
/// Lives used to be wounds on her face. Now they're hearts on both sides, and spent ones are
/// drawn hollow so you can see how many you started with. Two frames in hearts.png, full and hollow.
/// </remarks>
public class HeartsSprite
{
    /// <summary>Width and height of one frame in hearts.png.</summary>
    private const int FrameSize = 12;

    /// <summary>How far the art is blown up.</summary>
    private const float Scale = 3f;

    /// <summary>The gap between hearts, in screen pixels.</summary>
    private const float Gap = 6f;

    private Texture2D _sheet;

    /// <summary>How wide a row of this many hearts is, in screen pixels.</summary>
    /// <param name="count">How many hearts are in the row.</param>
    public static float WidthOf(int count) => count * FrameSize * Scale + (count - 1) * Gap;

    /// <summary>How tall a heart is on screen.</summary>
    public static float Height => FrameSize * Scale;

    /// <summary>Loads hearts.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => _sheet = content.Load<Texture2D>("hearts");

    /// <summary>Draws a row of hearts from a top-left corner.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    /// <param name="topLeft">Where the first heart's top-left corner goes, in screen pixels.</param>
    /// <param name="lives">How many are still full.</param>
    /// <param name="total">How many there are altogether.</param>
    /// <param name="layer">The sort depth to draw at.</param>
    public void Draw(SpriteBatch spriteBatch, Vector2 topLeft, int lives, int total, float layer)
    {
        if (_sheet is null) return;

        for (int i = 0; i < total; i++)
        {
            int frame = i < lives ? 0 : 1;
            var source = new Rectangle(frame * FrameSize, 0, FrameSize, FrameSize);
            var at = new Vector2(topLeft.X + i * (FrameSize * Scale + Gap), topLeft.Y);
            spriteBatch.Draw(_sheet, at, source, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, layer);
        }
    }
}
