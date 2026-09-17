namespace TheBlackBox;

/// <summary>
/// Every item the box can deal, by name.
/// </summary>
/// <remarks>
/// <para>
/// An enum rather than the loose strings <see cref="SaveData.Flags"/> uses, because these two
/// lists are not the same kind of list. A flag is a fact that happened and nothing but the
/// writing cares about it, so the set is open and grows every time a line of dialogue is
/// added. An item is something the round loop has to reason about -- deal it, hold it, spend
/// it, resolve it -- and every one of those places wants the compiler to say when a case has
/// been missed. There will be a few dozen of these at most, and the game is wrong if it is
/// ever handed one it does not recognise.
/// </para>
/// <para>
/// The names are the ids. They are written into saves as text by <see cref="ItemCatalog"/>
/// rather than as the numbers behind them, so reordering this enum cannot silently turn one
/// player's revolver into somebody else's tourniquet, and a save stays readable by hand.
/// Members may be reordered or inserted freely; renaming or removing one is the change that
/// breaks old saves, and <see cref="ItemCatalog.TryParse"/> is what stops that breaking a run.
/// </para>
/// <para>
/// Grouped by what an item is for rather than alphabetically, because the groups are how the
/// set is balanced -- the question being asked while tuning is nearly always "how much of the
/// table is a weapon" and never "what comes after Pact".
/// </para>
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
