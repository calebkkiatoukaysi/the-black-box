// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0,
// as presented in the CIS 580 textbook (Game Architecture > Game Screens).
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System;
using Microsoft.Xna.Framework;

namespace TheBlackBox.StateManagement;

/// <summary>
/// A screen is one layer of the game with its own update and draw. Screens stack on top of each other.
/// </summary>
public abstract class GameScreen
{
    /// <summary>Whether this screen is a popup.</summary>
    /// <remarks>A popup doesn't make the screen under it transition off, so that one stays drawn. (That's how the pause menu sits over the table.)</remarks>
    public bool IsPopup { get; protected set; }

    /// <summary>How long the screen takes to transition on.</summary>
    protected TimeSpan TransitionOnTime { get; set; } = TimeSpan.Zero;

    /// <summary>How long the screen takes to transition off.</summary>
    protected TimeSpan TransitionOffTime { get; set; } = TimeSpan.Zero;

    /// <summary>Where the screen is in its transition, 0 is fully on and 1 is fully off.</summary>
    protected float TransitionPosition { get; set; } = 1;

    /// <summary>The alpha for the transition. 1 when fully on, 0 when fully off.</summary>
    public float TransitionAlpha => 1f - TransitionPosition;

    /// <summary>The current state of the screen.</summary>
    public ScreenState ScreenState { get; set; } = ScreenState.TransitionOn;

    /// <summary>Whether the screen is leaving for good and not just covered.</summary>
    /// <remarks>If this is set the screen takes itself off the manager once it finishes transitioning off.</remarks>
    public bool IsExiting { get; protected internal set; }

    /// <summary>Whether this is the screen taking input right now.</summary>
    public bool IsActive => !_otherScreenHasFocus && (
        ScreenState == ScreenState.TransitionOn ||
        ScreenState == ScreenState.Active);

    private bool _otherScreenHasFocus;

    /// <summary>The ScreenManager in charge of this screen.</summary>
    public ScreenManager ScreenManager { get; internal set; }

    /// <summary>Called when the screen gets added to the manager. Load content here.</summary>
    public virtual void Activate() { }

    /// <summary>Called when the screen gets removed from the manager.</summary>
    public virtual void Deactivate() { }

    /// <summary>Unloads content for the screen. Called when it gets removed from the manager.</summary>
    public virtual void Unload() { }

    /// <summary>Runs the transition. This gets called every frame, even when the screen is covered or hidden.</summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above this one is taking input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top of this one.</param>
    public virtual void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        _otherScreenHasFocus = otherScreenHasFocus;

        if (IsExiting)
        {
            // Leaving for good, so transition off and then take it off the stack.
            ScreenState = ScreenState.TransitionOff;

            if (!UpdateTransitionPosition(gameTime, TransitionOffTime, 1))
                ScreenManager.RemoveScreen(this);
        }
        else if (coveredByOtherScreen)
        {
            // Something is on top of it, so transition off until it's hidden.
            ScreenState = UpdateTransitionPosition(gameTime, TransitionOffTime, 1)
                ? ScreenState.TransitionOff
                : ScreenState.Hidden;
        }
        else
        {
            // Otherwise transition on and become active.
            ScreenState = UpdateTransitionPosition(gameTime, TransitionOnTime, -1)
                ? ScreenState.TransitionOn
                : ScreenState.Active;
        }
    }

    /// <summary>Moves TransitionPosition along by however much time passed.</summary>
    /// <remarks>
    /// The textbook version checks >= 0 here on the way off, which is always true, so my fade outs
    /// were ending on the first frame. Changed it to >= 1.
    /// </remarks>
    /// <param name="gameTime">The game time.</param>
    /// <param name="time">How long the whole transition takes.</param>
    /// <param name="direction">-1 to transition on, 1 to transition off.</param>
    /// <returns>True while it's still transitioning, false once it's done.</returns>
    private bool UpdateTransitionPosition(GameTime gameTime, TimeSpan time, int direction)
    {
        float transitionDelta = time == TimeSpan.Zero
            ? 1
            : (float)(gameTime.ElapsedGameTime.TotalMilliseconds / time.TotalMilliseconds);

        TransitionPosition += transitionDelta * direction;

        if (direction < 0 && TransitionPosition <= 0 || direction > 0 && TransitionPosition >= 1)
        {
            TransitionPosition = MathHelper.Clamp(TransitionPosition, 0, 1);
            return false;
        }

        return true;
    }

    /// <summary>Handles input. Only gets called on the screen that has focus.</summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="input">The keyboard, mouse and gamepads, this frame and last.</param>
    public virtual void HandleInput(GameTime gameTime, InputState input) { }

    /// <summary>Takes one typed character. Only sent to the screen that has focus.</summary>
    /// <remarks>I added this one, it isn't in the tutorial. The name field needs real typed characters (shift, caps lock and all).</remarks>
    /// <param name="character">The character the window reported.</param>
    public virtual void HandleTextInput(char character) { }

    /// <summary>Draws the screen. Gets called for every screen that isn't hidden.</summary>
    /// <param name="gameTime">The game time.</param>
    public virtual void Draw(GameTime gameTime) { }

    /// <summary>Tells the screen to leave, after it transitions off.</summary>
    public void ExitScreen()
    {
        if (TransitionOffTime == TimeSpan.Zero)
            ScreenManager.RemoveScreen(this);
        else
            IsExiting = true;
    }
}
