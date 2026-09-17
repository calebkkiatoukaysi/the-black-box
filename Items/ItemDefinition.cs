namespace TheBlackBox;

/// <summary>Who an item is aimed at when it is used.</summary>
/// <remarks>
/// This is what the item points at, not what it finally does. Resolution is the round loop's
/// job; this is here so the table can say, and the table is the only place that has to be
/// edited when it changes.
/// </remarks>
public enum ItemTarget
{
    /// <summary>Nobody. The item does nothing at all.</summary>
    None,

    /// <summary>The player using it.</summary>
    Self,

    /// <summary>The one across the table.</summary>
    Opponent,

    /// <summary>Both sides at once.</summary>
    Both,

    /// <summary>The box, which decides what happens next.</summary>
    Box,
}

/// <summary>
/// One item, as the game knows it: what it is called, what it says it does, and how readily
/// the box parts with it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately inert. There is no <c>Use</c> method and no effect on this class, because an
/// effect needs the round -- two sets of lives, two held items, whatever is guarding each side
/// -- and none of that exists yet. Hanging behaviour off the definition now would mean
/// guessing at the shape of a loop that has not been written, and guessing wrong in twelve
/// places instead of one. When the loop arrives it resolves items by <see cref="Id"/>, and the
/// compiler lists every item it has not handled.
/// </para>
/// <para>
/// A record rather than a class: these are values, there is exactly one of each, and nothing
/// should ever be in a position to edit the catalogue at runtime.
/// </para>
/// </remarks>
/// <param name="Id">Which item this is.</param>
/// <param name="Name">What the item is called on screen, in the box's own words.</param>
/// <param name="Description">What the item claims it does, in one line the player can read at a glance.</param>
/// <param name="Target">Who the item is pointed at. See <see cref="ItemTarget"/>.</param>
/// <param name="Weight">
/// How heavily the box favours this item when it deals. Relative to every other weight in the
/// catalogue rather than a percentage, so an item can be added or retuned without every other
/// number having to be corrected to keep a total at 100.
/// </param>
public readonly record struct ItemDefinition(
    ItemId Id,
    string Name,
    string Description,
    ItemTarget Target,
    int Weight)
{
    /// <summary>
    /// Whether the item does nothing, and the hand that bought it bought nothing.
    /// </summary>
    /// <remarks>
    /// Worth asking about rather than comparing against <see cref="ItemId.Cinder"/> everywhere,
    /// because a blank is a kind of item and not one specific item -- the box is cruel and
    /// there will be more of them.
    /// </remarks>
    public bool IsBlank => Target == ItemTarget.None;
}
