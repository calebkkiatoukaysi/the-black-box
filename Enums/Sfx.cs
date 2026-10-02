namespace TheBlackBox;

/// <summary>
/// Every sound effect that is not an item. The file is the name in kebab case under Content/Sfx,
/// so MenuMove is Sfx/menu-move. Items have their own sounds, one per ItemId, under Sfx/Items.
/// </summary>
/// <remarks>Grouped by where they are heard. Renaming one means renaming its file and its line in Content.mgcb.</remarks>
public enum Sfx
{
    // Menus.

    /// <summary>The cursor moving onto another plate.</summary>
    MenuMove,

    /// <summary>A plate pressed.</summary>
    MenuConfirm,

    /// <summary>Backing out of a menu.</summary>
    MenuBack,

    /// <summary>A value changed with left or right, on the options or the customization screen.</summary>
    OptionChange,

    /// <summary>A key typed into the name field.</summary>
    Type,

    /// <summary>The pause menu coming up.</summary>
    Pause,

    // The lobby.

    /// <summary>One character of a line being typed out. Re-pitched per speaker.</summary>
    TextBlip,

    /// <summary>One step on concrete. Re-pitched and panned per step.</summary>
    Footstep,

    /// <summary>Trying the door to the box before anyone has asked you to.</summary>
    DoorLocked,

    /// <summary>The door to the box unlocking once you have been challenged.</summary>
    DoorOpen,

    /// <summary>Walking through the door, into the box's room.</summary>
    EnterArena,

    /// <summary>Walking over something with nowhere to put it.</summary>
    PocketsFull,

    // The table.

    /// <summary>The box's jaws drawing back for a hand.</summary>
    BoxOpen,

    /// <summary>The box taking the hand.</summary>
    HandIn,

    /// <summary>The tag thrown out of the mouth.</summary>
    Payout,

    /// <summary>The tag landing in the palm.</summary>
    Catch,

    /// <summary>The tag going off the edge of the table.</summary>
    Drop,

    /// <summary>A skill check done well.</summary>
    CheckGood,

    /// <summary>A skill check done badly.</summary>
    CheckBad,

    /// <summary>A life taken off the opponent.</summary>
    Hit,

    /// <summary>A life taken off the player.</summary>
    Damage,

    /// <summary>A life given back, to either side.</summary>
    Heal,

    /// <summary>A veil or a mirror stopping something.</summary>
    Guard,

    /// <summary>The verdict when the player gets up from the table.</summary>
    Win,

    /// <summary>The verdict when they do not.</summary>
    Lose,
}
