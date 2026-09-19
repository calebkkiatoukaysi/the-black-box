namespace TheBlackBox;

/// <summary>
/// One row of the item table: name, description, target, and how often the box deals it.
/// </summary>
/// <remarks>
/// No Use method on purpose. Effects need the whole round (lives, held items, guards), so
/// ItemResolver switches on the Id instead and the compiler tells me if I miss one.
/// </remarks>
/// <param name="Id">Which item this is.</param>
/// <param name="Name">What it is called on screen.</param>
/// <param name="Description">One line on what it claims to do.</param>
/// <param name="Target">Who it is pointed at.</param>
/// <param name="Weight">How often the box deals it, relative to the other weights.</param>
public readonly record struct ItemDefinition(
    ItemId Id,
    string Name,
    string Description,
    ItemTarget Target,
    int Weight)
{
    /// <summary>Whether the item does nothing. A kind of item, not one specific id, since there is more than one blank.</summary>
    public bool IsBlank => Target == ItemTarget.None;
}
