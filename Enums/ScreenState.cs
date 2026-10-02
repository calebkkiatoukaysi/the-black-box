namespace TheBlackBox;

/// <summary>
/// Where a screen is in its life on the ScreenManager's stack. From the Game State Management tutorial.
/// </summary>
public enum ScreenState
{
    /// <summary>Fading in.</summary>
    TransitionOn,

    /// <summary>Fully on and taking input, if nothing above it has focus.</summary>
    Active,

    /// <summary>Fading out, either to make room for a screen on top or because it is leaving for good.</summary>
    TransitionOff,

    /// <summary>Covered by another screen and not drawn.</summary>
    Hidden,
}
