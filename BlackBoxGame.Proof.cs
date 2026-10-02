using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Screens;
using TheBlackBox.StateManagement;

namespace TheBlackBox;



/// <summary>
/// Bug fixing class for capturing proof shots of the game.
///
/// This class handles the automated capture of proof shots during gameplay. Works well with debugging and layout verification.
/// Also helps out with regression testing by providing visual confirmation of game states.
/// How to use:
/// 1. Set the <c>ProofDirectory</c> property to the desired output folder.
/// 2. Run the game with the proof mode enabled.
/// 3. The game will automatically capture and save proof shots to the specified directory.
/// </summary>
/// <remarks>
/// The run goes the way a player would: the title, the options, the save form, the customization
/// screen, the lobby, the door, the table, the verdict, back to the lobby, and out to the title
/// through the pause menu. Each screen with a schedule of its own (customization, lobby, table)
/// is handed its own frame count once it is up. It plays a scratch run that is never written,
/// and it is silent: every sound that would have played goes into audio.log instead.
/// </remarks>
public partial class BlackBoxGame
{
    /// <summary>Where the shots go, or null when the game is being played.</summary>
    public string ProofDirectory { get; init; }

    // The menus, by frame: each one is given time to fade in before it is shot.
    private const int ProofTitleShot = 50;
    private const int ProofOptionsOpen = 51;
    private const int ProofOptionsShot = 75;
    private const int ProofOptionsClose = 76;
    private const int ProofSlotsOpen = 95;
    private const int ProofSlotsShot = 120;
    private const int ProofCustomizeOpen = 121;

    /// <summary>How long the lobby is given after the verdict sends the player back to it, and the title after the pause menu does.</summary>
    private const int ProofSettle = 60;

    /// <summary>The frame the proof run is on, which is what schedules it.</summary>
    private int _proofFrame;

    /// <summary>Whether a frame has been drawn since the schedule last moved.</summary>
    /// <remarks>
    /// Saving a PNG stalls the fixed timestep, and it catches up with a burst of Updates with
    /// no Draw between. My first draft lost shots to that burst, so the schedule only counts
    /// drawn frames.
    /// </remarks>
    private bool _proofDrawn = true;

    /// <summary>The file the next frame is to be written to, or null.</summary>
    private string _proofPending;

    /// <summary>The target the frame is being drawn into while a shot is pending.</summary>
    private RenderTarget2D _proofTarget;

    /// <summary>Which leg of the run it is on, the screen whose own schedule is running, and the frame that screen came up on.</summary>
    private ProofStage _proofStage = ProofStage.Menus;
    private GameScreen _proofSubject;
    private int _proofSubjectStart;

    /// <summary>Every sound the run asked for, by frame, written out at the end.</summary>
    private readonly StringBuilder _proofAudio = new();
    private int _proofAudioSeen;

    /// <summary>Runs the proof schedule, one drawn frame at a time.</summary>
    private void UpdateProof()
    {
        if (ProofDirectory is null) return;
        if (!_proofDrawn) return;

        _proofDrawn = false;
        _proofFrame++;
        RecordProofAudio();

        switch (_proofStage)
        {
            case ProofStage.Menus:
                _proofPending = ProofMenus();
                break;

            case ProofStage.Customization:
                _proofPending = RunSubject<CustomizationScreen>(s => s.ProofStep(Frame()), CustomizationScreen.ProofEnd, ProofStage.Lobby);
                break;

            case ProofStage.Lobby:
                _proofPending = RunSubject<LobbyScreen>(s => s.ProofStep(Frame()), LobbyScreen.ProofEnd, ProofStage.Table);
                break;

            case ProofStage.Table:
                _proofPending = RunSubject<TableScreen>(s => s.ProofStep(Frame()), TableScreen.ProofEnd, ProofStage.LobbyAgain);
                break;

            case ProofStage.LobbyAgain:
                _proofPending = RunSubject<LobbyScreen>(LobbyAgain, ProofSettle + 2, ProofStage.TitleAgain);
                break;

            case ProofStage.TitleAgain:
                _proofPending = RunSubject<TitleScreen>(s => Frame() == ProofSettle ? "title-again.png" : null, ProofSettle, ProofStage.Done);
                break;

            default:
                File.WriteAllText(Path.Combine(ProofDirectory, "audio.log"), _proofAudio.ToString());
                Exit();
                break;
        }
    }

