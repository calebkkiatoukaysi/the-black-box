using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// The whole set of items, and the box's hand when it reaches in to pick one.
/// </summary>
/// <remarks>
/// <para>
/// One table, in one file, because every question anyone asks about an item -- what it is
/// called, what it claims to do, how often it turns up -- is answered by the same row. Adding
/// an item is a line in <see cref="Table"/> and a member on <see cref="ItemId"/>, and nothing
/// else in the game has to be told.
/// </para>
/// <para>
/// Nothing here knows what an item <em>does</em>. See <see cref="ItemDefinition"/> for why.
/// </para>
/// </remarks>
public static class ItemCatalog
{
    /// <summary>
    /// Every item, in the order <see cref="ItemId"/> declares them, grouped as that enum
    /// groups them.
    /// </summary>
    /// <remarks>
    /// The weights happen to total 100, which makes them readable as percentages while the
    /// game is being tuned. Nothing depends on that -- <see cref="Deal"/> divides by whatever
    /// they add up to -- so an item can be added without the rest being corrected.
    /// </remarks>
    private static readonly ItemDefinition[] Table =
    {
        // Blanks.
        new(ItemId.Cinder, "Cinder",
            "Ash, pressed into the shape of a gift. It does nothing.",
            ItemTarget.None, 6),

        new(ItemId.SpentShell, "Spent Shell",
            "A casing, still warm. Whatever it carried, somebody has already had it.",
            ItemTarget.None, 4),

        // Weapons.
        new(ItemId.Revolver, "Revolver",
            "One round, already chambered. Costs them a life.",
            ItemTarget.Opponent, 17),

        new(ItemId.Pact, "Pact",
            "Costs you a life. Costs them one too.",
            ItemTarget.Both, 4),

        new(ItemId.Wager, "Wager",
            "Costs one of you a life. The box chooses which.",
            ItemTarget.Box, 8),

        new(ItemId.HighCard, "High Card",
            "You both cut the deck. The low card costs a life.",
            ItemTarget.Box, 7),

        // Guards.
        new(ItemId.Tourniquet, "Tourniquet",
            "Returns a life you have already lost. Never more than you began with.",
            ItemTarget.Self, 4),

        new(ItemId.AshVeil, "Ash Veil",
            "The next thing used against you does not land.",
            ItemTarget.Self, 6),

        new(ItemId.Mirror, "Mirror",
            "The next thing used against you happens to them instead.",
            ItemTarget.Self, 3),

        // Sight.
        new(ItemId.Lens, "Lens",
            "Shows you what they are holding this round.",
            ItemTarget.Opponent, 7),

        new(ItemId.Tally, "Tally",
            "Shows you how much they have put away, and nothing about what it is.",
            ItemTarget.Opponent, 5),

        new(ItemId.MarkedDeck, "Marked Deck",
            "Shows you what the box deals next. It does not say to whom.",
            ItemTarget.Box, 5),

        new(ItemId.Confession, "Confession",
            "They cannot lie to you for one exchange. They may still decline to speak.",
            ItemTarget.Opponent, 4),

        // Interference.
        new(ItemId.Levy, "Levy",
            "Burns what they are holding this round. They paid a hand for it.",
            ItemTarget.Opponent, 6),

        new(ItemId.SecondHand, "Second Hand",
            "The box takes again, and gives again, without waiting for the round.",
            ItemTarget.Box, 6),

        new(ItemId.WildCard, "Wild Card",
            "Becomes the last thing used at this table. If nothing has been, it is a blank.",
            ItemTarget.Box, 3),

        new(ItemId.Rotgut, "Rotgut",
            "Returns a life. The next hand you are dealt, you will not see until you have played it.",
            ItemTarget.Self, 3),

        new(ItemId.LastCall, "Last Call",
            "Both of you drink, and neither of you sees what the box gives next.",
            ItemTarget.Both, 2),
    };

    /// <summary>Every item, by id, for the lookups the rest of the game does.</summary>
    private static readonly Dictionary<ItemId, ItemDefinition> ById = new();

    /// <summary>What <see cref="Deal"/> divides by. Summed once rather than on every draw.</summary>
    private static readonly int TotalWeight;

