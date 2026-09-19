using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The end of a run: a veil over the table, and what became of the player, set large across it.
/// </summary>
/// <remarks>Two endings, one screen. The chapter only turns when the player leaves, in TableScreen.Leave.</remarks>
public class EndingVeil
{
    /// <summary>How dark the veil is, and its colour.</summary>
    private const float Opacity = 0.78f;
    private static readonly Color VeilColor = new(4, 3, 8);

    /// <summary>Where the verdict and the line under it sit.</summary>
    private const float TitleY = 300f;
    private const float LineY = 446f;

    /// <summary>Room left either side of the verdict before it is shrunk to fit.</summary>
    private const float Margin = 80f;

    private static readonly Color LineColor = new(170, 158, 160);
    private static readonly Vector2 ShadowOffset = new(3f, 3f);

    private SpriteFont _titleFont;
    private SpriteFont _uiFont;
    private Texture2D _pixel;

    /// <summary>Loads the fonts and the pixel the veil is stretched from.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    /// <param name="graphicsDevice">The device, for the pixel.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _titleFont = content.Load<SpriteFont>("spectral-title");
        _uiFont = content.Load<SpriteFont>("spectral-ui");

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Draws the veil and the verdict. The text batch.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="won">Whether the player is still standing.</param>
    public void Draw(SpriteBatch spriteBatch, bool won)
    {
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, BlackBoxGame.ScreenWidth, BlackBoxGame.ScreenHeight), null,
            VeilColor * Opacity, 0f, Vector2.Zero, SpriteEffects.None, Layers.Veil);

        string title = won ? "YOU ADVANCE" : "YOU HAVE BEEN CONSUMED BY THE BOX";
        string line = won
            ? "THE BOX LETS YOU UP. THERE IS ANOTHER TABLE."
            : "IT DOES NOT LET GO. IT WILL BE WAITING.";

        // The long verdict shrinks to fit rather than running off the edges.
        Text.DrawFitted(spriteBatch, _titleFont, title, TitleY, BlackBoxGame.ScreenWidth - 2f * Margin,
            won ? ButtonSprite.BoneWhite : ButtonSprite.EmberRed, Palette.HeavyShadow, ShadowOffset);
        Text.DrawCentered(spriteBatch, _uiFont, line, LineY, LineColor, Palette.FormShadow, Palette.ShadowOffset);
    }
}
