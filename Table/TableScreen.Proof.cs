using Microsoft.Xna.Framework;

namespace TheBlackBox;

/// <summary>
/// The table's part of the proof run: one state after another, set up by hand, and which frames to keep.
/// </summary>
/// <remarks>
/// Frames are counted by BlackBoxGame, which sits the scratch run down at frame 2 and takes
/// the shots. Anything animated is pinned on the frame of its shot, because saving a PNG stalls
/// the fixed timestep and the catch-up updates would move it.
/// </remarks>
public partial class TableScreen
{
    /// <summary>The last frame of the schedule. The game quits after it.</summary>
    internal const int ProofEnd = 582;

    /// <summary>Sets up whatever state this frame calls for, and names the file if the frame is to be kept.</summary>
    /// <param name="frame">Which drawn frame this is, counted from the title screen.</param>
    /// <returns>The file the frame should be written to, or null.</returns>
    internal string ProofStep(int frame)
    {
        switch (frame)
        {
            // The discussion, a few frames in so the wheel has settled.
            case 20:
                return "table-discussion.png";

            // The player's turn, with things in both sets of pockets.
            case 72:
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

            case 130:
                return "table-turn.png";

            // The close-up, held on the Offer phase so the catch-up updates after a save do not open the mouth early.
            case 131:
                _phase = RoundPhase.Offer;
                OfferHand();
                _phase = RoundPhase.Offer;
                _lid.Open = 0.45f;
                return null;

            case 133:
                return "table-mouth.png";

            // Reaching. The reach is pinned on the frame of each shot because the round moves it every update.
            case 136:
                _phase = RoundPhase.Reaching;
                _lid.Open = 1f;
                return null;

            case 138:
                _reach = 0.5f;
                _hand.Reach = _reach;
                return "table-reaching.png";

            // All the way in. The hold is reset so the box has not paid yet.
            case 141:
                _reach = 1f;
                _hand.Reach = _reach;
                _held = 0f;
                return "table-taken.png";

            // The payout. Set up by hand rather than through Deal() so the box cannot deal nothing.
            case 144:
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);
                _hand.IsVisible = false;
                _opponent.SetDisposition(_run.OpponentDisposition);
                Payout();
                return null;

            // The tag is pinned just under the mouth, above where the palm can reach. The palm sits under
            // the mouse, so anywhere lower and the catch could fire on the frame of the shot itself.
            case 146:
                _token.Place(new Vector2(600f, 560f));
                return "table-catching.png";

            // The aim check, with a revolver in hand.
            case 148:
                CatchToken();
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Revolver);
                Decide(DealtChoice.Use);
                return null;

            case 154:
                return "table-aim.png";

            // The other two checks, same way: drop the current check, swap the item, ask again.
            case 156:
                _check = null;
                _afterCheck = null;
                _phase = RoundPhase.PlayerTurn;
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Tourniquet);
                Decide(DealtChoice.Use);
                return null;

            case 162:
                return "table-steady.png";

            case 164:
                _check = null;
                _afterCheck = null;
                _phase = RoundPhase.PlayerTurn;
                _run.Dealt = ItemCatalog.ToSaveId(ItemId.Lens);
                Decide(DealtChoice.Use);
                return null;

            case 172:
                return "table-read.png";

            // Then nobody touches anything: the read check times out, the lens resolves, the opponent plays, the round closes.
            case 560:
                return "table-resolved.png";

            // The two endings, forced by setting lives and phase, which is all the ending screen reads.
            case 563:
                _run.PlayerLives = 0;
                _phase = RoundPhase.Over;
                _returnButton.Reset();
                return null;

            case 565:
                return "table-consumed.png";

            case 568:
                _run.PlayerLives = 2;
                _run.OpponentLives = 0;
                return null;

            case 570:
                return "table-advance.png";

            // Chapter two: advance the run the way Leave does and enter it again, which seats the second opponent.
            case 572:
                _run.Advance();
                Enter(_slot, _run);
                return null;

            case 580:
                return "table-second.png";

            default:
                return null;
        }
    }
}
