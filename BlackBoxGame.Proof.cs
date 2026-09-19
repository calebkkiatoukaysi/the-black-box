using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Proof shots: <c>dotnet run -- --proof shots</c> plays a scratch run through every state
/// worth looking at and writes each one to a PNG, then quits. How I check the layout.
/// </summary>
/// <remarks>
/// Drawn into a render target rather than read off the window, so the shot is the whole
/// 1600x900 table no matter what is in front of the window. The states themselves are set up
/// by the table, in TableScreen.Proof.
/// </remarks>
public partial class BlackBoxGame
{
    /// <summary>Where the shots go, or null when the game is being played.</summary>
    public string ProofDirectory { get; init; }

    /// <summary>The frame the scratch run sits down on. Before it the title screen is settling.</summary>
    private const int ProofSitDown = 2;

    /// <summary>A name as long as the form allows, so the heading is proved at its widest.</summary>
    private const string ProofName = "Proof Of Concept";

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

    /// <summary>Runs the proof schedule, one drawn frame at a time.</summary>
    private void UpdateProof()
    {
        if (ProofDirectory is null) return;
        if (!_proofDrawn) return;

        _proofDrawn = false;
        _proofFrame++;

        // A run of its own, never written anywhere. The table takes it from here.
        if (_proofFrame == ProofSitDown)
        {
            SaveData run = SaveData.NewRun();
            run.PlayerName = ProofName;
            EnterRun(0, run);
        }
        else if (_proofFrame > TableScreen.ProofEnd)
        {
            Exit();
        }
        else
        {
            _proofPending = _table.ProofStep(_proofFrame);
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
