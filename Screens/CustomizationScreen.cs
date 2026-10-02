using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheBlackBox.Characters;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// Before a new run starts: who the player is, their colours, what they wear, and their name.
/// A preview walks in place and turns, in whatever has been picked.
/// </summary>
/// <remarks>
/// Every option is a plate on a MenuScreen, so the keyboard walks it the same way as the title
/// and left and right change the value. The name row is the exception: while it is picked,
/// letters type instead of steering, and only the arrows leave it. The box stays up behind the
/// veil, watching.
/// </remarks>
public partial class CustomizationScreen : MenuScreen
{
    private const string Heading = "WHO ARE YOU?";
    private const string Note = "THE BOX WILL REMEMBER IT. SO WILL THEY.";
    private const string BeginLabel = "SIT DOWN";
    private const string BackLabel = "BACK";

    /// <summary>What each conscript and each accessory is called on the plates.</summary>
    private static readonly string[] ConscriptNames = { "THE JACKET", "THE COAT" };
    private static readonly string[] AccessoryNames = { "NOTHING", "A SCARF", "A CAP" };

    // The panel on the right with the options on it.
    private static readonly Rectangle PanelBounds = new(800, 100, 740, 660);
    private static readonly Point RowSize = new(560, 62);
    private const float RowGap = 10f;
    private const float RowsTop = 196f;

    /// <summary>The rows sit left of the panel's middle, so the colour swatches fit to their right.</summary>
    private const float RowsShift = -40f;
    private static readonly Point ButtonSize = new(300, 74);
    private const float ButtonGap = 40f;

    /// <summary>Where the preview stands, and how big. Twice the lobby's size, so the colours can be read.</summary>
    private static readonly Vector2 PreviewFeet = new(420f, 700f);
    private const float PreviewScale = 6f;

    /// <summary>How long the preview faces each way before it turns.</summary>
    private const float TurnSeconds = 1.6f;

    /// <summary>The pool of light the preview stands in.</summary>
    private static readonly Point PoolSize = new(420, 110);
    private static readonly Color PoolColor = new Color(232, 214, 170) * 0.22f;
    private const float NoteY = 770f;

    /// <summary>Where the box hangs while the player is dressed: over the preview's shoulder, at the size it sits on the table.</summary>
    /// <remarks>Left in the middle of the screen it was half under the panel, which looked like a mistake rather than a choice.</remarks>
    private static readonly Vector2 BoxAt = new(420f, 250f);

    /// <summary>How dark the veil over the box is. Darker than a form's, since the preview has to read against it.</summary>
    private const float VeilOpacity = 0.86f;

