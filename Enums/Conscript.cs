namespace TheBlackBox;

/// <summary>
/// The two characters the player can pick. Each has their own walking sheet (conscript-first and conscript-second).
/// </summary>
/// <remarks>Saves store the name, so reordering these is fine but renaming them isn't.</remarks>
public enum Conscript
{
    /// <summary>Short hair, work jacket over a shirt.</summary>
    First,

    /// <summary>Hair tied back, long coat with a belt.</summary>
    Second,
}
