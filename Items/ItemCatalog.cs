using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// The table of every item, and the weighted draw the box deals from.
/// </summary>
/// <remarks>
/// Adding an item is one row here and one member on ItemId. What an item actually does lives
/// in ItemResolver, not here.
/// </remarks>
public static class ItemCatalog
{
    /// <summary>Every item, in the same order and groups as ItemId.</summary>
    /// <remarks>The weights add up to 100 so I can read them as percentages, but nothing depends on that.</remarks>
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

    /// <summary>Every item, by id.</summary>
    private static readonly Dictionary<ItemId, ItemDefinition> ById = new();

    /// <summary>The sum of the weights, summed once.</summary>
    private static readonly int TotalWeight;

    /// <summary>Indexes the table, and throws if an ItemId has no row.</summary>
    /// <remarks>
    /// This throws on purpose. A missing row is my mistake while writing the game, and I would
    /// rather hear about it on the first frame than find an item with no name mid-run.
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

    /// <summary>Looks up one item.</summary>
    /// <param name="id">The item.</param>
    /// <returns>Its row in the table.</returns>
    public static ItemDefinition Get(ItemId id) => ById[id];

    /// <summary>What an item is called on screen.</summary>
    /// <param name="id">The item to name.</param>
    public static string NameOf(ItemId id) => ById[id].Name;

    /// <summary>Reads an item back out of a save.</summary>
    /// <remarks>
    /// Checked against the dictionary and not just Enum.TryParse, because TryParse will happily
    /// turn "42" into an id that has no row. Anything unknown comes back false and gets dropped.
    /// </remarks>
    /// <param name="text">The id as written to disk.</param>
    /// <param name="id">The item, if the text named one.</param>
    /// <returns>True if this build knows the item.</returns>
    public static bool TryParse(string text, out ItemId id)
    {
        id = default;
        return !string.IsNullOrWhiteSpace(text)
            && Enum.TryParse(text, ignoreCase: true, out id)
            && ById.ContainsKey(id);
    }

    /// <summary>How an item is written to disk: by name, so reordering the enum cannot break a save.</summary>
    /// <param name="id">The item to write.</param>
    public static string ToSaveId(ItemId id) => id.ToString();

    /// <summary>Deals one item, weighted by the table.</summary>
    /// <remarks>
    /// The caller passes the Random so a run can be seeded and replayed. Favour is the box going
    /// easy on the player: a blank or a pact gets redrawn that often, once, so bad draws are
    /// rarer but still possible. See RoundRules.PlayerFavour.
    /// </remarks>
    /// <param name="random">The source of the draw.</param>
    /// <param name="favour">How often a cruel draw is redrawn, 0 to 1.</param>
    /// <returns>The item dealt.</returns>
    public static ItemId Deal(Random random, float favour = 0f)
    {
        ArgumentNullException.ThrowIfNull(random);

        ItemId item = Draw(random);

        if (favour > 0f && IsCruel(item) && random.NextDouble() < favour) item = Draw(random);

        return item;
    }

    /// <summary>Whether a draw is bad enough for favour to redraw it.</summary>
    /// <param name="item">The item that came up.</param>
    private static bool IsCruel(ItemId item) => item is ItemId.Cinder or ItemId.SpentShell or ItemId.Pact;

    /// <summary>One weighted draw.</summary>
    /// <param name="random">The source of the draw.</param>
    private static ItemId Draw(Random random)
    {
        int roll = random.Next(TotalWeight);

        foreach (ItemDefinition item in Table)
        {
            roll -= item.Weight;
            if (roll < 0) return item.Id;
        }

        // Should be unreachable since the roll is under TotalWeight. A blank is safer than a throw if it ever is.
        return ItemId.Cinder;
    }

    /// <summary>Which skill check an item asks for. By kind: weapons aim, guards hold, sight items read.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The check, or None.</returns>
    public static SkillCheck CheckFor(ItemId item) => item switch
    {
        ItemId.Revolver or ItemId.Pact or ItemId.Wager or ItemId.HighCard => SkillCheck.Aim,
        ItemId.Tourniquet or ItemId.AshVeil or ItemId.Mirror => SkillCheck.Steady,
        ItemId.Lens or ItemId.MarkedDeck => SkillCheck.Read,
        _ => SkillCheck.None,
    };
}
