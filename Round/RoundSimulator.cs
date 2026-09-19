using System;
using System.Globalization;

namespace TheBlackBox;

/// <summary>
/// Plays the table headless, thousands of times, and says who wins.
/// </summary>
/// <remarks>
/// Run with `dotnet run -- --simulate 5000`. No window, no dialogue, just RoundEngine and a
/// counter. The stand-in player is a plain one (shoots when it can, heals when hurt, keeps a
/// guard for when it is scared) because that is about what a third-round player does. The
/// discussion is not simulated; the table is played at each of the three tempers instead.
/// </remarks>
public static class RoundSimulator
{
    /// <summary>How far from even the hostile and open tempers are sampled at.</summary>
    private const int TemperSpread = 60;

    /// <summary>A fixed seed, so two runs of the simulator print the same table.</summary>
    private const int Seed = 12345;

    /// <summary>Plays this many runs at each temper and prints the results.</summary>
    /// <param name="runs">How many runs per temper.</param>
    public static void Run(int runs)
    {
        runs = Math.Max(1, runs);

        Console.WriteLine("The Black Box  --  " + runs.ToString(CultureInfo.InvariantCulture)
            + " runs per temper, favour " + RoundRules.PlayerFavour.ToString(CultureInfo.InvariantCulture)
            + ", restraint " + RoundRules.OpponentRestraint.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine();
        Console.WriteLine("TEMPER     WON     LOST   UNFINISHED   ROUNDS");

        foreach ((string name, int warmth) in new[] { ("HOSTILE", -TemperSpread), ("EVEN", 0), ("OPEN", TemperSpread) })
        {
            var random = new Random(warmth + Seed);
            int won = 0, lost = 0, unfinished = 0, rounds = 0;

            for (int i = 0; i < runs; i++)
            {
                SaveData run = SaveData.NewRun();
                run.PlayerName = "sim";
                run.OpponentDisposition = new Disposition(warmth, Disposition.Neutral.Guard);

                Play(run, random);

                rounds += run.Round;
                if (run.OpponentLives <= 0 && run.PlayerLives > 0) won++;
                else if (run.PlayerLives <= 0) lost++;
                else unfinished++;
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-8}  {1,5:P0}   {2,5:P0}   {3,8:P0}   {4,6:F1}",
                name, won / (double)runs, lost / (double)runs, unfinished / (double)runs, rounds / (double)runs));
        }
    }

    /// <summary>Plays one run to its end, or to the round cap.</summary>
    /// <param name="run">A fresh run.</param>
    /// <param name="random">The box's hand.</param>
    private static void Play(SaveData run, Random random)
    {
        while (!RoundEngine.IsOver(run) && run.Round < RoundRules.SimulatedRoundCap)
        {
            RoundEngine.DealBoth(run, random);

            PlayerTurn(run, random);

            if (!RoundEngine.IsOver(run)) RoundEngine.OpponentTurn(run, random);

            RoundEngine.CloseRound(run);
        }
    }

    /// <summary>The stand-in player's turn: pockets first, then the hand.</summary>
    /// <param name="run">The table.</param>
    /// <param name="random">The coin.</param>
    private static void PlayerTurn(SaveData run, Random random)
    {
        // Pockets first, front to back, whenever something in there is worth playing.
        for (int slot = 0; slot < run.Banked.Count && !RoundEngine.IsOver(run);)
        {
            if (ItemCatalog.TryParse(run.Banked[slot], out ItemId item) && WantsToPlay(run, item, fromPocket: true))
                RoundEngine.PlayFromPocket(run, slot, random);
            else
                slot++;
        }

        if (RoundEngine.IsOver(run)) return;

        // Then the hand. A second hand can refill it, so loop until it is empty.
        while (ItemCatalog.TryParse(run.Dealt, out ItemId held) && !RoundEngine.IsOver(run))
        {
            DealtChoice choice;

            if (run.DealtBlind) choice = random.NextDouble() < 0.5 ? DealtChoice.Use : DealtChoice.Leave;
            else if (WantsToPlay(run, held, fromPocket: false)) choice = DealtChoice.Use;
            else if (ItemCatalog.Get(held).IsBlank || held == ItemId.Pact) choice = DealtChoice.Leave;
            else if (RoundEngine.CanPocketDealt(run)) choice = DealtChoice.Pocket;
            else choice = DealtChoice.Leave;

            RoundEngine.DecideDealt(run, choice, random);
        }
    }

    /// <summary>What a plain player does with an item, whether it came from a pocket or the box.</summary>
    /// <param name="run">The table.</param>
    /// <param name="item">The item in question.</param>
    /// <param name="fromPocket">True if it is already pocketed, which makes it worth more.</param>
    private static bool WantsToPlay(SaveData run, ItemId item, bool fromPocket)
    {
        bool hurt = run.PlayerLives < SaveData.StartingLives;
        bool cornered = run.PlayerLives <= 1;
        bool guarded = ItemCatalog.TryParse(run.PlayerWard, out _);

        return item switch
        {
            ItemId.Revolver => true,
            ItemId.Wager or ItemId.HighCard => run.PlayerLives > run.OpponentLives,
            ItemId.Tourniquet => hurt,
            ItemId.Rotgut => cornered,
            ItemId.AshVeil or ItemId.Mirror => !guarded && (cornered || !fromPocket && run.PocketsFull),
            ItemId.Lens or ItemId.Tally or ItemId.MarkedDeck or ItemId.Confession => !fromPocket,
            ItemId.Levy or ItemId.SecondHand => !fromPocket,
            ItemId.WildCard => ItemCatalog.TryParse(run.LastUsed, out ItemId last) && last == ItemId.Revolver,
            _ => false,
        };
    }
}
