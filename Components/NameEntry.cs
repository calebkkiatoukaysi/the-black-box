using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Asks a new run what the player is called.
/// </summary>
/// <remarks>
/// <para>
/// Opens once, over the title screen, when a slot is claimed that has no name on it yet. A
/// run that already has one never sees this again -- the name is on the save, so two slots
/// are two different people.
/// </para>
/// <para>
/// Characters arrive from <see cref="GameWindow.TextInput"/> rather than from reading
/// <c>Keyboard.GetState</c>. Reading the keyboard directly would mean owning the mapping from
/// key to character, which means owning shift, caps lock, dead keys and every keyboard layout
/// that is not the one this was written on. The window already knows all of that.
/// </para>
/// </remarks>
public class NameEntry
{
    /// <summary>Width and height of the single frame in panel.png.</summary>
    private const int PanelFrameSize = 32;

    /// <summary>Fixed corner of the panel's nine-slice. Matches <c>PANEL_CORNER</c> in the generator.</summary>
    private const int PanelCornerSize = 12;

    /// <summary>Blown up by the same whole number as the buttons that sit on it.</summary>
    private const float PanelScale = 3f;

    private const string Heading = "WHAT ARE YOU CALLED?";
    private const string Note = "THE BOX WILL REMEMBER IT. SO WILL THEY.";
    private const string BeginLabel = "SIT DOWN";
    private const string BackLabel = "BACK";

    private const int PanelWidth = 820;
    private const int PanelPadding = 40;
    private const int HeadingGap = 34;
    private const int FieldHeight = 64;
    private const int FieldGap = 26;

    /// <summary>How long a name may be.</summary>
    /// <remarks>
    /// Short enough that it still fits on a plate and inside a written line when it is
    /// substituted for <c>{name}</c>, which is the only real constraint on it.
    /// </remarks>
    public const int MaxLength = 16;

    /// <summary>How far the veil darkens the title screen behind the form.</summary>
    private const float VeilOpacity = 0.62f;

    /// <summary>How long the caret spends visible, and then hidden.</summary>
    private const double CaretBlink = 0.53;

    private Texture2D _panel;

    /// <summary>A single white pixel, for the veil, the rule under the field and the caret.</summary>
    private Texture2D _pixel;

    private SpriteFont _font;
    private SpriteFont _detailFont;

    private readonly Rectangle _screen;
    private readonly StringBuilder _typed = new();

    private ButtonSprite _beginButton;
    private ButtonSprite _backButton;

    private Rectangle _panelBounds;
    private Rectangle _fieldBounds;
    private float _noteY;

    private double _caretTimer;

    /// <summary>Whether the form is on screen and taking input.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>What has been typed so far, trimmed.</summary>
    public string Name => _typed.ToString().Trim();

    /// <summary>Whether what has been typed is enough to start a run with.</summary>
    public bool IsValid => Name.Length > 0;

    /// <summary>Raised when the player commits to a name.</summary>
    public event Action<string> Confirmed;

    /// <summary>Raised when the player backs out without naming anybody.</summary>
    public event Action Cancelled;

    /// <summary>Creates the form, centred against the screen it will be drawn over.</summary>
    /// <param name="screen">The whole window, in screen pixels.</param>
    public NameEntry(Rectangle screen)
    {
        _screen = screen;
    }

    /// <summary>Loads the panel, the fonts and the two buttons.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    /// <param name="graphicsDevice">The device, for the one pixel this is all drawn from.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _panel = content.Load<Texture2D>("panel");
        _font = content.Load<SpriteFont>("spectral-ui");
        _detailFont = content.Load<SpriteFont>("spectral-detail");

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _beginButton = new ButtonSprite(BeginLabel, Vector2.Zero, ButtonSprite.Amber);
        _backButton = new ButtonSprite(BackLabel, Vector2.Zero, ButtonSprite.BoneWhite);

        _beginButton.LoadContent(content);
        _backButton.LoadContent(content);

        _beginButton.Clicked += Commit;
        _backButton.Clicked += Close;

