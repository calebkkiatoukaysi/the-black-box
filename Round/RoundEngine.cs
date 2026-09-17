using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// What the player can do with the item in their hand.
/// </summary>
public enum DealtChoice
{
    /// <summary>Play it now. It is destroyed.</summary>
    Use,

    /// <summary>Put it in a pocket, to be played on a later turn.</summary>
    Pocket,

    /// <summary>Give it back to the box. Nothing happens.</summary>
    Leave,
}

/// <summary>
/// One round of the table, as rules rather than as a screen.
/// </summary>
/// <remarks>
/// <para>
/// The screen used to own the round: which hand was dealt what, who had answered and who
/// had not, all of it in <see cref="BlackBoxGame"/> next to the code that draws buttons.
/// That made it impossible to play a round without a window open, and a game that can only
/// be balanced by playing it by hand is a game that never gets balanced. Everything in here
/// takes a <see cref="SaveData"/> and a <see cref="Random"/> and hands back what happened
/// as lines, so the same code plays the table on screen and plays it five thousand times in
/// <see cref="RoundSimulator"/>.
/// </para>
/// <para>
/// The shape of a round is: both hands go in and both sides are dealt; the player takes
/// their turn -- any number of things out of their pockets, and then one decision about the
/// item in their hand; the opponent takes theirs, second, into the unknown the player just
/// made; and the round closes. Each of those is one method here, in that order.
/// </para>
/// </remarks>
public static class RoundEngine
{
    /// <summary>
    /// Both hands go in together and both sides are paid.
    /// </summary>
    /// <remarks>
    /// The player's deal is the favoured one -- see <see cref="RoundRules.PlayerFavour"/>.
    /// Anything the box already promised through a marked deck goes to the player, and is
    /// what it promised, whatever the favour would otherwise have done with it.
    /// </remarks>
    /// <param name="run">The table.</param>
    /// <param name="random">The box's hand.</param>
    public static void DealBoth(SaveData run, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(random);

        ItemResolver.PutInHand(run, ItemResolver.TakeNextDeal(run, random, RoundRules.PlayerFavour), toPlayer: true);
        ItemResolver.PutInHand(run, ItemCatalog.Deal(random), toPlayer: false);
        run.HandsFed += 2;
    }

    /// <summary>
    /// Whether the item in the player's hand can go into a pocket.
    /// </summary>
    /// <remarks>
    /// Two things stop it. Full pockets, obviously. And a blind deal: what has not been
    /// looked at cannot be put away, because putting it away unseen would be a way of never
    /// paying what <see cref="ItemId.Rotgut"/> charged. A blind item is used or left.
    /// </remarks>
    /// <param name="run">The table.</param>
    public static bool CanPocketDealt(SaveData run) =>
        !run.DealtBlind && !run.PocketsFull && ItemCatalog.TryParse(run.Dealt, out _);

    /// <summary>
    /// Plays one item out of the player's pockets, mid-turn.
    /// </summary>
    /// <param name="run">The table.</param>
    /// <param name="slot">Which pocket, from 0.</param>
    /// <param name="random">The coin, for anything the item flips.</param>
    /// <returns>What happened, one line at a time. Empty if the slot held nothing.</returns>
    public static List<string> PlayFromPocket(SaveData run, int slot, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (slot < 0 || slot >= run.Banked.Count) return new List<string>();
        if (!ItemCatalog.TryParse(run.Banked[slot], out ItemId item))
        {
            // An id this build does not know. Dropped, the way an unreadable save is.
            run.Banked.RemoveAt(slot);
            return new List<string>();
        }

        run.Banked.RemoveAt(slot);
        return ItemResolver.Use(run, item, byPlayer: true, random);
    }

    /// <summary>
    /// Takes the player's decision about the item in their hand.
    /// </summary>
    /// <param name="run">The table.</param>
    /// <param name="choice">What they decided.</param>
    /// <param name="random">The coin, for anything the item flips.</param>
    /// <returns>What happened, one line at a time.</returns>
    public static List<string> DecideDealt(SaveData run, DealtChoice choice, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);

        var log = new List<string>();

        if (!ItemCatalog.TryParse(run.Dealt, out ItemId item))
        {
            run.Dealt = string.Empty;
            run.DealtBlind = false;
            return log;
        }

