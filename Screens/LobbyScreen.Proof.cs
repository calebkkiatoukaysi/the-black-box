using Microsoft.Xna.Framework;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The lobby's part of the proof run: walk around, talk to everyone, pick something up, get
/// challenged, pause, and go through the door.
/// </summary>
/// <remarks>
/// Movement and presses go in through the same code the keyboard uses (ReadMovement and the
/// interact press), so the shots actually test the collision, camera and footsteps. I only
/// teleport the player between scenes to keep the run short.
/// </remarks>
public partial class LobbyScreen
{
    /// <summary>How many frames the lobby gets to fade in before the first shot.</summary>
    private const int Settled = 50;

    /// <summary>The last frame of this schedule. The player is through the door by then.</summary>
    internal const int ProofEnd = Settled + 340;

    /// <summary>A direction to walk in instead of reading the keyboard, while the proof run is driving.</summary>
    private Vector2? _proofMove;

    /// <summary>How many E presses are waiting. It's a count so two presses can't turn into one.</summary>
    private int _proofPresses;

    /// <summary>Presses E, but only if someone is still talking.</summary>
    /// <remarks>
    /// My first try used a fixed number of presses, but a page that finished typing early needs
    /// one less, and the extra press started the whole conversation over.
    /// </remarks>
    private void PressWhileTalking()
    {
        if (_dialogue.IsOpen && _proofPresses == 0) _proofPresses = 1;
    }

    /// <summary>Uses up one of the waiting presses, if there is one.</summary>
    private bool TakeProofPress()
    {
        if (_proofPresses == 0) return false;

        _proofPresses--;
        return true;
    }

    /// <summary>Sets up whatever this frame needs, and returns a file name if this frame should be saved.</summary>
    /// <param name="frame">Which drawn frame this is, counted from the lobby's first.</param>
    /// <returns>The file the frame should be written to, or null.</returns>
    internal string ProofStep(int frame)
    {
        switch (frame)
        {
            case Settled:
                return "lobby.png";

            // Walk up and left into the column, to check the feet stop at it and the camera follows.
            case Settled + 1:
                _proofMove = new Vector2(-0.55f, -1f);
                return null;

            case Settled + 70:
                _proofMove = null;
                return "lobby-walk.png";

            // Talk to Serenity all the way through, which opens the door.
            case Settled + 72:
                _player.Position = _map.SerenitySpot + new Vector2(0f, 110f);
                _proofPresses++;
                return null;

            case Settled + 90:
                return "lobby-talk.png";

            case >= Settled + 92 and <= Settled + 108:
                PressWhileTalking();
                return null;

            // The other conscript (their portrait is a crop of their walking sheet).
            case Settled + 172:
                _player.Position = _map.ConscriptSpot + new Vector2(0f, 110f);
                _proofPresses++;
                return null;

            case Settled + 210:
                return "lobby-conscript.png";

            case >= Settled + 212 and <= Settled + 220:
                PressWhileTalking();
                return null;

            // Walk onto an item so it goes in the pockets.
            case Settled + 222:
                _player.Position = _map.ItemSpots["pillar"] + new Vector2(0f, 50f);
                _proofMove = new Vector2(0f, -1f);
                return null;

            case Settled + 232:
                _proofMove = null;
                return "lobby-pickup.png";

            // Stand in front of the open door, then pause.
            case Settled + 236:
                _player.Position = _map.Door.Foot + new Vector2(0f, 140f);
                _player.Facing = Direction.Up;
                return null;

            case Settled + 256:
                return "lobby-door.png";

            case Settled + 258:
                ScreenManager.AddScreen(new PauseMenuScreen(SaveAndLeave));
                return null;

            case Settled + 278:
                return "lobby-paused.png";

            case Settled + 279:
                foreach (GameScreen screen in ScreenManager.GetScreens())
                {
                    if (screen is PauseMenuScreen) ScreenManager.RemoveScreen(screen);
                }
                return null;

            // Then walk into it.
            case Settled + 282:
                _proofMove = new Vector2(0f, -1f);
                return null;

            default:
                return null;
        }
    }
}
