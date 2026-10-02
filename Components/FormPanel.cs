using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The concrete panel the forms sit on, and the veil that darkens whatever is behind them.
/// </summary>
/// <remarks>The save form, options, pause and customization screens all use this, so the panel numbers only live in one place.</remarks>
public class FormPanel
{
    /// <summary>Width and height of the single frame in panel.png.</summary>
    private const int FrameSize = 32;

    /// <summary>Fixed corner of the panel's nine-slice. Has to match PANEL_CORNER in tools/generate_assets.py.</summary>
    private const int CornerSize = 12;

    /// <summary>Scaled up by the same amount as the buttons on it.</summary>
    private const float Scale = 3f;

    /// <summary>How dark the veil is. Dark enough to read the panel, but you can still see the box.</summary>
    private const float VeilOpacity = 0.62f;

    /// <summary>Padding inside the panel. The forms lay things out from this.</summary>
    public const int Padding = 40;

    private Texture2D _panel;
    private Texture2D _pixel;

    /// <summary>Loads the panel and the pixel the veil is drawn with.</summary>
    /// <param name="content">The content manager to load with.</param>
    /// <param name="graphicsDevice">The device, for the pixel.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _panel = content.Load<Texture2D>("panel");

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Dims the whole screen behind the form.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="opacity">How far the form has faded in, 0 to 1.</param>
    public void DrawVeil(SpriteBatch spriteBatch, float opacity)
    {
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, BlackBoxGame.ScreenWidth, BlackBoxGame.ScreenHeight), null,
            Palette.Veil * (VeilOpacity * opacity), 0f, Vector2.Zero, SpriteEffects.None, Layers.Veil);
    }

    /// <summary>Draws the panel, nine-sliced so it can be any size.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    /// <param name="bounds">Where the panel goes, in screen pixels.</param>
    /// <param name="opacity">How far the form has faded in, 0 to 1.</param>
    public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float opacity)
    {
        NineSlice.Draw(spriteBatch, _panel, bounds, Point.Zero,
            FrameSize, CornerSize, Scale, Color.White * opacity, Layers.PanelPlate);
    }
}
