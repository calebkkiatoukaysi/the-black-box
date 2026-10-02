using Microsoft.Xna.Framework;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The lobby's part of the proof run: walk, talk to everyone, pick something up, be challenged,
/// pause, and go through the door.
/// </summary>
/// <remarks>
/// Movement and presses are fed in through the same paths the keyboard uses (ReadMovement and
/// the interact press), so the shots prove the collision, the camera and the footsteps, not a
/// teleport. The only teleports are between scenes, to keep the run short.
/// </remarks>
public partial class LobbyScreen
{
    /// <summary>How many frames the lobby is given to come up out of the fade.</summary>
    private const int Settled = 50;

    /// <summary>The last frame of the lobby's schedule. The door is gone through on it.</summary>
    internal const int ProofEnd = Settled + 340;

    /// <summary>A push to use instead of the keyboard, while the proof run is steering.</summary>
    private Vector2? _proofMove;

    /// <summary>Presses of E waiting to be taken, while the proof run is steering. Counted, so two never merge into one.</summary>
    private int _proofPresses;

    /// <summary>Presses E if somebody is still talking, and only then.</summary>
    /// <remarks>
    /// Not a fixed number of presses: a page that has finished typing before the press comes
    /// needs one press fewer, and an extra press on a closed box starts the conversation again.
    /// </remarks>
    private void PressWhileTalking()
    {
        if (_dialogue.IsOpen && _proofPresses == 0) _proofPresses = 1;
    }

    /// <summary>Takes one of the proof run's presses, if there is one.</summary>
    private bool TakeProofPress()
    {
        if (_proofPresses == 0) return false;

        _proofPresses--;
        return true;
    }

    /// <summary>Sets up whatever this frame calls for, and names the file if the frame is to be kept.</summary>
    /// <param name="frame">Which drawn frame this is, counted from the lobby's first.</param>
    /// <returns>The file the frame should be written to, or null.</returns>
    internal string ProofStep(int frame)
    {
        switch (frame)
        {
            case Settled:
                return "lobby.png";

            // Up and to the left, into the column, to prove the feet stop at it and the camera follows.
            case Settled + 1:
                _proofMove = new Vector2(-0.55f, -1f);
                return null;

            case Settled + 70:
                _proofMove = null;
                return "lobby-walk.png";

            // Serenity, the chapter's opponent: every page of hers, which opens the door.
            case Settled + 72:
                _player.Position = _map.SerenitySpot + new Vector2(0f, 110f);
                _proofPresses++;
                return null;

            case Settled + 90:
                return "lobby-talk.png";

            case >= Settled + 92 and <= Settled + 108:
                PressWhileTalking();
                return null;

            // The one who watches, and the other conscript, a page each so the dialogue box is seen with both kinds of picture.
            case Settled + 110:
                _player.Position = _map.SecondSpot + new Vector2(-60f, 110f);
                _proofPresses++;
                return null;

            case Settled + 160:
                return "lobby-watched.png";

            case >= Settled + 162 and <= Settled + 170:
                PressWhileTalking();
                return null;

            case Settled + 172:
                _player.Position = _map.ConscriptSpot + new Vector2(0f, 110f);
                _proofPresses++;
                return null;

            case Settled + 210:
                return "lobby-conscript.png";

            case >= Settled + 212 and <= Settled + 220:
                PressWhileTalking();
                return null;

            // Walk onto an item, which goes in the pockets with its sound.
            case Settled + 222:
                _player.Position = _map.ItemSpots["pillar"] + new Vector2(0f, 50f);
                _proofMove = new Vector2(0f, -1f);
                return null;

            case Settled + 232:
                _proofMove = null;
                return "lobby-pickup.png";

            // In front of the door, lit now, then paused over it.
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

            // And walk up into it.
            case Settled + 282:
                _proofMove = new Vector2(0f, -1f);
                return null;

            default:
                return null;
        }
    }
}
