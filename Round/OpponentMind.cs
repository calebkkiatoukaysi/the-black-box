using System;

namespace TheBlackBox;

/// <summary>
/// How the opponent decides whether to use what the box gave them.
/// </summary>
/// <remarks>
/// <para>
/// One function over a few numbers rather than a script per character, so a new opponent is
/// a new set of lines and a new sheet rather than new code. What makes them play differently
/// is the same <see cref="Disposition"/> that makes them talk differently: a man who has
/// decided he wants you to lose pulls the trigger at odds a man who has not would refuse.
/// </para>
/// <para>
/// That is the payoff of the discussion period. Talking is not flavour between rounds -- it
/// is the input to this, and a player who spends a night being cruel is playing against a
/// different opponent by the end of it.
/// </para>
/// </remarks>
public static class OpponentMind
{
    /// <summary>
    /// How willing they are to spend a life of yours, from 0 to 1.
    /// </summary>
    /// <remarks>
    /// Straight off warmth, inverted: fully hostile is entirely willing, fully open is
    /// entirely unwilling, and the neutral middle is a coin. Nothing else feeds it, because
    /// the whole point is that the player can move this number by talking.
    /// </remarks>
    /// <param name="disposition">What they think of the player.</param>
    public static float Malice(Disposition disposition) =>
        Math.Clamp(0.5f - disposition.Value / 200f, 0f, 1f);

    /// <summary>
    /// Decides what the opponent does with the item in their hand.
    /// </summary>
    /// <remarks>
    /// The same rule the player is held to: a blind item is used or given back, never
    /// pocketed, and a blank is not worth a pocket to anyone.
    /// </remarks>
    /// <param name="run">The table as it stands.</param>
    /// <param name="item">What they are holding.</param>
    /// <param name="random">The coin, for everything that is not already decided.</param>
    /// <returns>Use it, pocket it, or give it back.</returns>
    public static DealtChoice Decide(SaveData run, ItemId item, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(random);

        // They are playing blind. There is nothing to reason about, so it comes down to
        // temperament: a hostile one gambles, a warm one lets it go.
        if (run.OpponentDealtBlind)
        {
            return random.NextDouble() < 0.25 + Malice(run.OpponentDisposition) * 0.5
                ? DealtChoice.Use
                : DealtChoice.Leave;
        }

        if (WillUse(run, item, random)) return DealtChoice.Use;
        if (ItemCatalog.Get(item).IsBlank || run.OpponentPocketsFull) return DealtChoice.Leave;

        return DealtChoice.Pocket;
    }

    /// <summary>
    /// Looks in their pockets for something worth playing before the hand is decided.
    /// </summary>
    /// <remarks>
    /// They do not always look. <see cref="RoundRules.OpponentPocketOdds"/> is how often
    /// they remember what they are carrying, and the first thing in there that
    /// <see cref="WillUse"/> would play is the one they take out. One a turn, which the
    /// caller enforces; the player has no such limit.
    /// </remarks>
    /// <param name="run">The table as it stands.</param>
    /// <param name="random">The coin.</param>
    /// <returns>Which pocket to play, or -1 to leave them alone.</returns>
    public static int ChoosePocketPlay(SaveData run, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(random);

        if (run.OpponentBanked.Count == 0 || random.NextDouble() >= RoundRules.OpponentPocketOdds) return -1;

        for (int i = 0; i < run.OpponentBanked.Count; i++)
        {
            if (ItemCatalog.TryParse(run.OpponentBanked[i], out ItemId item) && WillUse(run, item, random))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Whether the opponent would play an item now, wherever it came from.
    /// </summary>
    /// <remarks>
    /// Weapons run through <see cref="RoundRules.OpponentRestraint"/>, which is the trial
    /// run holding their hand: the odds their temper gives them are what they would be at a
    /// harder table, and the restraint is what is taken off for this one.
    /// </remarks>
    /// <param name="run">The table as it stands.</param>
    /// <param name="item">The item in question.</param>
    /// <param name="random">The coin, for everything that is not already decided.</param>
    /// <returns>True to play it now, false to hold it.</returns>
    public static bool WillUse(SaveData run, ItemId item, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(random);

        float malice = Malice(run.OpponentDisposition);
        float restraint = RoundRules.OpponentRestraint;
        bool cornered = run.OpponentLives <= 1;
        bool canFinish = run.PlayerLives <= 1;

        switch (item)
        {
            // Nothing to gain by spending a blank.
            case ItemId.Cinder:
            case ItemId.SpentShell:
                return false;

            // A revolver at a player on their last life ends the run, and they take that
            // whatever they think of you. Otherwise it is temperament.
            case ItemId.Revolver:
                return canFinish || random.NextDouble() < (0.35 + malice * 0.6) * restraint;

            // Only worth it if the trade leaves them standing and the player not.
            case ItemId.Pact:
                return run.OpponentLives > run.PlayerLives
                    && random.NextDouble() < (0.4 + malice * 0.5) * restraint;

            // A coin is worth flipping when you are not the one behind on it.
            case ItemId.Wager:
            case ItemId.HighCard:
                return run.OpponentLives >= run.PlayerLives
                    && random.NextDouble() < (0.3 + malice * 0.4) * restraint;

            case ItemId.Tourniquet:
            case ItemId.Rotgut:
                return run.OpponentLives < SaveData.StartingLives
                    && (cornered || random.NextDouble() < 0.7);

            // Held until they are frightened, because a guard spent early is a guard wasted.
            case ItemId.AshVeil:
            case ItemId.Mirror:
                return cornered || random.NextDouble() < 0.3;

            // Information is cheap for them and they have no face to give away.
            case ItemId.Lens:
            case ItemId.Tally:
            case ItemId.MarkedDeck:
                return random.NextDouble() < 0.45;

            // Asking costs them nothing and warms nobody.
            case ItemId.Confession:
                return random.NextDouble() < 0.3;

            case ItemId.Levy:
                return random.NextDouble() < (0.35 + malice * 0.35) * restraint;

            // Free, and the box is the only thing at this table that never runs out.
            case ItemId.SecondHand:
                return true;

            case ItemId.LastCall:
                return random.NextDouble() < 0.2 + malice * 0.3;

            // Worth only as much as whatever it is about to copy.
            case ItemId.WildCard:
                return ItemCatalog.TryParse(run.LastUsed, out ItemId copied)
                    && copied is not (ItemId.Cinder or ItemId.SpentShell)
                    && random.NextDouble() < 0.6;

            default:
                return false;
        }
    }
}
