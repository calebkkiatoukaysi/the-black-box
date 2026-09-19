using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Proof shots: <c>dotnet run -- --proof shots</c> plays a scratch run through every state
/// worth looking at and writes each one to a PNG, then quits. How I check the layout.
/// </summary>
/// <remarks>
/// Drawn into a render target rather than read off the window, so the shot is the whole
/// 1600x900 table no matter what is in front of the window.
/// </remarks>
public partial class BlackBoxGame
{
    /// <summary>Where the shots go, or null when the game is being played.</summary>
    public string ProofDirectory { get; init; }

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

    /// <summary>Runs the proof schedule, one state after another.</summary>
    private void UpdateProof()
    {
        if (ProofDirectory is null) return;
        if (!_proofDrawn) return;

        _proofDrawn = false;
        _proofFrame++;

        switch (_proofFrame)
        {
            // A run of its own, never written anywhere.
            case 2:
                _runSlot = 0;
                _run = SaveData.NewRun();
                _run.PlayerName = "Proof";
                _runStatus = null;
                EnterRun();
                break;

            // Long enough after the line landed for the mouth to have shut again.
            case 70:
                _proofPending = "table-discussion.png";
                break;

            // The player's turn, with things in both sets of pockets.
            case 72:
                _wheel.Hide();
                _discussion = null;
                _discussionEnd = null;
                _saidHold = 0f;
                _speakHold = 0f;

                _run.Banked.Add(ItemCatalog.ToSaveId(ItemId.Revolver));
                _run.Banked.Add(ItemCatalog.ToSaveId(ItemId.AshVeil));
                _run.OpponentBanked.Add(ItemCatalog.ToSaveId(ItemId.Cinder));
                _run.OpponentBanked.Add(ItemCatalog.ToSaveId(ItemId.Mirror));
                _run.OpponentLives = 2;
                _run.OpponentDisposition = new Disposition(-40, 60);

                RoundEngine.DealBoth(_run, _random);
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);

                _opponent.Pose = OpponentPose.Even;
                _opponent.SetDisposition(_run.OpponentDisposition);
                ShowWounds();
                BeginTurn();
                break;

            case 130:
                _proofPending = "table-turn.png";
                break;

            // The close-up, held on the Offer phase so the catch-up updates after a save do not open the mouth early.
            case 131:
                _phase = RoundPhase.Offer;
                OfferHand();
                _phase = RoundPhase.Offer;
                _lidOpen = 0.45f;
                break;

            case 133:
                _proofPending = "table-mouth.png";
                break;

            // Reaching. The reach is pinned on the frame of each shot because the round moves it every update.
            case 136:
                _phase = RoundPhase.Reaching;
                _lidOpen = 1f;
                break;

            case 138:
                _reach = 0.5f;
                _hand.Reach = _reach;
                _proofPending = "table-reaching.png";
                break;

            // All the way in. The hold is reset so the box has not paid yet.
            case 141:
                _reach = 1f;
                _hand.Reach = _reach;
                _held = 0f;
                _proofPending = "table-taken.png";
                break;

            // The payout. Set up by hand rather than through Deal() so the box cannot deal nothing.
            case 144:
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);
                _hand.IsVisible = false;
                _opponent.Pose = OpponentPose.Even;
                Payout();
                break;

            case 146:
                _token.Place(new Vector2(600f, 760f));
                _proofPending = "table-catching.png";
                break;

            // The aim check, with a revolver in hand.
            case 148:
                CatchToken();
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Revolver);
                Decide(DealtChoice.Use);
                break;

            case 154:
                _proofPending = "table-aim.png";
                break;

            // The other two checks, same way: drop the current check, swap the item, ask again.
            case 156:
                _check = null;
                _afterCheck = null;
                _phase = RoundPhase.PlayerTurn;
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Tourniquet);
                Decide(DealtChoice.Use);
                break;

            case 162:
                _proofPending = "table-steady.png";
                break;

            case 164:
                _check = null;
                _afterCheck = null;
                _phase = RoundPhase.PlayerTurn;
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);
                Decide(DealtChoice.Use);
                break;

            case 172:
                _proofPending = "table-read.png";
                break;

            // Then nobody touches anything: the read check times out, the lens resolves, the opponent plays, the round closes.
            case 560:
                _proofPending = "table-resolved.png";
                break;

            // The two endings, forced by setting lives and phase, which is all the ending screen reads.
            case 563:
                _run.PlayerLives = 0;
                _phase = RoundPhase.Over;
                _returnButton.Reset();
                break;

            case 565:
                _proofPending = "table-consumed.png";
                break;

            case 568:
                _run.PlayerLives = 2;
                _run.OpponentLives = 0;
                break;

            case 570:
                _proofPending = "table-advance.png";
                break;

            // Chapter two: advance the run the way LeaveRun does and enter it again, which seats the second opponent.
            case 572:
                _run.Advance();
                EnterRun();
                break;

            case 575:
                _proofPending = "table-second-talking.png";
                break;

            // Long enough after the line landed for the mouth to have shut again.
            case 640:
                _proofPending = "table-second.png";
                break;

            case 642:
                Exit();
                break;
        }
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
