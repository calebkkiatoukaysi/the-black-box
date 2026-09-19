namespace TheBlackBox;

/// <summary>How the opponent sounds. Separate from Guard, which is whether they will tell you anything.</summary>
public enum Warmth
{
    /// <summary>They are done being civil.</summary>
    Hostile,

    /// <summary>Nothing given away either direction.</summary>
    Even,

    /// <summary>They have decided you are a person.</summary>
    Open,
}
