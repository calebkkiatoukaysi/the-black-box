using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The line the player types their name into, with its caret. On the customization screen.
/// </summary>
/// <remarks>
/// This was the NameEntry form, which asked for a name on its own after a slot was picked. The
/// name is one row of the customization screen now, so the form is gone and the field is what
/// is left of it. Characters come from <see cref="GameWindow.TextInput"/> instead of
/// Keyboard.GetState, so I don't have to handle shift, caps lock and keyboard layouts myself.
/// </remarks>
public class NameField
{
    /// <summary>The caret: how far after the text it sits, how far down from the top, its width, and its height as a share of the line.</summary>
    private const int CaretGap = 3;
    private const int CaretInset = 4;
    private const int CaretWidth = 2;
    private const float CaretHeight = 0.78f;

    /// <summary>How long the caret spends visible, and then hidden.</summary>
    private const double CaretBlink = 0.53;

    /// <summary>How long a name may be. Short enough to fit on a plate and inside a line of dialogue.</summary>
    public const int MaxLength = 16;

    private static readonly Color RuleColor = new(122, 108, 106);
    private static readonly Color TypedColor = new(240, 232, 226);
    private static readonly Color EmptyColor = new(110, 102, 104);

    private readonly StringBuilder _typed = new();
    private SpriteFont _font;
    private Texture2D _pixel;
    private double _caretTimer;

    /// <summary>Where the field is, in screen pixels. The text centres in it and the rule runs along its bottom.</summary>
    public Rectangle Bounds { get; set; }

    /// <summary>What is shown while nothing has been typed.</summary>
    public string Placeholder { get; set; } = "TYPE A NAME";

    /// <summary>Whether the field is taking typing. The caret only shows while it is.</summary>
    public bool Focused { get; set; }

    /// <summary>What has been typed so far, trimmed.</summary>
    public string Name => _typed.ToString().Trim();

    /// <summary>Whether what has been typed is enough to start a run with.</summary>
    public bool IsValid => Name.Length > 0;

    /// <summary>Loads the font and the pixel the rule and the caret are drawn with.</summary>
    /// <param name="content">The content manager to load with.</param>
    /// <param name="graphicsDevice">The device, for the pixel.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _font = content.Load<SpriteFont>("spectral-ui");

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Takes one character from the window. Backspace is handled here; other control characters are dropped.</summary>
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

        // Restart the blink so the caret is solid while typing.
        _caretTimer = 0;
        return _typed.Length != before;
    }

    /// <summary>Runs the caret's blink.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime) => _caretTimer += gameTime.ElapsedGameTime.TotalSeconds;

    /// <summary>Draws what has been typed (or the placeholder), the rule under it, and the caret after it.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="opacity">How far the screen has faded in, 0 to 1.</param>
    public void Draw(SpriteBatch spriteBatch, float opacity)
    {
        // The rule under the field, so an empty field still looks like somewhere to type.
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
