using Microsoft.Xna.Framework;

namespace TheBlackBox;

/// <summary>
/// Colours and shadows more than one screen uses. Anything only one class uses stays there.
/// </summary>
public static class Palette
{
    /// <summary>How far every text shadow sits down and right of its text.</summary>
    public static readonly Vector2 ShadowOffset = new(2f, 2f);

    /// <summary>The usual text shadow.</summary>
    public static readonly Color Shadow = Color.Black * 0.6f;

    /// <summary>A bit heavier, for the forms over the title screen.</summary>
    public static readonly Color FormShadow = Color.Black * 0.7f;

    /// <summary>Heavier still, under button labels, which sit on lit plates.</summary>
    public static readonly Color LabelShadow = Color.Black * 0.75f;

    /// <summary>The heaviest, under the verdict at the end of a run.</summary>
    public static readonly Color HeavyShadow = Color.Black * 0.8f;

    /// <summary>The grey the bookkeeping is written in.</summary>
    public static readonly Color DimText = new(122, 112, 114);

    /// <summary>The dark the forms veil the title screen with. Tinted, not flat black, so the title dims instead of going grey.</summary>
    public static readonly Color Veil = new(6, 5, 10);

    /// <summary>The heading on a form.</summary>
    public static readonly Color Heading = new(214, 206, 200);

    /// <summary>A sight once the shot has gone, and a tag picked wrong.</summary>
    public static readonly Color FiredTint = new(255, 96, 72);

    /// <summary>A tag picked right.</summary>
    public static readonly Color PickedTint = new(160, 255, 160);
}