    /// <summary>The title, the options over it, the save form over it, and then the customization screen with a scratch run.</summary>
    private string ProofMenus()
    {
        switch (_proofFrame)
        {
            case ProofTitleShot:
                return "title.png";

            case ProofOptionsOpen:
                _screenManager.AddScreen(new OptionsScreen());
                return null;

            case ProofOptionsShot:
                return "options.png";

            case ProofOptionsClose:
                foreach (GameScreen screen in _screenManager.GetScreens())
                    if (screen is OptionsScreen) screen.ExitScreen();
                return null;

            // The save form reads the real slots off disk to show them. It never writes unless one is clicked.
            case ProofSlotsOpen:
                _screenManager.AddScreen(new SaveSlotScreen());
                return null;

            case ProofSlotsShot:
                return "slots.png";

            // A run of its own, never written anywhere, dressed and named on the customization screen.
            case ProofCustomizeOpen:
                foreach (GameScreen screen in _screenManager.GetScreens())
                    if (screen is not BoxBackgroundScreen) _screenManager.RemoveScreen(screen);
                _screenManager.AddScreen(new CustomizationScreen(RunSession.Scratch(SaveData.NewRun())));
                _proofStage = ProofStage.Customization;
                return null;

            default:
                return null;
        }
    }

    /// <summary>Back in the lobby off the verdict: a shot of it, then the pause menu's way out to the title.</summary>
    private string LobbyAgain(LobbyScreen lobby)
    {
        if (Frame() == ProofSettle) return "lobby-again.png";

        if (Frame() == ProofSettle + 1) _screenManager.AddScreen(new PauseMenuScreen(() => null));

        if (Frame() == ProofSettle + 2)
        {
            foreach (GameScreen screen in _screenManager.GetScreens())
                if (screen is PauseMenuScreen pause) pause.ProofReturnToTitle();
        }

        return null;
    }

    /// <summary>Waits for a screen of a type to come up, then runs a step of its schedule each frame until it is done.</summary>
    /// <typeparam name="T">The screen to wait for.</typeparam>
    /// <param name="step">One frame of its schedule. Returns the file to shoot, or null.</param>
    /// <param name="end">The last frame of its schedule.</param>
    /// <param name="next">Which leg comes after.</param>
    private string RunSubject<T>(System.Func<T, string> step, int end, ProofStage next) where T : GameScreen
    {
        if (_proofSubject is not T subject)
        {
            foreach (GameScreen screen in _screenManager.GetScreens())
            {
                if (screen is T found && !found.IsExiting)
                {
                    _proofSubject = found;
                    _proofSubjectStart = _proofFrame;
                }
            }
            return null;
        }

        if (Frame() > end)
        {
            _proofStage = next;
            _proofSubject = null;
            return null;
        }

        return step(subject);
    }

    /// <summary>How many frames the current subject has been up.</summary>
    private int Frame() => _proofFrame - _proofSubjectStart;

    /// <summary>Copies any new sounds off the audio manager's log with the frame they were asked for on.</summary>
    private void RecordProofAudio()
    {
        for (; _proofAudioSeen < _audio.Log.Count; _proofAudioSeen++)
            _proofAudio.Append(_proofFrame).Append(' ').AppendLine(_audio.Log[_proofAudioSeen]);
    }

    /// <summary>Points the frame at a render target if a shot is pending.</summary>
    private void BeginProofCapture()
    {
        if (_proofPending is null) return;

        _proofTarget = new RenderTarget2D(GraphicsDevice, ScreenWidth, ScreenHeight);
        GraphicsDevice.SetRenderTarget(_proofTarget);
    }

    /// <summary>Writes the frame out if one was pending, and puts the window back.</summary>
    private void EndProofCapture()
    {
        _proofDrawn = true;
        if (_proofTarget is null) return;

        GraphicsDevice.SetRenderTarget(null);

        Directory.CreateDirectory(ProofDirectory);
        using (FileStream file = File.Create(Path.Combine(ProofDirectory, _proofPending)))
            _proofTarget.SaveAsPng(file, ScreenWidth, ScreenHeight);

        _proofTarget.Dispose();
        _proofTarget = null;
        _proofPending = null;
    }
}
