namespace TheBlackBox;

/// <summary>
/// The two people the player can be. Each has their own walking sheet, conscript-first and conscript-second.
/// </summary>
/// <remarks>Saves store the name, so reordering is safe and renaming is not.</remarks>
public enum Conscript
{
    /// <summary>Short hair, a work jacket over a shirt.</summary>
    First,

    /// <summary>Hair tied back, a long belted coat.</summary>
    Second,
}
