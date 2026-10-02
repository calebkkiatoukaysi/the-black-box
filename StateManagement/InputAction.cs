// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TheBlackBox.StateManagement;

/// <summary>
/// A group of keys and buttons that all do the same thing, like W, the up arrow and up on the d-pad.
/// </summary>
public class InputAction
{
    private readonly Buttons[] _buttons;
    private readonly Keys[] _keys;
    private readonly bool _firstPressOnly;

    // Delegate types for the two kinds of check, so Occurred can pick held or new press once at the top.
    private delegate bool ButtonPress(Buttons button, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex);
    private delegate bool KeyPress(Keys key);

    /// <summary>Creates an action out of the buttons and keys that trigger it.</summary>
    /// <param name="triggerButtons">The gamepad buttons that trigger it, or null.</param>
    /// <param name="triggerKeys">The keys that trigger it, or null.</param>
    /// <param name="firstPressOnly">True to fire once per press, false to fire every frame it is held.</param>
    public InputAction(Buttons[] triggerButtons, Keys[] triggerKeys, bool firstPressOnly)
    {
        _buttons = triggerButtons is null ? Array.Empty<Buttons>() : (Buttons[])triggerButtons.Clone();
        _keys = triggerKeys is null ? Array.Empty<Keys>() : (Keys[])triggerKeys.Clone();
        _firstPressOnly = firstPressOnly;
    }

    /// <summary>Whether the action happened this frame.</summary>
    /// <param name="inputState">The input to check.</param>
    /// <param name="controllingPlayer">Only this player's pad, or null for any.</param>
    /// <param name="playerIndex">Who did it. The keyboard is always player one.</param>
    /// <returns>True if any of its keys or buttons fired.</returns>
    public bool Occurred(IInputState inputState, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    {
        ButtonPress buttonTest;
        KeyPress keyTest;

        if (_firstPressOnly)
        {
            buttonTest = inputState.IsNewButtonPress;
            keyTest = inputState.IsNewKeyPress;
        }
        else
        {
            buttonTest = inputState.IsButtonPressed;
            keyTest = inputState.IsKeyPressed;
        }

        foreach (Buttons button in _buttons)
        {
            if (buttonTest(button, controllingPlayer, out playerIndex))
                return true;
        }

        foreach (Keys key in _keys)
        {
            if (keyTest(key))
            {
                playerIndex = PlayerIndex.One;
                return true;
            }
        }

        playerIndex = PlayerIndex.One;
        return false;
    }

    /// <summary>Whether the action happened this frame, for anyone.</summary>
    /// <param name="inputState">The input to check.</param>
    public bool Occurred(IInputState inputState) => Occurred(inputState, null, out _);
}
