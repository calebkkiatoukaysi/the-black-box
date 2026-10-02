using Microsoft.Xna.Framework;
using TheBlackBox.Screens;
using TheBlackBox.StateManagement;

namespace TheBlackBox;

/// <summary>
/// The table's part of the proof run: one state after another, set up by hand, and which frames to keep.
/// </summary>
/// <remarks>
/// Frames are counted from the first frame the table gets drawn. BlackBoxGame does the counting
/// and takes the shots. Anything animated is pinned on the frame of its shot, because saving a PNG stalls
/// the fixed timestep and the catch-up updates would move it.
/// </remarks>
public partial class TableScreen
{
    /// <summary>How many frames the table gets to fade in before the first shot.</summary>
    private const int Settled = 50;

    /// <summary>How many frames the results screen gets to fade in.</summary>
    private const int VerdictSettled = 80;

    /// <summary>The last frame of the table schedule.</summary>
    internal const int ProofEnd = Settled + 604 + 3 * VerdictSettled;

    /// <summary>Sets up whatever state this frame calls for, and names the file if the frame is to be kept.</summary>
    /// <param name="frame">Which frame this is, counting from the table's first one.</param>
    /// <returns>The file the frame should be written to, or null.</returns>
    internal string ProofStep(int frame)
    {
        switch (frame)
        {
            // The discussion, a few frames in so the wheel has settled.
            case Settled + 18:
                return "table-discussion.png";

            // The player's turn, with things in both sets of pockets.
            case Settled + 70:
                _wheel.Hide();
                _discussion = null;
                _discussionEnd = null;
                _saidHold = 0f;

                _run.Banked.Add(ItemCatalog.ToSaveId(ItemId.Revolver));
                _run.Banked.Add(ItemCatalog.ToSaveId(ItemId.AshVeil));
                _run.OpponentBanked.Add(ItemCatalog.ToSaveId(ItemId.Cinder));
                _run.OpponentBanked.Add(ItemCatalog.ToSaveId(ItemId.Mirror));
                _run.OpponentLives = 2;
                _run.OpponentDisposition = new Disposition(-40, 60);

                RoundEngine.DealBoth(_run, _random);
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);

                _opponent.SetDisposition(_run.OpponentDisposition);
                ShowWounds();
                BeginTurn();
                return null;

            case Settled + 128:
                return "table-turn.png";

            // The close-up, held on the Offer phase so the catch-up updates after a save do not open the mouth early.
            case Settled + 129:
                _phase = RoundPhase.Offer;
                OfferHand();
                _phase = RoundPhase.Offer;
                _lid.Open = 0.45f;
                return null;

            case Settled + 131:
                return "table-mouth.png";

            // Reaching. The reach is pinned on the frame of each shot because the round moves it every update.
            case Settled + 134:
                _phase = RoundPhase.Reaching;
                _lid.Open = 1f;
                return null;

            case Settled + 136:
                _reach = 0.5f;
                _hand.Reach = _reach;
                return "table-reaching.png";

            // All the way in. The hold is reset so the box has not paid yet.
            case Settled + 139:
                _reach = 1f;
                _hand.Reach = _reach;
                _held = 0f;
                return "table-taken.png";

            // The payout. Set up by hand rather than through Deal() so the box cannot deal nothing.
            case Settled + 142:
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);
                _hand.IsVisible = false;
                _opponent.SetDisposition(_run.OpponentDisposition);
                Payout();
                return null;

            // The tag is pinned just under the mouth, above where the palm can reach. The palm sits under
            // the mouse, so anywhere lower and the catch could fire on the frame of the shot itself.
            case Settled + 144:
                _token.Place(new Vector2(600f, 560f));
                return "table-catching.png";

            // The aim check, with a revolver in hand.
            case Settled + 146:
                CatchToken();
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Revolver);
                Decide(DealtChoice.Use);
                return null;

            case Settled + 152:
                return "table-aim.png";

            // The other two checks, same way: drop the current check, swap the item, ask again.
            case Settled + 154:
                _check = null;
                _afterCheck = null;
                _phase = RoundPhase.PlayerTurn;
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Tourniquet);
                Decide(DealtChoice.Use);
                return null;

            case Settled + 160:
                return "table-steady.png";

            case Settled + 162:
                _check = null;
                _afterCheck = null;
                _phase = RoundPhase.PlayerTurn;
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);
                Decide(DealtChoice.Use);
                return null;

            case Settled + 170:
                return "table-read.png";

            // Then nobody touches anything: the read check times out, the lens resolves, the opponent plays, the round closes.
            case Settled + 558:
                return "table-resolved.png";

            // Open the pause menu over the table, then close it.
            case Settled + 560:
                ScreenManager.AddScreen(new PauseMenuScreen(SaveAndLeave));
                return null;

            case Settled + 580:
                return "table-paused.png";

            case Settled + 581:
                ExitProofPopups();
                return null;

            // Force both endings by setting the lives and phase. Update shows the results based on the phase.
            case Settled + 590:
                _run.PlayerLives = 0;
                _phase = RoundPhase.Over;
                return null;

            case Settled + 590 + VerdictSettled:
                return "table-consumed.png";

            case Settled + 591 + VerdictSettled:
                ExitProofPopups();
                _run.PlayerLives = 2;
                _run.OpponentLives = 0;
                _verdictShown = false;
                return null;

            case Settled + 591 + 2 * VerdictSettled:
                return "table-advance.png";

            // Chapter two: advance the run like FinishRun does and start it again. Serenity's the only one on the roster so she's the opponent again.
            case Settled + 592 + 2 * VerdictSettled:
                ExitProofPopups();
                _run.Advance();
                Enter();
                return null;

            case Settled + 600 + 2 * VerdictSettled:
                return "table-chapter-two.png";

            // Win that one too, then go back to the lobby from the results.
            case Settled + 602 + 2 * VerdictSettled:
                _run.OpponentLives = 0;
                _phase = RoundPhase.Over;
                return null;

            case Settled + 603 + 3 * VerdictSettled:
                foreach (GameScreen screen in ScreenManager.GetScreens())
                {
                    if (screen is ResultsScreen results) results.ProofReturnToLobby();
                }
                return null;

            default:
                return null;
        }
    }

    /// <summary>Removes the pause menu or the results right away, with no fade.</summary>
    private void ExitProofPopups()
    {
        foreach (GameScreen screen in ScreenManager.GetScreens())
        {
            if (screen is PauseMenuScreen or ResultsScreen) ScreenManager.RemoveScreen(screen);
        }
    }
}
