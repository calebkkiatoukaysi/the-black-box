using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Proof shots: the table rendered to PNG files without anybody at the keyboard.
/// </summary>
/// <remarks>
/// <para>
/// <c>dotnet run -- --proof shots</c> opens a scratch run that never touches a save slot,
/// plays it to the states that matter -- mid-discussion, the player's turn with things in
/// both sets of pockets, and the arm going into the box, halfway and all the way -- and
/// writes each to a file in <c>shots/</c>. Then it quits. It is how the layout is checked
/// after any change to a sheet, the plate or the pockets, and how the pictures for a
/// release get taken, and it is in its own file because none of it is the game.
/// </para>
/// <para>
/// The frames are drawn into a render target rather than read back off the window, so the
/// shot is the whole 1600x900 table whatever is in front of the window on the desktop.
/// </para>
/// </remarks>
public partial class BlackBoxGame
{
    /// <summary>Where the shots go, or null when the game is being played.</summary>
    public string ProofDirectory { get; init; }

    /// <summary>The frame the proof run is on, which is what schedules it.</summary>
    private int _proofFrame;

    /// <summary>
    /// Whether a frame has been drawn since the schedule last moved.
    /// </summary>
    /// <remarks>
    /// Writing a 1600x900 PNG takes a few hundred milliseconds, and the fixed timestep pays
    /// that back afterwards by running Update after Update with no Draw between them. The
    /// first draft of the reaching shots was scheduled a handful of frames after the turn
    /// shot, and every one of those frames went by in that burst: the pending file name was
    /// overwritten and the game quit before anything was drawn. So the schedule only counts
    /// frames that were actually drawn, and a shot is never more than one drawn frame away
    /// from being written.
    /// </remarks>
    private bool _proofDrawn = true;

    /// <summary>The file the next frame is to be written to, or null.</summary>
    private string _proofPending;

    /// <summary>The target the frame is being drawn into while a shot is pending.</summary>
    private RenderTarget2D _proofTarget;

    /// <summary>Runs the proof schedule: sit down, wait for the face to settle, shoot, and so on.</summary>
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

            // The player's turn, with something in every kind of pocket and a wound on them.
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

            // The arm, halfway in and still curled. Put straight into the reaching phase
            // rather than through the button, so the shot is of a known point on the way.
            // The reach is pinned again on the frame of the shot, because the round has
            // been moving it on every update in between.
            case 132:
                _phase = RoundPhase.Reaching;
                _held = 0f;
                _hand.IsVisible = true;
                _opponent.Pose = OpponentPose.Reaching;
                break;

            case 134:
                _reach = 0.5f;
                _hand.Reach = _reach;
                _proofPending = "table-reaching.png";
                break;

            // And all the way in, open, while the box holds it. The hold is reset on the
            // frame of the shot, so it is nowhere near TakeSeconds and the box has not paid.
            case 138:
                _reach = 1f;
                _hand.Reach = _reach;
                _held = 0f;
                _proofPending = "table-taken.png";
                break;

            case 140:
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

    /// <summary>Writes the frame out, if one was pending, and puts the window back.</summary>
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
