namespace TheBlackBox;

/// <summary>
/// Where a lobby prop sits in the picture, which decides what it sorts against.
/// </summary>
public enum PropPlacement
{
    /// <summary>Standing on the floor: sorted against the figures by how far down the room its foot is, and solid.</summary>
    Floor,

    /// <summary>Fixed to the wall: always behind the figures, never in the way.</summary>
    Wall,
}
