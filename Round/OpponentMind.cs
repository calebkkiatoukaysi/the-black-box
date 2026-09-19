using System;

namespace TheBlackBox;

/// <summary>
/// How the opponent decides whether to use what the box gave them.
/// </summary>
/// <remarks>
/// One set of odds driven by <see cref="Disposition"/> rather than a script per character.
/// This is where talking pays off: an opponent you were cruel to pulls the trigger more.
/// </remarks>
public static class OpponentMind
{
    // The odds behind every decision below. A pair is a floor plus what malice adds, a single
    // number is a flat chance. Tune here, not in the cases.
    private const double BlindGambleBase = 0.25, BlindGambleMalice = 0.5;
    private const double RevolverBase = 0.35, RevolverMalice = 0.6;
    private const double PactBase = 0.4, PactMalice = 0.5;
    private const double CoinBase = 0.3, CoinMalice = 0.4;
    private const double HealOdds = 0.7;
    private const double GuardOdds = 0.3;
    private const double SightOdds = 0.45;
    private const double ConfessionOdds = 0.3;
    private const double LevyBase = 0.35, LevyMalice = 0.35;
    private const double LastCallBase = 0.2, LastCallMalice = 0.3;
    private const double WildCardOdds = 0.6;

    /// <summary>How willing they are to spend a life of yours, from 0 to 1.</summary>
    /// <remarks>Just warmth inverted. Nothing else feeds it, so the player can move it by talking.</remarks>
    /// <param name="disposition">What they think of the player.</param>
    public static float Malice(Disposition disposition) =>
        Math.Clamp(0.5f - disposition.Value / (float)(Disposition.Ceiling - Disposition.Floor), 0f, 1f);

    /// <summary>Decides what the opponent does with the item in their hand.</summary>
    /// <remarks>Same rules as the player: a blind item is used or given back, and a blank is never pocketed.</remarks>
    /// <param name="run">The table as it stands.</param>
    /// <param name="item">What they are holding.</param>
    /// <param name="random">The coin.</param>
    /// <returns>Use it, pocket it, or give it back.</returns>
    public static DealtChoice Decide(SaveData run, ItemId item, Random random)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(random);

        // Playing blind, so it is pure temperament: a hostile one gambles, a warm one lets it go.
        if (run.OpponentDealtBlind)
        {
            return random.NextDouble() < BlindGambleBase + Malice(run.OpponentDisposition) * BlindGambleMalice
                ? DealtChoice.Use
                : DealtChoice.Leave;
        }

        if (WillUse(run, item, random)) return DealtChoice.Use;
        if (ItemCatalog.Get(item).IsBlank || run.OpponentPocketsFull) return DealtChoice.Leave;

        return DealtChoice.Pocket;
    }

    /// <summary>Looks in their pockets for something worth playing before the hand is decided.</summary>
    /// <remarks>They only look <see cref="RoundRules.OpponentPocketOdds"/> of the time, and take the first thing WillUse says yes to.</remarks>
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

    /// <summary>Whether the opponent would play an item now, wherever it came from.</summary>
    /// <remarks>Weapons are scaled by <see cref="RoundRules.OpponentRestraint"/>, which is the trial run going easy.</remarks>
    /// <param name="run">The table as it stands.</param>
    /// <param name="item">The item in question.</param>
    /// <param name="random">The coin.</param>
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

            // A revolver at a player on their last life ends the run, so they always take it.
            case ItemId.Revolver:
                return canFinish || random.NextDouble() < (RevolverBase + malice * RevolverMalice) * restraint;

            // Only worth it if the trade leaves them standing and the player not.
            case ItemId.Pact:
                return run.OpponentLives > run.PlayerLives
                    && random.NextDouble() < (PactBase + malice * PactMalice) * restraint;

            // A coin is worth flipping when you are not the one behind on it.
            case ItemId.Wager:
            case ItemId.HighCard:
                return run.OpponentLives >= run.PlayerLives
                    && random.NextDouble() < (CoinBase + malice * CoinMalice) * restraint;

            case ItemId.Tourniquet:
            case ItemId.Rotgut:
                return run.OpponentLives < SaveData.StartingLives
                    && (cornered || random.NextDouble() < HealOdds);

            // Held until they are scared. A guard spent early is wasted.
            case ItemId.AshVeil:
            case ItemId.Mirror:
                return cornered || random.NextDouble() < GuardOdds;

            // Information is cheap for them.
            case ItemId.Lens:
            case ItemId.Tally:
            case ItemId.MarkedDeck:
                return random.NextDouble() < SightOdds;

            // Asking costs them nothing and warms nobody.
            case ItemId.Confession:
                return random.NextDouble() < ConfessionOdds;

            case ItemId.Levy:
                return random.NextDouble() < (LevyBase + malice * LevyMalice) * restraint;

            // Free, so always.
            case ItemId.SecondHand:
                return true;

            case ItemId.LastCall:
                return random.NextDouble() < LastCallBase + malice * LastCallMalice;

            // Worth only as much as whatever it is about to copy.
            case ItemId.WildCard:
                return ItemCatalog.TryParse(run.LastUsed, out ItemId copied)
                    && copied is not (ItemId.Cinder or ItemId.SpentShell)
                    && random.NextDouble() < WildCardOdds;

            default:
                return false;
        }
    }
}
