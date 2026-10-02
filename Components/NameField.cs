using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The text field where the player types their name, on the customization screen.
/// </summary>
/// <remarks>
/// This used to be the NameEntry form. The name is just a row on the customization screen now, so
/// I kept the text field part and dropped the rest. Characters come from Window.TextInput instead
/// of Keyboard.GetState so I don't have to deal with shift, caps lock and keyboard layouts.
/// </remarks>
public class NameField
{
    /// <summary>The caret: gap after the text, how far down from the top, width, and height as a fraction of the line.</summary>
    private const int CaretGap = 3;
    private const int CaretInset = 4;
    private const int CaretWidth = 2;
    private const float CaretHeight = 0.78f;

    /// <summary>How long the caret stays on, then off.</summary>
    private const double CaretBlink = 0.53;

    /// <summary>Max name length. Short enough to fit on a plate and in a line of dialogue.</summary>
    public const int MaxLength = 16;

    private static readonly Color RuleColor = new(122, 108, 106);
    private static readonly Color TypedColor = new(240, 232, 226);
    private static readonly Color EmptyColor = new(110, 102, 104);

    private readonly StringBuilder _typed = new();
    private SpriteFont _font;
    private Texture2D _pixel;
    private double _caretTimer;

    /// <summary>Where the field is in screen pixels. The text is centred in it and the underline runs along the bottom.</summary>
    public Rectangle Bounds { get; set; }

    /// <summary>What shows when nothing has been typed yet.</summary>
    public string Placeholder { get; set; } = "TYPE A NAME";

    /// <summary>Whether the field is taking typing. The caret only shows when it is.</summary>
    public bool Focused { get; set; }

    /// <summary>What has been typed so far, trimmed.</summary>
    public string Name => _typed.ToString().Trim();

    /// <summary>Whether the name is good enough to start a run with.</summary>
    public bool IsValid => Name.Length > 0;

    /// <summary>Loads the font and the pixel for the underline and caret.</summary>
    /// <param name="content">The content manager to load with.</param>
    /// <param name="graphicsDevice">The device, for the pixel.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _font = content.Load<SpriteFont>("spectral-ui");

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Takes one character from the window. Handles backspace, and ignores other control characters.</summary>
    /// <param name="character">The character the window reported.</param>
    /// <returns>True if the name changed.</returns>
    public bool TypeCharacter(char character)
    {
        if (!Focused) return false;

        int before = _typed.Length;

        if (character == '\b')
        {
            if (_typed.Length > 0) _typed.Length--;
        }
        else
        {
            if (char.IsControl(character)) return false;
            if (_typed.Length >= MaxLength) return false;

            // No leading or double spaces.
            if (character == ' ' && (_typed.Length == 0 || _typed[^1] == ' ')) return false;

            _typed.Append(character);
        }

        // Restart the blink so the caret stays solid while typing.
        _caretTimer = 0;
        return _typed.Length != before;
    }

    /// <summary>Runs the caret blink.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime) => _caretTimer += gameTime.ElapsedGameTime.TotalSeconds;

    /// <summary>Draws the name (or the placeholder), the underline, and the caret.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="opacity">How far the screen has faded in, 0 to 1.</param>
    public void Draw(SpriteBatch spriteBatch, float opacity)
    {
        // The underline, so an empty field still looks like somewhere to type.
        var rule = new Rectangle(Bounds.Left, Bounds.Bottom, Bounds.Width, 2);
        spriteBatch.Draw(_pixel, rule, null, (Focused ? ButtonSprite.Amber : RuleColor) * opacity,
            0f, Vector2.Zero, SpriteEffects.None, Layers.Text);

        string text = _typed.ToString();
        bool empty = text.Length == 0;
        Vector2 size = _font.MeasureString(empty ? Placeholder : text);

        float left = MathF.Round(Bounds.Center.X - size.X / 2f);
        float top = MathF.Round(Bounds.Center.Y - size.Y / 2f);

        spriteBatch.DrawString(_font, empty ? Placeholder : text, new Vector2(left, top) + Palette.ShadowOffset,
            Palette.Shadow * opacity, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
        spriteBatch.DrawString(_font, empty ? Placeholder : text, new Vector2(left, top),
            (empty ? EmptyColor : TypedColor) * opacity, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);

        if (!Focused || _caretTimer % (CaretBlink * 2) >= CaretBlink) return;

        float caretX = empty ? Bounds.Center.X : left + size.X + CaretGap;
        var caret = new Rectangle((int)caretX, (int)top + CaretInset, CaretWidth, (int)MathF.Round(_font.LineSpacing * CaretHeight));
        spriteBatch.Draw(_pixel, caret, null, ButtonSprite.Amber * opacity,
            0f, Vector2.Zero, SpriteEffects.None, Layers.Text);
    }
}