    /// <summary>
    /// Indexes the table, and refuses to start if it is incomplete.
    /// </summary>
    /// <remarks>
    /// This is the one place in the project that throws on purpose. <see cref="SaveSystem"/>
    /// swallows everything because the thing going wrong there is a disk, and a player cannot
    /// fix a disk from the title screen. The thing going wrong here is a member added to
    /// <see cref="ItemId"/> without a row to go with it, which is a mistake made while the game
    /// is being written and is far cheaper to hear about on the first frame than to find later
    /// as an item the box deals that has no name.
    /// </remarks>
    static ItemCatalog()
    {
        foreach (ItemDefinition item in Table)
        {
            if (!ById.TryAdd(item.Id, item))
                throw new InvalidOperationException($"{item.Id} appears in the item table twice.");

            if (item.Weight < 0)
                throw new InvalidOperationException($"{item.Id} has a negative weight.");

            TotalWeight += item.Weight;
        }

        foreach (ItemId id in Enum.GetValues<ItemId>())
        {
            if (!ById.ContainsKey(id))
                throw new InvalidOperationException($"{id} is an item with no row in the catalogue.");
        }
    }

    /// <summary>Every item there is, in declaration order.</summary>
    public static IReadOnlyList<ItemDefinition> All => Table;

    /// <summary>
    /// Looks up one item.
    /// </summary>
    /// <param name="id">The item to describe.</param>
    /// <returns>The row for <paramref name="id"/>.</returns>
    public static ItemDefinition Get(ItemId id) => ById[id];

    /// <summary>What an item is called on screen.</summary>
    /// <param name="id">The item to name.</param>
    public static string NameOf(ItemId id) => ById[id].Name;

    /// <summary>
    /// Reads an item back out of a save.
    /// </summary>
    /// <remarks>
    /// The dictionary is what decides, not <see cref="Enum.TryParse{T}(string, bool, out T)"/>
    /// alone: that happily parses <c>"42"</c> into an <see cref="ItemId"/> that has no name and
    /// no row, so a hand-edited or out-of-date save could put an item into play that does not
    /// exist. Anything this cannot place comes back false and the caller drops it, the same way
    /// an unreadable slot is dropped rather than crashing the run.
    /// </remarks>
    /// <param name="text">The id as it was written to disk.</param>
    /// <param name="id">The item, if the text named one.</param>
    /// <returns>True if <paramref name="text"/> named an item this build knows.</returns>
    public static bool TryParse(string text, out ItemId id)
    {
        id = default;
        return !string.IsNullOrWhiteSpace(text)
            && Enum.TryParse(text, ignoreCase: true, out id)
            && ById.ContainsKey(id);
    }

    /// <summary>How an item is written to disk. See <see cref="ItemId"/> for why it is text.</summary>
    /// <param name="id">The item to write.</param>
    public static string ToSaveId(ItemId id) => id.ToString();

    /// <summary>
    /// Reaches into the box and takes one item out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Weighted, so the box can be cruel on purpose -- blanks and revolvers are common, mirrors
    /// and wild cards are not. The caller supplies the <see cref="Random"/> rather than this holding
    /// a static one, so a run can be seeded and replayed while the loop is being tested.
    /// </para>
    /// <para>
    /// <paramref name="favour"/> is the box being kinder to one side of the table than it is
    /// written to be. A hand that came up with a blank or a pact is put back and dealt again
    /// that often -- once, so the bad draw is rarer and not impossible. The opponent is dealt
    /// with no favour at all; see <see cref="RoundRules.PlayerFavour"/> for why the player is.
    /// </para>
    /// </remarks>
    /// <param name="random">The source of the draw.</param>
    /// <param name="favour">How often a cruel draw is taken back, from 0 to 1.</param>
    /// <returns>The item the box gives back for the hand it took.</returns>
    public static ItemId Deal(Random random, float favour = 0f)
    {
        ArgumentNullException.ThrowIfNull(random);

        ItemId item = Draw(random);

        if (favour > 0f && IsCruel(item) && random.NextDouble() < favour) item = Draw(random);

        return item;
    }

    /// <summary>Whether a draw is one the box's favour is allowed to take back.</summary>
    /// <param name="item">The item that came up.</param>
    private static bool IsCruel(ItemId item) => item is ItemId.Cinder or ItemId.SpentShell or ItemId.Pact;

    /// <summary>One weighted draw, with nothing done to it.</summary>
    /// <param name="random">The source of the draw.</param>
    private static ItemId Draw(Random random)
    {
        int roll = random.Next(TotalWeight);

        foreach (ItemDefinition item in Table)
        {
            roll -= item.Weight;
            if (roll < 0) return item.Id;
        }

        // Unreachable: the weights sum to TotalWeight, so the roll always runs out inside the
        // loop. Returning a blank rather than throwing means that if it ever is reached, the
        // box is stingy for one round instead of taking the game down mid-run.
        return ItemId.Cinder;
    }
}
