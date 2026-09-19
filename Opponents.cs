using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TheBlackBox;

/// <summary>
/// One opponent: the id on the save, their sheet and how to draw it, and their script.
/// </summary>
/// <remarks>
/// Frame size and scale are per opponent because the two are not made the same way. The first
/// is art pixels blown up 6x, the second is her picture at 1x. Anything that changes during a
/// run (disposition, lives) lives on SaveData, not here.
/// </remarks>
/// <param name="Id">What SaveData.OpponentId holds while they are in the seat.</param>
/// <param name="Sheet">The content name of their sheet: six poses across, one row.</param>
/// <param name="FrameWidth">Width of one frame, in sheet pixels.</param>
/// <param name="FrameHeight">Height of one frame. The bottom row is the table lip.</param>
/// <param name="Scale">How far a frame is blown up. A whole number so the pixels stay square.</param>
/// <param name="Face">Where the middle of their face is, from the bottom middle of the frame. The aim check uses it.</param>
/// <param name="Script">Their side of the discussion periods.</param>
public sealed record Opponent(
    string Id, string Sheet, int FrameWidth, int FrameHeight, float Scale, Vector2 Face, DialogueScript Script);

/// <summary>
/// The roster, in chapter order. A chapter past the end gets the last one so a run never falls off the table.
/// </summary>
/// <remarks>
/// Both opponents use the stand-in script until they have their own. Looked up by id too,
/// because the save stores the id, and an unknown id falls back to the first rather than throwing.
/// </remarks>
public static class Opponents
{
    /// <summary>The default: her picture, sampled to art pixels at 4x. Her id is the stand-in script's, since the first saves were written with it.</summary>
    public static readonly Opponent First = new(
        DemoDiscussion.Script.OpponentId, "opponent-second-sheet", 140, 120, 4f, new Vector2(4f, -265f), DemoDiscussion.Script);

    /// <summary>The one from the character sheet, who sits down in chapter two.</summary>
    public static readonly Opponent Second = new(
        "second", "opponent-sheet", 107, 100, 6f, new Vector2(-27f, -310f), DemoDiscussion.Script);

    /// <summary>Everyone, in chapter order.</summary>
    public static readonly IReadOnlyList<Opponent> Roster = new[] { First, Second };

    /// <summary>Who sits down for a chapter, counted from one.</summary>
    /// <param name="chapter">The chapter the run is on.</param>
    public static Opponent ForChapter(int chapter) => Roster[Math.Clamp(chapter - 1, 0, Roster.Count - 1)];

    /// <summary>Who a save's OpponentId names, or First if nobody does.</summary>
    /// <param name="id">The id on the save.</param>
    public static Opponent ById(string id)
    {
        foreach (Opponent opponent in Roster)
            if (opponent.Id == id) return opponent;
        return First;
    }
}
