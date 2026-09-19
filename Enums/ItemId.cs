namespace TheBlackBox;

/// <summary>
/// Every item the box can deal.
/// </summary>
/// <remarks>
/// An enum, not strings, so the round code gets a compiler error when a case is missed. Saves
/// store the name and not the number, so reordering here is safe but renaming breaks old saves.
/// Grouped by what an item is for, since that is how I balance them.
/// </remarks>
public enum ItemId
{
    // Blanks. The box took a hand and gave back nothing worth having.

    /// <summary>Ash pressed into a shape. Does nothing.</summary>
    Cinder,

    /// <summary>A fired casing. Does nothing.</summary>
    SpentShell,

    // Weapons. Lives come off the table.

    /// <summary>Takes a life off the opponent.</summary>
    Revolver,

    /// <summary>Takes a life off both sides.</summary>
    Pact,

    /// <summary>Takes a life off one side, and the box picks which.</summary>
    Wager,

    /// <summary>Both sides cut the deck, and the low card pays.</summary>
    HighCard,

    // Guards. Lives stay on the table.

    /// <summary>Gives a life back, up to where the run started.</summary>
    Tourniquet,

    /// <summary>Stops the next item used against you.</summary>
    AshVeil,

    /// <summary>Sends the next item used against you back the way it came.</summary>
    Mirror,

    // Sight. The only answer to items nobody can see.

    /// <summary>Shows what the opponent is holding this round.</summary>
    Lens,

    /// <summary>Shows how much the opponent has put away.</summary>
    Tally,

    /// <summary>Shows what the box will deal next.</summary>
    MarkedDeck,

    /// <summary>Binds the opponent to the truth for one exchange.</summary>
    Confession,

    // Interference. Everything else on the table gets moved around.

    /// <summary>Burns what the opponent is holding this round, unused.</summary>
    Levy,

    /// <summary>Buys another item without waiting for the next round.</summary>
    SecondHand,

    /// <summary>Becomes the last item used at the table.</summary>
    WildCard,

    /// <summary>Trades sight for a life: you drink, and are dealt blind.</summary>
    Rotgut,

    /// <summary>Both sides drink, and both are dealt blind.</summary>
    LastCall,
}
