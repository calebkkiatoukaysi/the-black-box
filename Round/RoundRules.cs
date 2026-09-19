namespace TheBlackBox;

/// <summary>
/// The numbers that decide how hard the table is.
/// </summary>
/// <remarks>
/// All in one place so they actually get tuned. This is the trial run and it is meant to lean
/// the player's way while they learn. I picked these with `dotnet run -- --simulate 5000`
/// (see RoundSimulator); change one and run that before trusting it.
/// </remarks>
public static class RoundRules
{
    /// <summary>How many items either side can carry. The same on both sides on purpose.</summary>
    public const int PocketSlots = 3;

    /// <summary>How often the box takes back a blank or a pact it was about to deal the player, and deals again.</summary>
    /// <remarks>0 is no favour, 1 would never deal one. It is one redraw, not a guarantee, so a bad hand is still possible.</remarks>
    public const float PlayerFavour = 0.5f;

    /// <summary>What the opponent's willingness to fire is multiplied by. Below 1 they hold fire more than their temper would.</summary>
    public const float OpponentRestraint = 0.7f;

    /// <summary>How many pocket items the opponent may play in one turn. The player is not limited, which is most of what makes the trial run winnable.</summary>
    public const int OpponentPocketPlays = 1;

    /// <summary>The odds the opponent looks in their pockets at all on a turn. They forget what they have.</summary>
    public const float OpponentPocketOdds = 0.5f;

    /// <summary>How many rounds the simulator allows before calling a run unfinished. A real run has no cap.</summary>
    public const int SimulatedRoundCap = 40;
}
