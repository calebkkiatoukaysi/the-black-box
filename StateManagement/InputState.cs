// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TheBlackBox.StateManagement;

/// <summary>
/// The keyboard, mouse and gamepads, this frame and last, so a screen can tell a press from a hold.
/// </summary>
/// <remarks>
/// Same as the advanced input tutorial's InputState, except the ScreenManager updates it (like
/// the textbook's version) instead of it being its own component, and it keeps the mouse too,
/// because the menus here can be clicked.
/// </remarks>
public class InputState : IInputState
{
    /// <summary>The most gamepads MonoGame reports.</summary>
    private const int MaxInputs = 4;

    /// <summary>Every gamepad this frame.</summary>
    public readonly GamePadState[] CurrentGamePadStates = new GamePadState[MaxInputs];

    /// <summary>The keyboard this frame.</summary>
    public KeyboardState CurrentKeyboardState;

    /// <summary>The mouse this frame.</summary>
    public MouseState CurrentMouseState;

    private readonly GamePadState[] _priorGamePadStates = new GamePadState[MaxInputs];
    private KeyboardState _priorKeyboardState;
    private MouseState _priorMouseState;

    /// <summary>Whether the first frame has been read, so the prior frame is a real one.</summary>
    private bool _sampled;

    /// <summary>Reads every device and keeps the last frame for comparison.</summary>
    public void Update()
    {
        for (int i = 0; i < MaxInputs; i++)
        {
            _priorGamePadStates[i] = CurrentGamePadStates[i];
            CurrentGamePadStates[i] = GamePad.GetState((PlayerIndex)i);
        }

        _priorKeyboardState = CurrentKeyboardState;
        CurrentKeyboardState = Keyboard.GetState();

        _priorMouseState = CurrentMouseState;
        CurrentMouseState = Mouse.GetState();

        // A key held down while the game starts should not count as pressed on the first frame.
        if (!_sampled)
        {
            _sampled = true;
            _priorKeyboardState = CurrentKeyboardState;
            _priorMouseState = CurrentMouseState;
            for (int i = 0; i < MaxInputs; i++) _priorGamePadStates[i] = CurrentGamePadStates[i];
        }
    }

    /// <inheritdoc/>
    public bool IsKeyPressed(Keys key) => CurrentKeyboardState.IsKeyDown(key);

    /// <inheritdoc/>
    public bool IsNewKeyPress(Keys key) =>
        CurrentKeyboardState.IsKeyDown(key) && _priorKeyboardState.IsKeyUp(key);

    /// <inheritdoc/>
    public bool IsButtonPressed(Buttons button, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    {
        if (controllingPlayer.HasValue)
        {
            playerIndex = controllingPlayer.Value;
            return CurrentGamePadStates[(int)playerIndex].IsButtonDown(button);
        }

        return IsButtonPressed(button, PlayerIndex.One, out playerIndex) ||
               IsButtonPressed(button, PlayerIndex.Two, out playerIndex) ||
               IsButtonPressed(button, PlayerIndex.Three, out playerIndex) ||
               IsButtonPressed(button, PlayerIndex.Four, out playerIndex);
    }

    /// <inheritdoc/>
    public bool IsNewButtonPress(Buttons button, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    {
        if (controllingPlayer.HasValue)
        {
            playerIndex = controllingPlayer.Value;
            int i = (int)playerIndex;
            return CurrentGamePadStates[i].IsButtonDown(button) && _priorGamePadStates[i].IsButtonUp(button);
        }

        return IsNewButtonPress(button, PlayerIndex.One, out playerIndex) ||
               IsNewButtonPress(button, PlayerIndex.Two, out playerIndex) ||
               IsNewButtonPress(button, PlayerIndex.Three, out playerIndex) ||
               IsNewButtonPress(button, PlayerIndex.Four, out playerIndex);
    }

    /// <summary>Whether the mouse moved since last frame. Menus only follow the mouse when it does.</summary>
    public bool MouseMoved =>
        CurrentMouseState.X != _priorMouseState.X || CurrentMouseState.Y != _priorMouseState.Y;

    /// <summary>Where the mouse is, in screen pixels.</summary>
    public Point MousePosition => CurrentMouseState.Position;
}