        LayOut();
    }

    /// <summary>Opens the form with an empty field.</summary>
    public void Open()
    {
        _typed.Clear();
        _caretTimer = 0;
        IsOpen = true;

        _beginButton.Reset();
        _backButton.Reset();
    }

    /// <summary>Closes the form and tells whoever opened it that nobody was named.</summary>
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        Cancelled?.Invoke();
    }

    /// <summary>
    /// Takes one character from the window.
    /// </summary>
    /// <remarks>
    /// Control characters are handled here rather than filtered out before they arrive, since
    /// backspace and return are the two most important keys on this form. Everything else that
    /// is not printable is dropped -- a name is not the place for a tab.
    /// </remarks>
    /// <param name="character">The character the window reported.</param>
    public void TypeCharacter(char character)
    {
        if (!IsOpen) return;

        switch (character)
        {
            case '\b':
                if (_typed.Length > 0) _typed.Length--;
                break;

            case '\r':
            case '\n':
                Commit();
                break;

            default:
                if (char.IsControl(character)) return;
                if (_typed.Length >= MaxLength) return;

                // A leading space is not a name, and nor is a double one.
                if (character == ' ' && (_typed.Length == 0 || _typed[^1] == ' ')) return;

                _typed.Append(character);
                break;
        }

        // Any edit restarts the blink, so the caret is solid while somebody is typing.
        _caretTimer = 0;
    }

    /// <summary>Runs the caret and the two buttons.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
        if (!IsOpen) return;

        _caretTimer += gameTime.ElapsedGameTime.TotalSeconds;

        // There is nobody to sit down as until something has been typed.
        _beginButton.Enabled = IsValid;

        _beginButton.Update(gameTime);
        _backButton.Update(gameTime);
    }

    /// <summary>Draws the form over whatever is behind it.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        if (!IsOpen) return;

        spriteBatch.Draw(_pixel, _screen, null, new Color(6, 5, 10) * VeilOpacity,
            0f, Vector2.Zero, SpriteEffects.None, Layers.Veil);

        NineSlice.Draw(spriteBatch, _panel, _panelBounds, Point.Zero,
            PanelFrameSize, PanelCornerSize, PanelScale, Color.White, Layers.PanelPlate);

        DrawCentered(spriteBatch, _font, Heading, _panelBounds.Top + PanelPadding, new Color(214, 206, 200));

        // The rule under the field, so the empty form still reads as somewhere to type.
        var rule = new Rectangle(_fieldBounds.Left, _fieldBounds.Bottom, _fieldBounds.Width, 2);
        spriteBatch.Draw(_pixel, rule, null, new Color(122, 108, 106),
            0f, Vector2.Zero, SpriteEffects.None, Layers.Text);

        DrawField(spriteBatch);
        DrawCentered(spriteBatch, _detailFont, Note, _noteY, new Color(132, 122, 124));

        _beginButton.Draw(gameTime, spriteBatch);
        _backButton.Draw(gameTime, spriteBatch);
    }

    /// <summary>Draws what has been typed, and the caret sitting after it.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    private void DrawField(SpriteBatch spriteBatch)
    {
        string text = _typed.ToString();
        Vector2 size = _font.MeasureString(text);

        float left = MathF.Round(_fieldBounds.Center.X - size.X / 2f);
        float top = MathF.Round(_fieldBounds.Center.Y - size.Y / 2f);

        if (text.Length > 0)
        {
            spriteBatch.DrawString(_font, text, new Vector2(left + 2f, top + 2f), Color.Black * 0.6f,
                0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
            spriteBatch.DrawString(_font, text, new Vector2(left, top), new Color(240, 232, 226),
                0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);
        }

        if (_caretTimer % (CaretBlink * 2) >= CaretBlink) return;

        var caret = new Rectangle((int)(left + size.X) + 3, (int)top + 4, 2, (int)MathF.Round(_font.LineSpacing * 0.78f));
        spriteBatch.Draw(_pixel, caret, null, ButtonSprite.Amber,
            0f, Vector2.Zero, SpriteEffects.None, Layers.Text);
    }

    /// <summary>Accepts the typed name, if there is one.</summary>
    private void Commit()
    {
        if (!IsOpen || !IsValid) return;

        IsOpen = false;
        Confirmed?.Invoke(Name);
    }

    /// <summary>
    /// Sizes the panel around its contents and centres it.
    /// </summary>
    /// <remarks>
    /// Run once, after the buttons have measured their own labels. Same reasoning as the title
    /// screen's row: laying out from measurements rather than constants means the form stays
    /// centred when a label changes.
    /// </remarks>
    private void LayOut()
    {
        int headingHeight = (int)MathF.Round(_font.LineSpacing);
        int noteHeight = (int)MathF.Round(_detailFont.LineSpacing);

        int height = PanelPadding + headingHeight + HeadingGap
                   + FieldHeight + FieldGap + noteHeight + HeadingGap
                   + _beginButton.Size.Y + PanelPadding;

        _panelBounds = new Rectangle(
            (_screen.Width - PanelWidth) / 2,
            (_screen.Height - height) / 2,
            PanelWidth,
            height);

        int fieldWidth = PanelWidth - PanelPadding * 4;
        int fieldTop = _panelBounds.Top + PanelPadding + headingHeight + HeadingGap;

        _fieldBounds = new Rectangle(
            _panelBounds.Left + (PanelWidth - fieldWidth) / 2, fieldTop, fieldWidth, FieldHeight);

        _noteY = _fieldBounds.Bottom + FieldGap;

        float row = _noteY + noteHeight + HeadingGap + _beginButton.Size.Y / 2f;
        float total = _beginButton.Size.X + 36 + _backButton.Size.X;
        float left = _panelBounds.Center.X - total / 2f;

        _beginButton.Center = new Vector2(left + _beginButton.Size.X / 2f, row);
        _backButton.Center = new Vector2(left + _beginButton.Size.X + 36 + _backButton.Size.X / 2f, row);
    }

    /// <summary>Draws a line centred across the panel, over a hard offset shadow.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="font">The SpriteFont to measure and render with.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="y">Top of the line, in screen pixels.</param>
    /// <param name="color">Colour of the text.</param>
    private void DrawCentered(SpriteBatch spriteBatch, SpriteFont font, string text, float y, Color color)
    {
        Vector2 size = font.MeasureString(text);
        var position = new Vector2(MathF.Round(_panelBounds.Center.X - size.X / 2f), MathF.Round(y));

        spriteBatch.DrawString(font, text, position + new Vector2(2f, 2f), Color.Black * 0.6f,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
        spriteBatch.DrawString(font, text, position, color,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);
    }
}
