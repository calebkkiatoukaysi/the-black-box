using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The concrete slab the forms are built on, and the veil that dims whatever is behind them.
/// </summary>
/// <remarks>The save form and the options, pause and customization screens all sit on one of these, so the panel's numbers live here once.</remarks>
public class FormPanel
{
    /// <summary>Width and height of the single frame in panel.png.</summary>
    private const int FrameSize = 32;

    /// <summary>Fixed corner of the panel's nine-slice. Has to match PANEL_CORNER in tools/generate_assets.py.</summary>
    private const int CornerSize = 12;

    /// <summary>Blown up by the same whole number as the buttons that sit on it.</summary>
    private const float Scale = 3f;

    /// <summary>How far the veil darkens what is behind. Enough to read the panel, not enough to hide the box.</summary>
    private const float VeilOpacity = 0.62f;

    /// <summary>Breathing room inside the panel's recess. Forms lay their contents out from this.</summary>
    public const int Padding = 40;

    private Texture2D _panel;
    private Texture2D _pixel;

    /// <summary>Loads the panel and the pixel the veil is stretched from.</summary>
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

    /// <summary>Draws the slab, nine-sliced to any size.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    /// <param name="bounds">Where the slab goes, in screen pixels.</param>
    /// <param name="opacity">How far the form has faded in, 0 to 1.</param>
    public void Draw(SpriteBatch spriteBatch, Rectangle bounds, float opacity)
    {
        NineSlice.Draw(spriteBatch, _panel, bounds, Point.Zero,
            FrameSize, CornerSize, Scale, Color.White * opacity, Layers.PanelPlate);
    }
}
