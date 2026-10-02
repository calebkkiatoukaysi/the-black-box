namespace TheBlackBox;

/// <summary>
/// Where a lobby prop sits, which decides how it gets sorted.
/// </summary>
public enum PropPlacement
{
    /// <summary>On the floor. Sorted with the people by how far down the room it is, and you can't walk through it.</summary>
    Floor,

    /// <summary>On the wall. Always drawn behind people and never blocks anything.</summary>
    Wall,
}
