using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheBlackBox.Audio;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The save form as a popup over the title. You pick a slot and it sends the run where it needs to go.
/// </summary>
/// <remarks>
/// A slot with no name goes to the customization screen first. A named one goes to the lobby, or
/// straight back to the table if you left in the middle of a round (see RunSession.IsAtTable).
/// </remarks>
public class SaveSlotScreen : GameScreen
{
    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.25);

    private readonly SaveSlotMenu _menu = new(new Rectangle(0, 0, BlackBoxGame.ScreenWidth, BlackBoxGame.ScreenHeight));

    private readonly InputAction _up = new(new[] { Buttons.DPadUp, Buttons.LeftThumbstickUp }, new[] { Keys.Up, Keys.W }, true);
    private readonly InputAction _down = new(new[] { Buttons.DPadDown, Buttons.LeftThumbstickDown }, new[] { Keys.Down, Keys.S }, true);
    private readonly InputAction _left = new(new[] { Buttons.DPadLeft, Buttons.LeftThumbstickLeft }, new[] { Keys.Left, Keys.A }, true);
    private readonly InputAction _right = new(new[] { Buttons.DPadRight, Buttons.LeftThumbstickRight }, new[] { Keys.Right, Keys.D }, true);
    private readonly InputAction _select = new(new[] { Buttons.A }, new[] { Keys.Enter, Keys.Space }, true);
    private readonly InputAction _cancel = new(new[] { Buttons.B, Buttons.Back }, new[] { Keys.Escape }, true);

    private ContentManager _content;
    private AudioManager _audio;

    /// <summary>Hooks up what happens when a slot is picked or the form is closed.</summary>
    public SaveSlotScreen()
    {
        IsPopup = true;
        TransitionOnTime = FadeTime;
        TransitionOffTime = FadeTime;

        _menu.SlotChosen += Begin;
        _menu.Closed += () =>
        {
            _audio.Play(Sfx.MenuBack);
            ExitScreen();
        };
    }

    /// <summary>Loads the form and opens it with whatever is on disk right now.</summary>
    public override void Activate()
    {
        _content ??= new ContentManager(ScreenManager.Game.Services, "Content");
        _audio = ScreenManager.Game.Services.GetService<AudioManager>();

        _menu.LoadContent(_content, ScreenManager.GraphicsDevice);
        _menu.Open();
    }

    /// <summary>Unloads the form.</summary>
    public override void Unload() => _content?.Unload();

    /// <summary>Moves around the form with the keyboard, follows the mouse, and backs out on Escape.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="input">The input this frame.</param>
    public override void HandleInput(GameTime gameTime, InputState input)
    {
        if (!_menu.IsOpen) return;

        bool moved =
            input.MouseMoved && _menu.FollowMouse(input.MousePosition) ||
            _up.Occurred(input) && _menu.Navigate(-1, 0) ||
            _down.Occurred(input) && _menu.Navigate(1, 0) ||
            _left.Occurred(input) && _menu.Navigate(0, -1) ||
            _right.Occurred(input) && _menu.Navigate(0, 1);

        if (moved) _audio.Play(Sfx.MenuMove);

        if (_select.Occurred(input)) _menu.Press();
        else if (_cancel.Occurred(input))
        {
            _menu.Close();
        }
    }

    /// <summary>Runs the form while it has focus.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above has the input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (IsActive) _menu.Update(gameTime);
    }

    /// <summary>Draws the form, faded with the transition.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;

        _menu.Opacity = TransitionAlpha;

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _menu.Draw(gameTime, spriteBatch);
        spriteBatch.End();
    }

    /// <summary>Sends the picked run to customization if it's new, or to wherever it was left.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="run">The run in it.</param>
    private void Begin(int slot, SaveData run)
    {
        _audio.Play(Sfx.MenuConfirm);

        var session = new RunSession(slot, run);

        if (string.IsNullOrWhiteSpace(run.PlayerName))
        {
            // The box stays up behind the customization screen, so only the menus leave.
            foreach (GameScreen screen in ScreenManager.GetScreens())
            {
                if (screen is not BoxBackgroundScreen) screen.ExitScreen();
            }

            ScreenManager.AddScreen(new CustomizationScreen(session));
            return;
        }

        LoadingScreen.Load(ScreenManager, null,
            session.IsAtTable ? new TableScreen(session) : new LobbyScreen(session));
    }
}
