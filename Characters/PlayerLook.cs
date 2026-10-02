using System;

namespace TheBlackBox.Characters;

/// <summary>
/// How the player looks: which conscript, the three colours, and what they're wearing. Picked on
/// the customization screen and kept on the save.
/// </summary>
/// <remarks>
/// The colours are saved as preset numbers and not actual colours, so I can tweak a preset later
/// without breaking anyone's save.
/// </remarks>
public class PlayerLook
{
    /// <summary>Which conscript, by name.</summary>
    public string Character { get; set; } = nameof(Conscript.First);

    /// <summary>Which hair preset.</summary>
    public int Hair { get; set; } = 1;

    /// <summary>Which outfit preset.</summary>
    public int Outfit { get; set; }

    /// <summary>Which accent preset. It colours the shirt, belt, collar, and the scarf or cap.</summary>
    public int Accent { get; set; }

    /// <summary>The accessory, by name.</summary>
    public string Accessory { get; set; } = nameof(TheBlackBox.Accessory.None);

    /// <summary>Character as the enum, or First if the save has something it doesn't recognise.</summary>
    public Conscript Who => Enum.TryParse(Character, out Conscript who) && Enum.IsDefined(who) ? who : Conscript.First;

    /// <summary>Accessory as the enum, or None if it doesn't recognise it.</summary>
    public Accessory Wearing =>
        Enum.TryParse(Accessory, out Accessory wearing) && Enum.IsDefined(wearing) ? wearing : TheBlackBox.Accessory.None;

    /// <summary>A copy, so the customization screen can change things without touching the save until you hit SIT DOWN.</summary>
    public PlayerLook Clone() => (PlayerLook)MemberwiseClone();
}
