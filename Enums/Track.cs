namespace TheBlackBox;

/// <summary>
/// The three songs. Each screen asks for one when it opens and AudioManager fades between them.
/// </summary>
public enum Track
{
    /// <summary>"It Is Watching": the title screen, the options and the customization screen.</summary>
    Title,

    /// <summary>"Holding": the lobby.</summary>
    Lobby,

    /// <summary>"Place Your Hand": the table.</summary>
    Arena,
}
