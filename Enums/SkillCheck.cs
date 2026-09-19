namespace TheBlackBox;

/// <summary>
/// Which minigame runs before an item resolves.
/// </summary>
/// <remarks>
/// Three checks shared by kind of item, not one per item. Eighteen minigames would have been
/// too much. Each gives an efficiency from 0 to 1; the opponent and the simulator always pass 1.
/// </remarks>
public enum SkillCheck
{
    /// <summary>Nothing. The item resolves as it always has.</summary>
    None,

    /// <summary>Weapons. A sight over the opponent; how close it was is how well it lands.</summary>
    Aim,

    /// <summary>Guards. Hold an ember in the light; how long it was held is how well the guard holds.</summary>
    Steady,

    /// <summary>Sight items. Pick the marked tag out of three, or the reading is nothing.</summary>
    Read,
}
