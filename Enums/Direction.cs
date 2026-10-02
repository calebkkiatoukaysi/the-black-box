namespace TheBlackBox;

/// <summary>
/// Which way a figure in the lobby is facing. The value is the row on its walking sheet.
/// </summary>
public enum Direction
{
    /// <summary>Toward the viewer.</summary>
    Down,

    /// <summary>To the left of the screen.</summary>
    Left,

    /// <summary>To the right of the screen.</summary>
    Right,

    /// <summary>Away from the viewer.</summary>
    Up,
}
