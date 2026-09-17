namespace TheBlackBox;

/// <summary>
/// The numbers that decide how hard the table is.
/// </summary>
/// <remarks>
/// <para>
/// All in one place, because these are the dials the game is tuned with and a dial buried
/// in the code that reads it is a dial nobody turns. This is the trial run, and it is meant
/// to lean the player's way: the point of the first table is to teach how a round works,
/// and a player who is shot dead while they are still learning what a pocket is has been
/// taught nothing.
/// </para>
/// <para>
/// None of it is hidden from the maths. <c>dotnet run -- --simulate 5000</c> plays the table
/// headless with these numbers and prints who wins, which is how they were chosen -- see
/// <see cref="RoundSimulator"/>. Change one here and run that before trusting it.
/// </para>
/// </remarks>
public static class RoundRules
{
    /// <summary>
    /// How many items either side can carry. Three each, the same on both sides of the
    /// table, because the pocket is the one part of the game the box is not in charge of
    /// and it should not favour anyone.
    /// </summary>
    public const int PocketSlots = 3;

    /// <summary>
    /// How often the box quietly takes back a blank or a pact it was about to hand the
    /// player, and deals again.
    /// </summary>
    /// <remarks>
    /// 0 is the box as it is written in <see cref="ItemCatalog"/>; 1 would never let the
    /// player draw one. It is a second draw and not a guarantee, so a bad hand is still
    /// possible and the player still learns what one costs.
    /// </remarks>
    public const float PlayerFavour = 0.5f;

    /// <summary>
    /// What the opponent's willingness to fire a weapon is multiplied by. Below 1 they hold
    /// their fire more often than their temper alone would have them.
    /// </summary>
    public const float OpponentRestraint = 0.7f;

    /// <summary>
    /// How many items the opponent may play out of their pockets in one turn. The player
    /// is not limited, and that asymmetry is most of what makes the trial run winnable.
    /// </summary>
    public const int OpponentPocketPlays = 1;

    /// <summary>
    /// The odds the opponent looks in their pockets at all on a given turn. They are a
    /// person, not a player who has read the item list, and they forget what they have.
    /// </summary>
    public const float OpponentPocketOdds = 0.5f;

    /// <summary>
    /// How many rounds the simulator lets a run go before calling it unfinished. A real run
    /// has no cap; this only stops two very cautious sides sitting there forever.
    /// </summary>
    public const int SimulatedRoundCap = 40;
}
