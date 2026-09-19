namespace TheBlackBox;

/// <summary>Who an item is aimed at. What it does to them is ItemResolver's job.</summary>
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