    /// <summary>A colour swatch beside a row: how big a square, the gap between them, and how far off the plate.</summary>
    private const int SwatchSize = 14;
    private const int SwatchGap = 3;
    private const int SwatchOffset = 14;

    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.5);
    private static readonly Color NoteColor = new(132, 122, 124);
    private static readonly Direction[] TurnOrder = { Direction.Down, Direction.Left, Direction.Up, Direction.Right };

    private readonly RunSession _session;
    private readonly PlayerLook _look;
    private readonly FormPanel _panel = new();
    private readonly NameField _name = new();
    private readonly CharacterPalettes _palettes = new();
    private readonly WalkerSprite _preview = new();
    private readonly Texture2D[] _sheets = new Texture2D[2];

    private readonly MenuEntry _conscript;
    private readonly MenuEntry _hair;
    private readonly MenuEntry _outfit;
    private readonly MenuEntry _accent;
    private readonly MenuEntry _wearing;
    private readonly MenuEntry _nameRow;
    private readonly MenuEntry _begin;

    /// <summary>The arrows still steer while the name row is picked. Letters do not.</summary>
    private readonly InputAction _arrowUp = new(new[] { Buttons.DPadUp }, new[] { Keys.Up }, true);
    private readonly InputAction _arrowDown = new(new[] { Buttons.DPadDown }, new[] { Keys.Down }, true);
    private readonly InputAction _enter = new(new[] { Buttons.A }, new[] { Keys.Enter }, true);
    private readonly InputAction _escape = new(new[] { Buttons.B, Buttons.Back }, new[] { Keys.Escape }, true);

    private SpriteFont _font;
    private SpriteFont _detailFont;
    private Texture2D _glow;
    private Texture2D _recoloured;
    private BoxScene _scene;
    private float _turnClock;

    /// <summary>Why the save failed when the player tried to sit down, or null.</summary>
    private string _status;

    /// <summary>Builds the rows for a run that has no name yet.</summary>
    /// <param name="session">The run, and the slot it goes back into.</param>
    public CustomizationScreen(RunSession session)
    {
        _session = session;
        _look = session.Run.Look.Clone();

        TransitionOnTime = FadeTime;
        TransitionOffTime = FadeTime;

        _conscript = Option(step => _look.Character = ((Conscript)CharacterPalettes.Wrap((int)_look.Who + step, ConscriptNames.Length)).ToString());
        _hair = Option(step => _look.Hair = CharacterPalettes.Wrap(_look.Hair + step, CharacterPalettes.HairNames.Length));
        _outfit = Option(step => _look.Outfit = CharacterPalettes.Wrap(_look.Outfit + step, CharacterPalettes.OutfitNames.Length));
        _accent = Option(step => _look.Accent = CharacterPalettes.Wrap(_look.Accent + step, CharacterPalettes.AccentNames.Length));
        _wearing = Option(step => _look.Accessory = ((Accessory)CharacterPalettes.Wrap((int)_look.Wearing + step, AccessoryNames.Length)).ToString());

        // The name row is a plate with the field drawn over it. Picking it does nothing; typing does.
        _nameRow = new MenuEntry(string.Empty, ButtonSprite.Amber, RowSize);
        MenuEntries.Add(_nameRow);

        _begin = new MenuEntry(BeginLabel, ButtonSprite.Amber, ButtonSize);
        _begin.Selected += Begin;
        MenuEntries.Add(_begin);

        var back = new MenuEntry(BackLabel, ButtonSprite.BoneWhite, ButtonSize);
        back.Selected += GoBack;
        MenuEntries.Add(back);
    }

    /// <summary>Loads the sheets, the palettes and the form, and puts the preview in the starting colours.</summary>
    public override void Activate()
    {
        base.Activate();

        _panel.LoadContent(Content, ScreenManager.GraphicsDevice);
        _name.LoadContent(Content, ScreenManager.GraphicsDevice);
        _palettes.LoadContent(Content);
        _preview.LoadContent(Content);
        _font = Content.Load<SpriteFont>("spectral-ui");
        _detailFont = Content.Load<SpriteFont>("spectral-detail");
        _glow = Content.Load<Texture2D>("glow");

        _scene = ScreenManager.Game.Services.GetService<BoxScene>();
        _scene.Box.Position = BoxAt;
        _scene.Box.Scale = BlackBoxSprite.TableScale;

        _sheets[(int)Conscript.First] = Content.Load<Texture2D>("conscript-first");
        _sheets[(int)Conscript.Second] = Content.Load<Texture2D>("conscript-second");

        LayOut();
        Refresh();
    }

    /// <summary>Lets go of the recoloured sheet, which is not the content manager's to unload.</summary>
    public override void Unload()
    {
        _recoloured?.Dispose();
        base.Unload();
    }

    /// <summary>Steers the menu, except on the name row, where letters type and only the arrows leave.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="input">The input this frame.</param>
    public override void HandleInput(GameTime gameTime, InputState input)
    {
        if (SelectedEntry != MenuEntries.IndexOf(_nameRow) || input.MouseMoved && !_nameRow.Plate.Bounds.Contains(input.MousePosition))
        {
            base.HandleInput(gameTime, input);
            return;
        }

        if (_arrowUp.Occurred(input)) Step(-1);
        else if (_arrowDown.Occurred(input) || _enter.Occurred(input)) Step(1);
        else if (_escape.Occurred(input)) OnCancel();
    }

    /// <summary>Typing goes into the name, but only while its row is picked.</summary>
    /// <param name="character">The character the window reported.</param>
    public override void HandleTextInput(char character)
    {
        if (!_name.Focused) return;

        if (_name.TypeCharacter(character)) Audio.Play(Sfx.Type);
        _begin.Plate.Enabled = _name.IsValid;
    }

    /// <summary>Runs the menu, the caret, and the preview turning round.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above has the input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        _name.Focused = IsActive && SelectedEntry == MenuEntries.IndexOf(_nameRow);
        _name.Update(gameTime);

        _turnClock += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _preview.Facing = TurnOrder[(int)(_turnClock / TurnSeconds) % TurnOrder.Length];
        _preview.IsWalking = true;
        _preview.Update(gameTime);
    }

    /// <summary>Escape is BACK.</summary>
    protected override void OnCancel()
    {
        Audio.Play(Sfx.MenuBack);
        GoBack();
    }

    /// <summary>Draws the veil over the box, the preview in its pool of light, the panel and its plates, and the name.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        float alpha = TransitionAlpha;

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        spriteBatch.Draw(ScreenManager.BlankTexture, new Rectangle(0, 0, BlackBoxGame.ScreenWidth, BlackBoxGame.ScreenHeight), null,
            Palette.Veil * (VeilOpacity * alpha), 0f, Vector2.Zero, SpriteEffects.None, Layers.Veil);
        _panel.Draw(spriteBatch, PanelBounds, alpha);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp);
        var pool = new Rectangle((int)PreviewFeet.X - PoolSize.X / 2, (int)PreviewFeet.Y - PoolSize.Y / 2, PoolSize.X, PoolSize.Y);
        spriteBatch.Draw(_glow, pool, PoolColor * alpha);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _preview.Draw(spriteBatch, 0.5f, PreviewScale);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Text.Draw(spriteBatch, _font, Heading, PanelBounds.Center.X, PanelBounds.Top + FormPanel.Padding,
            Palette.Heading * alpha, Palette.FormShadow * alpha, centred: true);
        Text.Draw(spriteBatch, _detailFont, _status ?? Note, PreviewFeet.X, NoteY,
            (_status is null ? NoteColor : ButtonSprite.EmberRed) * alpha, Palette.FormShadow * alpha, centred: true);
        spriteBatch.End();

        base.Draw(gameTime);

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        DrawSwatch(spriteBatch, _hair, _palettes.Hair(_look.Hair), alpha);
        DrawSwatch(spriteBatch, _outfit, _palettes.Outfit(_look.Outfit), alpha);
        DrawSwatch(spriteBatch, _accent, _palettes.Accent(_look.Accent), alpha);
        _name.Draw(spriteBatch, alpha);
        spriteBatch.End();
    }

    /// <summary>Makes one adjustable row. Clicking it steps it forward, the same as right.</summary>
    /// <param name="change">Moves the value by -1 or 1.</param>
    private MenuEntry Option(Action<int> change)
    {
        var entry = new MenuEntry(string.Empty, ButtonSprite.BoneWhite, RowSize);
        entry.Adjusted += step =>
        {
            change(step);
            Refresh();
        };
        entry.Selected += () =>
        {
            change(1);
            Audio.Play(Sfx.OptionChange);
            Refresh();
        };
        MenuEntries.Add(entry);
        return entry;
    }

    /// <summary>Puts every value on its plate and the new colours on the preview.</summary>
    private void Refresh()
    {
        // Wrapped, so a save edited by hand to something out of range still shows a name.
        _look.Hair = CharacterPalettes.Wrap(_look.Hair, CharacterPalettes.HairNames.Length);
        _look.Outfit = CharacterPalettes.Wrap(_look.Outfit, CharacterPalettes.OutfitNames.Length);
        _look.Accent = CharacterPalettes.Wrap(_look.Accent, CharacterPalettes.AccentNames.Length);

        _conscript.Text = Row("CONSCRIPT", ConscriptNames[(int)_look.Who]);
        _hair.Text = Row("HAIR", CharacterPalettes.HairNames[_look.Hair]);
        _outfit.Text = Row("OUTFIT", CharacterPalettes.OutfitNames[_look.Outfit]);
        _accent.Text = Row("ACCENT", CharacterPalettes.AccentNames[_look.Accent]);
        _wearing.Text = Row("WEARING", AccessoryNames[(int)_look.Wearing]);
        _begin.Plate.Enabled = _name.IsValid;

        _recoloured?.Dispose();
        _recoloured = _palettes.Recolour(ScreenManager.GraphicsDevice, _sheets[(int)_look.Who], _look);
        _preview.Sheet = _recoloured;
        _preview.Wearing = _look.Wearing;
    }

    /// <summary>A row's label: what it is, then the value between the arrows that change it.</summary>
    private static string Row(string name, string value) => name + "   <  " + value + "  >";

    /// <summary>Moves the selection from the name row, which steers itself.</summary>
    /// <param name="step">-1 for up, 1 for down.</param>
    private void Step(int step)
    {
        SelectedEntry += step;
        Audio.Play(Sfx.MenuMove);
    }

    /// <summary>Writes the name and the look to the run, saves it, and goes down to the lobby.</summary>
    private void Begin()
    {
        if (!_name.IsValid) return;

        _session.Run.PlayerName = _name.Name;
        _session.Run.Look = _look.Clone();

        // Saved straight away so the name is never asked for twice. A failed write keeps the player here.
        if (!_session.Save())
        {
            _status = "COULD NOT SAVE -- " + _session.LastError;
            return;
        }

        LoadingScreen.Load(ScreenManager, null, new LobbyScreen(_session));
    }

    /// <summary>Back to the title, leaving the slot claimed but unnamed, the way the old name form did.</summary>
    private void GoBack()
    {
        _scene.PlaceOnTitle();
        ExitScreen();
        ScreenManager.AddScreen(new TitleScreen());
    }

    /// <summary>Lays the rows down the panel, the two buttons side by side under them, and the name field into its row.</summary>
    private void LayOut()
    {
        float y = RowsTop;
        foreach (MenuEntry entry in new[] { _conscript, _hair, _outfit, _accent, _wearing, _nameRow })
        {
            entry.Plate.Center = new Vector2(PanelBounds.Center.X + RowsShift, y + RowSize.Y / 2f);
            y += RowSize.Y + RowGap;
        }

        float buttonsY = y + RowGap + ButtonSize.Y / 2f;
        MenuEntries[^2].Plate.Center = new Vector2(PanelBounds.Center.X - (ButtonSize.X + ButtonGap) / 2f, buttonsY);
        MenuEntries[^1].Plate.Center = new Vector2(PanelBounds.Center.X + (ButtonSize.X + ButtonGap) / 2f, buttonsY);

        Rectangle row = _nameRow.Plate.Bounds;
        _name.Bounds = new Rectangle(row.X + row.Width / 6, row.Y + 8, row.Width * 2 / 3, row.Height - 18);
        _preview.Position = PreviewFeet;
    }

    /// <summary>Draws a ramp's shades as little squares beside a row, so the colour is seen and not just named.</summary>
    private void DrawSwatch(SpriteBatch spriteBatch, MenuEntry entry, IReadOnlyList<Color> ramp, float alpha)
    {
        Rectangle plate = entry.Plate.Bounds;
        int x = plate.Right + SwatchOffset;
        int y = plate.Center.Y - SwatchSize / 2;

        for (int i = 0; i < ramp.Count; i++)
        {
            var square = new Rectangle(x + i * (SwatchSize + SwatchGap), y, SwatchSize, SwatchSize);
            spriteBatch.Draw(ScreenManager.BlankTexture, square, null, ramp[i] * alpha, 0f, Vector2.Zero, SpriteEffects.None, Layers.ButtonLabel);
        }
    }
}
