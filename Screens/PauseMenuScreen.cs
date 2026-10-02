using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// Escape in the lobby or at the table: RESUME, or save and go back to the title. It's a popup so
/// the paused screen stays drawn underneath.
/// </summary>
/// <remarks>
/// The screen underneath stops itself while this has focus (the IsActive check in its Update), so
/// the discussion clock doesn't keep running while you're paused.
/// </remarks>
public class PauseMenuScreen : MenuScreen
{
    private const string Heading = "PAUSED";
    private const string Note = "THE BOX WILL WAIT. IT ALWAYS DOES.";
    private const string ResumeLabel = "RESUME";
    private const string TitleLabel = "RETURN TO TITLE";

    private const int PanelWidth = 700;
    private const float EntryGap = 16f;
    private const float HeadingGap = 26f;
    private const float StatusGap = 14f;

    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.2);
    private static readonly Color NoteColor = new(132, 122, 124);

    private readonly Func<string> _leave;
    private readonly FormPanel _panel = new();

    private SpriteFont _font;
    private SpriteFont _detailFont;
    private Rectangle _panelBounds;
    private float _noteY;
    private float _statusY;

    /// <summary>Why the save failed on the way out, or null.</summary>
    private string _status;

    /// <summary>Builds the menu.</summary>
    /// <param name="leave">Saves the run on the way out. Returns why it couldn't, or null.</param>
    public PauseMenuScreen(Func<string> leave)
    {
        _leave = leave;

        IsPopup = true;
        TransitionOnTime = FadeTime;
        TransitionOffTime = FadeTime;

        // Amber keeps playing, red is the way out.
        var resume = new MenuEntry(ResumeLabel, ButtonSprite.Amber);
        var title = new MenuEntry(TitleLabel, ButtonSprite.EmberRed);

        resume.Selected += ExitScreen;
        title.Selected += ReturnToTitle;

        MenuEntries.Add(resume);
        MenuEntries.Add(title);
    }

    /// <summary>Loads the form, lays it out and plays the pause sound.</summary>
    public override void Activate()
    {
        base.Activate();

        _panel.LoadContent(Content, ScreenManager.GraphicsDevice);
        _font = Content.Load<SpriteFont>("spectral-ui");
        _detailFont = Content.Load<SpriteFont>("spectral-detail");

        LayOut();
        Audio.Play(Sfx.Pause);
    }

    /// <summary>Pressing Escape again is the same as RESUME.</summary>
    protected override void OnCancel()
    {
        Audio.Play(Sfx.MenuBack);
        ExitScreen();
    }

    /// <summary>Draws the veil, the panel and its lines, then the entries.</summary>
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
        if (_status is not null)
        {
            Text.Draw(spriteBatch, _detailFont, _status, _panelBounds.Center.X, _statusY,
                ButtonSprite.EmberRed * alpha, Palette.FormShadow * alpha, centred: true);
        }
        spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>For the proof run, since it can't press keys: RETURN TO TITLE.</summary>
    internal void ProofReturnToTitle() => ReturnToTitle();

    /// <summary>Saves and goes back to the title. If the save fails you stay where you are.</summary>
    private void ReturnToTitle()
    {
        _status = _leave();
        if (_status is not null)
        {
            _status = "COULD NOT SAVE -- " + _status;
            return;
        }

        LoadingScreen.Load(ScreenManager, null, new BoxBackgroundScreen(), new TitleScreen());
    }

    /// <summary>Sizes the panel to fit everything and centres it.</summary>
    private void LayOut()
    {
        float entries = -EntryGap;
        foreach (MenuEntry entry in MenuEntries) entries += entry.Plate.Size.Y + EntryGap;

        int height = (int)MathF.Round(FormPanel.Padding + _font.LineSpacing + HeadingGap / 2f
            + _detailFont.LineSpacing + HeadingGap + entries + StatusGap + _detailFont.LineSpacing + FormPanel.Padding);

        _panelBounds = new Rectangle(
            (BlackBoxGame.ScreenWidth - PanelWidth) / 2,
            (BlackBoxGame.ScreenHeight - height) / 2,
            PanelWidth,
            height);

        _noteY = _panelBounds.Top + FormPanel.Padding + _font.LineSpacing + HeadingGap / 2f;

        float top = _noteY + _detailFont.LineSpacing + HeadingGap;
        LayOutColumn(new Vector2(_panelBounds.Center.X, top + entries / 2f), EntryGap);

        _statusY = top + entries + StatusGap;
    }
}