        bool blind = run.DealtBlind;

        // Emptied before the item resolves rather than after: a second hand puts a new
        // item into whichever hand is empty, and this one has to be by then.
        run.Dealt = string.Empty;
        run.DealtBlind = false;

        switch (choice)
        {
            case DealtChoice.Use:
                log.AddRange(ItemResolver.Use(run, item, byPlayer: true, random));
                break;

            case DealtChoice.Pocket when !blind && !run.PocketsFull:
                run.Banked.Add(ItemCatalog.ToSaveId(item));
                log.Add("YOU PUT THE " + ItemCatalog.NameOf(item).ToUpperInvariant() + " IN YOUR POCKET.");
                break;

            // A pocket that was refused is a leave, said as one. This is not reachable
            // from the table, where the button is disabled, but the rule lives here.
            default:
                log.Add(blind
                    ? "YOU GIVE IT BACK WITHOUT EVER SEEING WHAT IT WAS."
                    : "YOU LET THE BOX TAKE THE " + ItemCatalog.NameOf(item).ToUpperInvariant() + " BACK.");
                break;
        }

        return log;
    }

    /// <summary>
    /// The opponent's whole turn: a look in their pockets, and then their decision.
    /// </summary>
    /// <remarks>
    /// They go second because the player acting into the unknown is the shape of the round.
    /// They have been sitting on their item the whole time the player was deciding, and
    /// they have seen everything the player did with theirs.
    /// </remarks>
    /// <param name="run">The table.</param>
    /// <param name="random">The coin.</param>
    /// <returns>What happened, one line at a time.</returns>
    public static List<string> OpponentTurn(SaveData run, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(random);

        var log = new List<string>();

        for (int plays = 0; plays < RoundRules.OpponentPocketPlays && !IsOver(run); plays++)
        {
            int slot = OpponentMind.ChoosePocketPlay(run, random);
            if (slot < 0) break;

            ItemCatalog.TryParse(run.OpponentBanked[slot], out ItemId pocketed);
            run.OpponentBanked.RemoveAt(slot);

            log.Add("THEY TAKE SOMETHING OUT OF THEIR POCKET.");
            log.AddRange(ItemResolver.Use(run, pocketed, byPlayer: false, random));
        }

        if (IsOver(run)) return log;

        if (!ItemCatalog.TryParse(run.OpponentDealt, out ItemId theirs))
        {
            run.OpponentDealt = string.Empty;
            run.OpponentDealtBlind = false;
            return log;
        }

        DealtChoice choice = OpponentMind.Decide(run, theirs, random);

        run.OpponentDealt = string.Empty;
        run.OpponentDealtBlind = false;

        switch (choice)
        {
            case DealtChoice.Use:
                log.AddRange(ItemResolver.Use(run, theirs, byPlayer: false, random));
                break;

            case DealtChoice.Pocket:
                run.OpponentBanked.Add(ItemCatalog.ToSaveId(theirs));
                log.Add("THEY PUT SOMETHING IN THEIR POCKET. YOU DO NOT SEE WHAT.");
                break;

            default:
                log.Add("THEY GIVE THEIRS BACK TO THE BOX.");
                break;
        }

        return log;
    }

    /// <summary>
    /// Closes the round: the count goes up, and what was only true for this round stops
    /// being true.
    /// </summary>
    /// <param name="run">The table.</param>
    /// <returns>What there is to say about how it ended, if anything.</returns>
    public static List<string> CloseRound(SaveData run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var log = new List<string>();

        run.Round++;
        run.SeesOpponentItem = false;
        run.SeesOpponentPockets = false;

        if (run.PlayerLives <= 0) log.Add("YOU HAVE NOTHING LEFT TO LOSE. THE BOX IS FINISHED WITH YOU.");
        else if (run.OpponentLives <= 0) log.Add("THEY HAVE NOTHING LEFT. THE BOX TAKES THEM, AND LOOKS AT YOU.");

        return log;
    }

    /// <summary>Whether somebody has run out of lives.</summary>
    /// <param name="run">The table.</param>
    public static bool IsOver(SaveData run) => run.PlayerLives <= 0 || run.OpponentLives <= 0;
}
