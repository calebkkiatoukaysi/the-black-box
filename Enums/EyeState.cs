namespace TheBlackBox;

/// <summary>
/// The stages of a single blink. An eye rests on Open and only passes through the other three on the way back to it.
/// </summary>
public enum EyeState
{
    Open = 0,
    Closing = 1,
    Closed = 2,
    Opening = 3,
}
