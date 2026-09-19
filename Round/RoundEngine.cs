using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// One round of the table, as rules rather than as a screen.
/// </summary>
/// <remarks>
/// This used to live in BlackBoxGame next to the drawing code, which meant I could not play a
/// round without a window open. Now everything takes a SaveData and a Random and returns
/// lines, so the screen and RoundSimulator run the same code. A round is: deal both, the
/// player's turn, the opponent's turn, close. One method each, in that order.
/// </remarks>
public static class RoundEngine
{
    /// <summary>Both hands go in together and both sides are paid.</summary>
    /// <remarks>The player's deal gets <see cref="RoundRules.PlayerFavour"/>. A marked deck's promise goes to the player as promised.</remarks>
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

    /// <summary>Whether the item in the player's hand can go into a pocket.</summary>
    /// <remarks>Not if the pockets are full, and not if it was dealt blind. Pocketing a blind item would dodge what the rotgut charged.</remarks>
    /// <param name="run">The table.</param>
    public static bool CanPocketDealt(SaveData run) =>
        !run.DealtBlind && !run.PocketsFull && ItemCatalog.TryParse(run.Dealt, out _);

    /// <summary>Plays one item out of the player's pockets, mid-turn.</summary>
    /// <param name="run">The table.</param>
    /// <param name="slot">Which pocket, from 0.</param>
    /// <param name="random">The coin, for anything the item flips.</param>
    /// <param name="efficiency">How well the item's check went, 0 to 1.</param>
    /// <returns>What happened, one line at a time. Empty if the slot held nothing.</returns>
    public static List<string> PlayFromPocket(SaveData run, int slot, Random random, float efficiency = 1f)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (slot < 0 || slot >= run.Banked.Count) return new List<string>();
        if (!ItemCatalog.TryParse(run.Banked[slot], out ItemId item))
        {
            // An id this build does not know. Just drop it.
            run.Banked.RemoveAt(slot);
            return new List<string>();
        }

        run.Banked.RemoveAt(slot);
        return ItemResolver.Use(run, item, byPlayer: true, random, efficiency);
    }

    /// <summary>Takes the player's decision about the item in their hand.</summary>
    /// <param name="run">The table.</param>
    /// <param name="choice">What they decided.</param>
    /// <param name="random">The coin, for anything the item flips.</param>
    /// <param name="efficiency">How well the item's check went, 0 to 1. Only matters for a use.</param>
    /// <returns>What happened, one line at a time.</returns>
    public static List<string> DecideDealt(SaveData run, DealtChoice choice, Random random, float efficiency = 1f)
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

        // Emptied before the item resolves, because a second hand needs the hand empty by then.
        run.Dealt = string.Empty;
        run.DealtBlind = false;

        switch (choice)
        {
            case DealtChoice.Use:
                log.AddRange(ItemResolver.Use(run, item, byPlayer: true, random, efficiency));
                break;

            case DealtChoice.Pocket when !blind && !run.PocketsFull:
                run.Banked.Add(ItemCatalog.ToSaveId(item));
                log.Add("YOU PUT THE " + ItemCatalog.NameOf(item).ToUpperInvariant() + " IN YOUR POCKET.");
                break;

            // A refused pocket is a leave. The table disables the button, but the rule lives here.
            default:
                log.Add(blind
                    ? "YOU GIVE IT BACK WITHOUT EVER SEEING WHAT IT WAS."
                    : "YOU LET THE BOX TAKE THE " + ItemCatalog.NameOf(item).ToUpperInvariant() + " BACK.");
                break;
        }

        return log;
    }

    /// <summary>The opponent's whole turn: a look in their pockets, then their decision.</summary>
    /// <remarks>They go second on purpose. They have seen everything the player did with theirs.</remarks>
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

    /// <summary>Closes the round: the count goes up and this round's looks are forgotten.</summary>
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
