// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TheBlackBox.StateManagement;

/// <summary>
/// The questions an InputAction asks about the keyboard and gamepads. Same interface as the advanced input tutorial.
/// </summary>
public interface IInputState
{
    /// <summary>Whether a key is down this frame.</summary>
    /// <param name="key">The key to check.</param>
    bool IsKeyPressed(Keys key);

    /// <summary>Whether a key went down this frame.</summary>
    /// <param name="key">The key to check.</param>
    bool IsNewKeyPress(Keys key);

    /// <summary>Whether a button is down this frame.</summary>
    /// <param name="button">The button to check.</param>
    /// <param name="controllingPlayer">Only this player, or null for anyone.</param>
    /// <param name="playerIndex">Who pressed it.</param>
    bool IsButtonPressed(Buttons button, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex);

    /// <summary>Whether a button went down this frame.</summary>
    /// <param name="button">The button to check.</param>
    /// <param name="controllingPlayer">Only this player, or null for anyone.</param>
    /// <param name="playerIndex">Who pressed it.</param>
    bool IsNewButtonPress(Buttons button, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex);
}
