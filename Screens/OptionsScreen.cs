using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The three volumes, on a form over the title. Left and right turn one down or up.
/// </summary>
/// <remarks>
/// The change is heard straight away, since AudioManager reads the settings every frame. The
/// file is only written once the form closes, not on every press.
/// </remarks>
public class OptionsScreen : MenuScreen
{
    private const string Heading = "OPTIONS";
    private const string Note = "LEFT AND RIGHT TO TURN IT DOWN OR UP";
    private const string MasterLabel = "MASTER";
    private const string MusicLabel = "MUSIC";
    private const string SoundLabel = "SOUND";
    private const string BackLabel = "BACK";

    /// <summary>How much one press moves a volume.</summary>
    private const float VolumeStep = 0.1f;

    private const int PanelWidth = 760;
    private static readonly Point EntrySize = new(520, 78);
    private const float EntryGap = 14f;
    private const float HeadingGap = 30f;

    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.25);
    private static readonly Color NoteColor = new(132, 122, 124);

    private readonly FormPanel _panel = new();
    private readonly MenuEntry _master;
    private readonly MenuEntry _music;
    private readonly MenuEntry _sound;

    private SpriteFont _font;
    private SpriteFont _detailFont;
    private Rectangle _panelBounds;
    private float _noteY;

    /// <summary>Builds the three volume entries and BACK.</summary>
    public OptionsScreen()
    {
        IsPopup = true;
        TransitionOnTime = FadeTime;
        TransitionOffTime = FadeTime;

        _master = new MenuEntry(string.Empty, ButtonSprite.Amber, EntrySize);
        _music = new MenuEntry(string.Empty, ButtonSprite.Amber, EntrySize);
        _sound = new MenuEntry(string.Empty, ButtonSprite.Amber, EntrySize);
        var back = new MenuEntry(BackLabel, ButtonSprite.BoneWhite);

        _master.Adjusted += step => Change(v => Audio.Settings.MasterVolume = v, Audio.Settings.MasterVolume, step);
        _music.Adjusted += step => Change(v => Audio.Settings.MusicVolume = v, Audio.Settings.MusicVolume, step);
        _sound.Adjusted += step => Change(v => Audio.Settings.SfxVolume = v, Audio.Settings.SfxVolume, step);
        back.Selected += Close;

        MenuEntries.Add(_master);
        MenuEntries.Add(_music);
        MenuEntries.Add(_sound);
        MenuEntries.Add(back);
    }

    /// <summary>Loads the form and lays it out in the middle of the screen.</summary>
    public override void Activate()
    {
        base.Activate();

        _panel.LoadContent(Content, ScreenManager.GraphicsDevice);
        _font = Content.Load<SpriteFont>("spectral-ui");
        _detailFont = Content.Load<SpriteFont>("spectral-detail");

        Relabel();
        LayOut();
    }

    /// <summary>Escape is the same as BACK.</summary>
    protected override void OnCancel()
    {
        Audio.Play(Sfx.MenuBack);
        Close();
    }

    /// <summary>Draws the veil, the panel, the heading and the note, then the entries.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        float alpha = TransitionAlpha;

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _panel.DrawVeil(spriteBatch, alpha);
        _panel.Draw(spriteBatch, _panelBounds, alpha);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Text.Draw(spriteBatch, _font, Heading, _panelBounds.Center.X, _panelBounds.Top + FormPanel.Padding,
            Palette.Heading * alpha, Palette.FormShadow * alpha, centred: true);
        Text.Draw(spriteBatch, _detailFont, Note, _panelBounds.Center.X, _noteY,
            NoteColor * alpha, Palette.FormShadow * alpha, centred: true);
        spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>Moves one volume a step, and puts the new number on the plates.</summary>
    /// <param name="set">Writes the new value back to the settings.</param>
    /// <param name="value">The value now.</param>
    /// <param name="step">-1 or 1.</param>
    private void Change(Action<float> set, float value, int step)
    {
        // Rounded to the step, so ten presses always land back on a clean number.
        float next = MathF.Round((value + step * VolumeStep) / VolumeStep) * VolumeStep;
        set(Math.Clamp(next, 0f, 1f));
        Relabel();
    }

    /// <summary>Writes each volume onto its plate.</summary>
    private void Relabel()
    {
        _master.Text = Label(MasterLabel, Audio.Settings.MasterVolume);
        _music.Text = Label(MusicLabel, Audio.Settings.MusicVolume);
        _sound.Text = Label(SoundLabel, Audio.Settings.SfxVolume);
    }

    /// <summary>A plate's label: the name, then the volume as a percentage between the two arrows that change it.</summary>
    private static string Label(string name, float value) => string.Format(CultureInfo.InvariantCulture,
        "{0}   <  {1}%  >", name, (int)MathF.Round(value * 100f));

    /// <summary>Saves the settings and closes the form.</summary>
    private void Close()
    {
        SettingsStore.Save(Audio.Settings);
        ExitScreen();
    }

    /// <summary>Sizes the panel around the heading, the entries and the note, and centres it.</summary>
    private void LayOut()
    {
        float entries = -EntryGap;
        foreach (MenuEntry entry in MenuEntries) entries += entry.Plate.Size.Y + EntryGap;

        int height = (int)MathF.Round(FormPanel.Padding + _font.LineSpacing + HeadingGap
            + entries + HeadingGap + _detailFont.LineSpacing + FormPanel.Padding);

        _panelBounds = new Rectangle(
            (BlackBoxGame.ScreenWidth - PanelWidth) / 2,
            (BlackBoxGame.ScreenHeight - height) / 2,
            PanelWidth,
            height);

        float top = _panelBounds.Top + FormPanel.Padding + _font.LineSpacing + HeadingGap;
        LayOutColumn(new Vector2(_panelBounds.Center.X, top + entries / 2f), EntryGap);

        _noteY = top + entries + HeadingGap;
    }
}
