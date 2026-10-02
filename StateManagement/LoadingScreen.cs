// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.StateManagement;

/// <summary>
/// Goes between two sets of screens. Every screen on the stack leaves, and once they've all
/// transitioned off the next screens get added.
/// </summary>
/// <remarks>
/// Same as the sample except the "Loading..." text is optional and my own, so going through the
/// door can put a line on the black screen in between.
/// </remarks>
public class LoadingScreen : GameScreen
{
    /// <summary>How long the line takes to fade in, and how long it stays up before the next screens come in.</summary>
    private const float LineFadeSeconds = 0.6f;
    private const float LineHoldSeconds = 1.4f;

    private static readonly Color LineColor = new(170, 158, 160);

    private readonly GameScreen[] _screensToLoad;
    private readonly string _line;

    private bool _otherScreensAreGone;
    private float _held;
    private SpriteFont _font;
    private ContentManager _content;

    private LoadingScreen(string line, GameScreen[] screensToLoad)
    {
        _line = line;
        _screensToLoad = screensToLoad;
    }

    /// <summary>Exits every screen on the stack and loads the new ones once they're gone.</summary>
    /// <param name="screenManager">The manager.</param>
    /// <param name="line">A line to show in between, or null for none.</param>
    /// <param name="screensToLoad">The screens to add, bottom first.</param>
    public static void Load(ScreenManager screenManager, string line, params GameScreen[] screensToLoad)
    {
        foreach (GameScreen screen in screenManager.GetScreens())
            screen.ExitScreen();

        screenManager.AddScreen(new LoadingScreen(line, screensToLoad));
    }

    /// <summary>Loads the font for the line.</summary>
    public override void Activate()
    {
        if (_line is null) return;

        _content ??= new ContentManager(ScreenManager.Game.Services, "Content");
        _font = _content.Load<SpriteFont>("spectral-ui");
    }

    /// <summary>Unloads the font.</summary>
    public override void Unload() => _content?.Unload();

    /// <summary>Once everything else is gone (and the line has been up long enough) it swaps in the next screens.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above has the input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (!_otherScreensAreGone) return;

        if (_line is not null && _held < LineHoldSeconds)
        {
            _held += (float)gameTime.ElapsedGameTime.TotalSeconds;
            return;
        }

        ScreenManager.RemoveScreen(this);

        foreach (GameScreen screen in _screensToLoad)
        {
            if (screen is not null) ScreenManager.AddScreen(screen);
        }

        // Loading can take a second and the fixed timestep would try to catch up all at once, so tell it not to.
        ScreenManager.Game.ResetElapsedTime();
    }

    /// <summary>Checks if it's the only screen left, and draws the line if there is one.</summary>
    /// <remarks>Checked in Draw like the sample does, so the last frame of the old screens' fade gets drawn first.</remarks>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        if (ScreenState == ScreenState.Active && ScreenManager.GetScreens().Length == 1)
            _otherScreensAreGone = true;

        if (_font is null || !_otherScreensAreGone) return;

        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        float y = (BlackBoxGame.ScreenHeight - _font.LineSpacing) / 2f;

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        float alpha = MathF.Min(1f, _held / LineFadeSeconds);
        Text.DrawCentered(spriteBatch, _font, _line, y, LineColor * alpha, Palette.Shadow * alpha, Palette.ShadowOffset);
        spriteBatch.End();
    }
}
