namespace TheBlackBox;

/// <summary>
/// Which part of the proof run is playing. It goes through the screens in the same order a player would.
/// </summary>
public enum ProofStage
{
    /// <summary>The title, the options and the save form.</summary>
    Menus,

    /// <summary>Customizing and naming the scratch run.</summary>
    Customization,

    /// <summary>The lobby, up to going through the door.</summary>
    Lobby,

    /// <summary>The table, through both verdicts and into the next chapter.</summary>
    Table,

    /// <summary>Back in the lobby after the verdict, then out through the pause menu.</summary>
    LobbyAgain,

    /// <summary>Back on the title, which is the end.</summary>
    TitleAgain,

    /// <summary>Writing the audio log and quitting.</summary>
    Done,
}
