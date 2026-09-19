using Microsoft.Xna.Framework;

namespace TheBlackBox;

/// <summary>
/// One opponent: the id on the save, their sheet and how to draw it, and their script.
/// </summary>
/// <remarks>
/// Frame size and scale are per opponent because the two are not made the same way. The first
/// is her picture sampled to art pixels at 4x, the second is the character sheet at 6x.
/// Anything that changes during a run (disposition, lives) lives on SaveData, not here.
/// </remarks>
/// <param name="Id">What SaveData.OpponentId holds while they are in the seat.</param>
/// <param name="Sheet">The content name of their sheet: five poses across, one row.</param>
/// <param name="FrameWidth">Width of one frame, in sheet pixels.</param>
/// <param name="FrameHeight">Height of one frame. The bottom row is the table lip.</param>
/// <param name="Scale">How far a frame is blown up. A whole number so the pixels stay square.</param>
/// <param name="Face">Where the middle of their face is, from the bottom middle of the frame. The aim check uses it.</param>
/// <param name="Script">Their side of the discussion periods.</param>
public sealed record Opponent(
    string Id, string Sheet, int FrameWidth, int FrameHeight, float Scale, Vector2 Face, DialogueScript Script);
