using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TheBlackBox;

/// <summary>
/// The roster, in chapter order. A chapter past the end gets the last one so a run never falls off the table.
/// </summary>
/// <remarks>
/// Serenity is the only one for now, so every chapter seats her. Looked up by id too, because the
/// save stores the id, and an unknown id (an older save's stand-in opponent, say) falls back to
/// the first rather than throwing.
/// </remarks>
public static class Opponents
{
    /// <summary>Serenity: her picture, sampled to art pixels at 4x, and her own script.</summary>
    public static readonly Opponent First = new(
        SerenityDiscussion.Script.OpponentId, "opponent-second-sheet", 140, 120, 4f, new Vector2(4f, -265f), SerenityDiscussion.Script);

    /// <summary>Everyone, in chapter order.</summary>
    public static readonly IReadOnlyList<Opponent> Roster = new[] { First };

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
