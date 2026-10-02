using System;

namespace TheBlackBox.Characters;

/// <summary>
/// How the player looks: who they are, the three colours picked for them, and what they have on.
/// Chosen on the customization screen and kept on the save.
/// </summary>
/// <remarks>
/// The colours are positions in the preset lists (CharacterPalettes), not colours, so a preset
/// can be retuned without touching anybody's save. Anything out of range reads as the first.
/// </remarks>
public class PlayerLook
{
    /// <summary>Which of the two, by name.</summary>
    public string Character { get; set; } = nameof(Conscript.First);

    /// <summary>Which hair preset.</summary>
    public int Hair { get; set; } = 1;

    /// <summary>Which outfit preset.</summary>
    public int Outfit { get; set; }

    /// <summary>Which accent preset. It colours the shirt, belt and collar, and whatever is worn on top.</summary>
    public int Accent { get; set; }

    /// <summary>What is worn on top, by name.</summary>
    public string Accessory { get; set; } = nameof(TheBlackBox.Accessory.None);

    /// <summary><see cref="Character"/> as the enum, or First if the save holds something this build does not know.</summary>
    public Conscript Who => Enum.TryParse(Character, out Conscript who) && Enum.IsDefined(who) ? who : Conscript.First;

    /// <summary><see cref="Accessory"/> as the enum, or None for anything unknown.</summary>
    public Accessory Wearing =>
        Enum.TryParse(Accessory, out Accessory wearing) && Enum.IsDefined(wearing) ? wearing : TheBlackBox.Accessory.None;

    /// <summary>A copy, so the customization screen can try things on without touching the save until it is confirmed.</summary>
    public PlayerLook Clone() => (PlayerLook)MemberwiseClone();
}
