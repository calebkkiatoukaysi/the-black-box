namespace TheBlackBox;

/// <summary>
/// Which part of a round the table is in. The order here is the order a round goes in.
/// </summary>
internal enum RoundPhase
{
    /// <summary>The discussion period. See <see cref="DiscussionPeriod"/>.</summary>
    Discussion,

    /// <summary>The box is waiting for a hand.</summary>
    Offer,

    /// <summary>A hand is going in, or being held.</summary>
    Reaching,

    /// <summary>The payout is skidding down the table and the player has to catch it.</summary>
    Catching,

    /// <summary>The player's turn: play from the pockets, then decide on the item in hand.</summary>
    PlayerTurn,

    /// <summary>A skill check is running for the item the player chose to use.</summary>
    SkillCheck,

    /// <summary>What has happened, read one line at a time.</summary>
    Resolving,

    /// <summary>Somebody is out of lives and the run is finished.</summary>
    Over,
}
