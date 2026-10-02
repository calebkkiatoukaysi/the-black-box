// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0,
// as presented in the CIS 580 textbook (Game Architecture > Game Screens).
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System;
using Microsoft.Xna.Framework;

namespace TheBlackBox.StateManagement;

/// <summary>
/// A screen is a single layer of game content that has its own update and draw logic and can
/// be combined with other layers to build menus and gameplay.
/// </summary>
public abstract class GameScreen
{
    /// <summary>Whether this screen is a popup.</summary>
    /// <remarks>
    /// Normally a screen brought up over another makes the one under it transition off. A popup
    /// doesn't, so the screen underneath stays drawn (that is how the pause menu sits over the table).
    /// </remarks>
    public bool IsPopup { get; protected set; }

    /// <summary>How long the screen takes to transition on.</summary>
    protected TimeSpan TransitionOnTime { get; set; } = TimeSpan.Zero;

    /// <summary>How long the screen takes to transition off.</summary>
    protected TimeSpan TransitionOffTime { get; set; } = TimeSpan.Zero;

    /// <summary>Where the screen is in its transition, from 0 (fully on) to 1 (fully off).</summary>
    protected float TransitionPosition { get; set; } = 1;

    /// <summary>The alpha the transition is at. 1 when fully on, 0 when fully off.</summary>
    public float TransitionAlpha => 1f - TransitionPosition;

    /// <summary>The current state of the screen.</summary>
    public ScreenState ScreenState { get; set; } = ScreenState.TransitionOn;

    /// <summary>Whether the screen is leaving for good, not just covered.</summary>
    /// <remarks>
    /// A screen transitions off either to make room for one on top, or because it is going away.
    /// When this is set it removes itself from the manager once the transition finishes.
    /// </remarks>
    public bool IsExiting { get; protected internal set; }

    /// <summary>Whether this screen is the one taking input right now.</summary>
    public bool IsActive => !_otherScreenHasFocus && (
        ScreenState == ScreenState.TransitionOn ||
        ScreenState == ScreenState.Active);

    private bool _otherScreenHasFocus;

    /// <summary>The ScreenManager in charge of this screen.</summary>
    public ScreenManager ScreenManager { get; internal set; }

    /// <summary>Called when the screen is added to the manager. Load content here.</summary>
    public virtual void Activate() { }

    /// <summary>Called when the screen is removed from the manager.</summary>
    public virtual void Deactivate() { }

    /// <summary>Unloads content for the screen. Called when the screen is removed from the manager.</summary>
    public virtual void Unload() { }

    /// <summary>
    /// Runs the transition. Unlike HandleInput this is called every frame, whether the screen is
    /// active, hidden, or partway through a transition.
    /// </summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above this one is taking input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top of this one.</param>
    public virtual void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        _otherScreenHasFocus = otherScreenHasFocus;

        if (IsExiting)
        {
            // Going away for good, so transition off and then take it off the stack.
            ScreenState = ScreenState.TransitionOff;

            if (!UpdateTransitionPosition(gameTime, TransitionOffTime, 1))
                ScreenManager.RemoveScreen(this);
        }
        else if (coveredByOtherScreen)
        {
            // Covered by another screen, so transition off until it is hidden.
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

    /// <summary>Moves <see cref="TransitionPosition"/> along by the time that has passed.</summary>
    /// <remarks>
    /// The textbook version checks <c>TransitionPosition &gt;= 0</c> on the way off, which is
    /// true from the first frame, so every fade-out finished instantly. It has to reach 1.
    /// </remarks>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="time">How long the whole transition takes.</param>
    /// <param name="direction">-1 to transition on, 1 to transition off.</param>
    /// <returns>True while still transitioning, false once it is done.</returns>
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

    /// <summary>Handles input. Only called on the screen that has focus.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="input">The keyboard, mouse and gamepads, this frame and last.</param>
    public virtual void HandleInput(GameTime gameTime, InputState input) { }

    /// <summary>Takes one typed character. Only sent to the screen that has focus.</summary>
    /// <remarks>Not in the tutorial. The name field needs real typed characters, shift and layouts included.</remarks>
    /// <param name="character">The character the window reported.</param>
    public virtual void HandleTextInput(char character) { }

    /// <summary>Draws the screen. Called for every screen that is not hidden.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public virtual void Draw(GameTime gameTime) { }

    /// <summary>Tells the screen to leave, giving it time to transition off first.</summary>
    public void ExitScreen()
    {
        if (TransitionOffTime == TimeSpan.Zero)
            ScreenManager.RemoveScreen(this);
        else
            IsExiting = true;
    }
}
