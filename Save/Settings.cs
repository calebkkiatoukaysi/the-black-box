using System;

namespace TheBlackBox;

/// <summary>
/// The settings that aren't part of a run: the three volumes.
/// </summary>
/// <remarks>Not in the save slots on purpose. Turning the music down shouldn't depend on which run you loaded.</remarks>
public class Settings
{
    /// <summary>Everything. Gets multiplied into the other two.</summary>
    public float MasterVolume { get; set; } = 0.8f;

    /// <summary>The songs.</summary>
    public float MusicVolume { get; set; } = 0.7f;

    /// <summary>Every sound effect, items included.</summary>
    public float SfxVolume { get; set; } = 0.9f;

    /// <summary>Clamps every volume to 0 to 1, in case the file got edited by hand.</summary>
    /// <returns>This, so it can be chained.</returns>
    public Settings Clamped()
    {
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
        SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
        return this;
    }
}
