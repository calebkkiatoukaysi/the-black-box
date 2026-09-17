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
/// plays it to two of the states that matter -- mid-discussion, and the player's turn with
/// things in both sets of pockets -- and writes each to a file in <c>shots/</c>. Then it
/// quits. It is how the layout is checked after any change to the sheet, the plate or the
/// pockets, and how the pictures for a release get taken, and it is in its own file because
/// none of it is the game.
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

    /// <summary>The file the next frame is to be written to, or null.</summary>
    private string _proofPending;

    /// <summary>The target the frame is being drawn into while a shot is pending.</summary>
    private RenderTarget2D _proofTarget;

    /// <summary>Runs the proof schedule: sit down, wait for the face to settle, shoot, and so on.</summary>
    private void UpdateProof()
    {
        if (ProofDirectory is null) return;

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

            case 132:
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

    /// <summary>Writes the frame out and puts the window back.</summary>
    private void EndProofCapture()
    {
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
