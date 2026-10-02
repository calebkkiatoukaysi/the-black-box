namespace TheBlackBox;

/// <summary>
/// Every sound effect that isn't an item. The file is the name in kebab case under Content/Sfx,
/// so MenuMove is Sfx/menu-move. Items each have their own sound under Sfx/Items.
/// </summary>
/// <remarks>Grouped by where you hear them. If you rename one, rename its file and its line in Content.mgcb too.</remarks>
public enum Sfx
{
    // Menus.

    /// <summary>Moving the cursor to another plate.</summary>
    MenuMove,

    /// <summary>Pressing a plate.</summary>
    MenuConfirm,

    /// <summary>Backing out of a menu.</summary>
    MenuBack,

    /// <summary>Changing a value with left or right (options and customization).</summary>
    OptionChange,

    /// <summary>A key typed into the name field.</summary>
    Type,

    /// <summary>Opening the pause menu.</summary>
    Pause,

    // The lobby.

    /// <summary>One character of dialogue getting typed out. The pitch changes per speaker.</summary>
    TextBlip,

    /// <summary>One footstep. The pitch and pan change every step.</summary>
    Footstep,

    /// <summary>Trying the door before you've been challenged.</summary>
    DoorLocked,

    /// <summary>The door unlocking after you get challenged.</summary>
    DoorOpen,

    /// <summary>Walking through the door to the table.</summary>
    EnterArena,

    /// <summary>Walking over an item with full pockets.</summary>
    PocketsFull,

    // The table.

    /// <summary>The box opening up for a hand.</summary>
    BoxOpen,

    /// <summary>The box taking the hand.</summary>
    HandIn,

    /// <summary>The box throwing the tag out.</summary>
    Payout,

    /// <summary>The tag landing in the palm.</summary>
    Catch,

    /// <summary>The tag going off the edge of the table.</summary>
    Drop,

    /// <summary>Passing a skill check.</summary>
    CheckGood,

    /// <summary>Failing a skill check.</summary>
    CheckBad,

    /// <summary>The opponent losing a life.</summary>
    Hit,

    /// <summary>The player losing a life.</summary>
    Damage,

    /// <summary>Either side getting a life back.</summary>
    Heal,

    /// <summary>A veil or mirror blocking something.</summary>
    Guard,

    /// <summary>The verdict when the player survives.</summary>
    Win,

    /// <summary>The verdict when they don't.</summary>
    Lose,
}
