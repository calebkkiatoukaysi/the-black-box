using System;

namespace TheBlackBox;

/// <summary>
/// The options that belong to the machine rather than to a run: the three volumes.
/// </summary>
/// <remarks>Kept out of the save slots on purpose. Turning the music down should not depend on which run is loaded.</remarks>
public class Settings
{
    /// <summary>Everything, multiplied into the other two.</summary>
    public float MasterVolume { get; set; } = 0.8f;

    /// <summary>The songs.</summary>
    public float MusicVolume { get; set; } = 0.7f;

    /// <summary>Every sound effect, items included.</summary>
    public float SfxVolume { get; set; } = 0.9f;

    /// <summary>Pulls every volume back into 0 to 1, for a file that was edited by hand.</summary>
    /// <returns>This, so it can be chained after a read.</returns>
    public Settings Clamped()
    {
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
        SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
        return this;
    }
}
