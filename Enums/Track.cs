namespace TheBlackBox;

/// <summary>
/// The three songs. Each screen asks for one when it comes on, and AudioManager fades between them.
/// </summary>
public enum Track
{
    /// <summary>"It Is Watching": the title screen, the options and the customization screen.</summary>
    Title,

    /// <summary>"Holding": the lobby, waiting to be called.</summary>
    Lobby,

    /// <summary>"Place Your Hand": the table.</summary>
    Arena,
}
