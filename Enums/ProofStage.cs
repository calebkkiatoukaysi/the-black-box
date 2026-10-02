namespace TheBlackBox;

/// <summary>
/// Which leg of the proof run is playing. The run goes the way a player would, one screen after another.
/// </summary>
public enum ProofStage
{
    /// <summary>The title, the options and the save form.</summary>
    Menus,

    /// <summary>Dressing and naming the scratch run.</summary>
    Customization,

    /// <summary>The lobby, up to going through the door.</summary>
    Lobby,

    /// <summary>The table, through both verdicts and into the next chapter.</summary>
    Table,

    /// <summary>Back in the lobby off the verdict, and out through the pause menu.</summary>
    LobbyAgain,

    /// <summary>The title again, which is the end of it.</summary>
    TitleAgain,

    /// <summary>Writing the audio log and quitting.</summary>
    Done,
}
