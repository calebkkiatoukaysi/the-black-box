using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The plate on the wall the conversation is written on, to the right of the opponent.
/// </summary>
/// <remarks>
/// It used to sit above their head, but the head reaches the top of the wall now. The speaker's
/// name is at the top, the line under it, and the plate grows downward as the line wraps. While
/// the player is deciding about something the box dealt, its picture sits left of its line.
/// </remarks>
public class DialoguePlate
{
    /// <summary>Where the plate is and how wide. DialogueWheel's patience bar is placed off these.</summary>
    public const int Left = 1000;
    public const int Top = 56;
    public const int Width = 580;

    /// <summary>Breathing room inside the plate, either side of the text.</summary>
    public const int Padding = 40;

    /// <summary>Where the top of the speaker's name sits, where the patience bar goes under it, and where the first line starts.</summary>
    public const float NameY = 66f;
    public const int PatienceY = 104;
    public const float LineY = 124f;

    /// <summary>How much of the padding is left under the last line.</summary>
    private const float BottomPad = 0.6f;

    /// <summary>How far the item's picture is blown up beside its line, and the gap between them.</summary>
    private const float IconScale = 3f;
    private const float IconGap = 16f;

    private static readonly Color PlateColor = new Color(6, 5, 9) * 0.80f;
    private static readonly Color PlateLip = new(64, 58, 66);
    private static readonly Color LineLive = new(226, 216, 210);
    private static readonly Color LineSaid = new(162, 150, 150);

    private readonly ItemSprite _items = new();
    private SpriteFont _uiFont;

    /// <summary>One white pixel, stretched into the plate.</summary>
    private Texture2D _pixel;

    /// <summary>How big the item's picture is on screen.</summary>
    private static float IconSize => ItemSprite.FrameSize * IconScale;

    /// <summary>Loads the font, the item pictures and the pixel.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    /// <param name="graphicsDevice">The device, for the pixel.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _uiFont = content.Load<SpriteFont>("spectral-ui");
        _items.LoadContent(content);

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Draws the plate, the speaker's name and the line. The text batch.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="speaker">Whose plate it is right now.</param>
    /// <param name="speakerColour">The colour their name is set in.</param>
    /// <param name="text">What is being said, or what the box just did.</param>
    /// <param name="live">Whether a line is being said now, rather than the box reading out what happened.</param>
    /// <param name="withIcon">Whether to leave room on the left for an item's picture. See <see cref="DrawIcon"/>.</param>
    public void Draw(SpriteBatch spriteBatch, string speaker, Color speakerColour, string text, bool live, bool withIcon)
    {
        float inset = withIcon ? IconSize + IconGap : 0f;
        float textLeft = Left + Padding + inset;
        float textWidth = Width - 2 * Padding - inset;

        string[] lines = Text.Wrap(_uiFont, text, textWidth);
        float textHeight = Math.Max(1, lines.Length) * _uiFont.LineSpacing;
        float blockHeight = withIcon ? MathF.Max(textHeight, IconSize) : textHeight;

        // The plate closes a little under the last line, or under the picture if that is taller, and never above the patience bar.
        float bottom = LineY + blockHeight + Padding * BottomPad;
        var plate = new Rectangle(Left, Top, Width, (int)MathF.Round(bottom - Top));

        spriteBatch.Draw(_pixel, plate, null, PlateColor,
            0f, Vector2.Zero, SpriteEffects.None, Layers.DialoguePlate);

        // A lit lip along the top edge so the plate reads as concrete, not as a rectangular shadow.
        spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, plate.Width, 2), null, PlateLip,
            0f, Vector2.Zero, SpriteEffects.None, Layers.TextShadow);

        Text.Draw(spriteBatch, _uiFont, speaker, Left + Width / 2f, NameY, speakerColour, Palette.Shadow, centred: true);

        // A short line beside a tall picture sits level with its middle.
        float centre = textLeft + textWidth / 2f;
        float top = LineY + (blockHeight - textHeight) / 2f;
        Color colour = live ? LineLive : LineSaid;

        for (int i = 0; i < lines.Length; i++)
        {
            Text.Draw(spriteBatch, _uiFont, lines[i], centre, top + i * _uiFont.LineSpacing, colour,
                Palette.Shadow, centred: true);
        }
    }

    /// <summary>Draws the dealt item's picture in the room <see cref="Draw"/> left for it. The pixel-art batch, so it stays crisp.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    /// <param name="item">The item in the player's hand.</param>
    public void DrawIcon(SpriteBatch spriteBatch, ItemId item) =>
        _items.Draw(spriteBatch, item, new Vector2(Left + Padding, LineY), IconScale, Layers.ButtonLabel);
}
