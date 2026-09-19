namespace TheBlackBox;

/// <summary>
/// What the player can do with the item in their hand.
/// </summary>
public enum DealtChoice
{
    /// <summary>Play it now. It is destroyed.</summary>
    Use,

    /// <summary>Put it in a pocket, to be played on a later turn.</summary>
    Pocket,

    /// <summary>Give it back to the box. Nothing happens.</summary>
    Leave,
}
