using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheBlackBox;

/// <summary>
/// Everything one save slot remembers about a run.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole save file. It is a plain data class with nothing but properties on it --
/// no sprites, no <c>Game</c>, no textures -- because the moment a save holds a reference to
/// something live it stops being serialisable, and because a save that is only data can be
/// read, diffed and hand-edited while the game is being built.
/// </para>
/// <para>
/// <see cref="Version"/> comes first for a reason. The fields below it are a guess at a game
/// that is not written yet, and they will be wrong. Stamping the shape into every file means
/// a save written this semester can be recognised and upgraded later instead of being thrown
/// away or, worse, read as if it were the new shape. It has been needed twice already --
/// see <see cref="SaveSystem.Upgrade"/>.
/// </para>
/// </remarks>
public class SaveData
{
    /// <summary>Which shape of save file this is. See <see cref="SaveSystem.CurrentVersion"/>.</summary>
    public int Version { get; set; } = SaveSystem.CurrentVersion;

    /// <summary>When the slot was first started, in UTC.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>When the slot was last written, in UTC. This is the date the form shows.</summary>
    public DateTime SavedUtc { get; set; }

    /// <summary>
    /// How long the player has spent in this run, in seconds.
    /// </summary>
    /// <remarks>
    /// Stored as a number rather than a <see cref="TimeSpan"/>: a TimeSpan lands in the file as
    /// <c>"01:02:03"</c>, which is a format to get wrong every time the file is touched by hand.
    /// <see cref="Playtime"/> is the property the game actually uses.
    /// </remarks>
    public double PlaytimeSeconds { get; set; }

    /// <summary>
    /// What the player called themselves.
    /// </summary>
    /// <remarks>
    /// Asked for once, when a slot is first started, and substituted into every written line
    /// that contains <c>{name}</c>. It is on the save rather than on the game because two
    /// slots are two different people.
    /// </remarks>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>How far through the story the run is.</summary>
    public int Chapter { get; set; } = 1;

    /// <summary>Which opponent is across the table, or empty between chapters.</summary>
    public string OpponentId { get; set; } = string.Empty;

    /// <summary>Which round of the current exchange the run is on.</summary>
    public int Round { get; set; }

    /// <summary>What the player has left to lose.</summary>
    public int PlayerLives { get; set; } = StartingLives;

    /// <summary>What the opponent has left to lose.</summary>
    public int OpponentLives { get; set; } = StartingLives;

    /// <summary>How many times the box has been fed a hand in this run.</summary>
    public int HandsFed { get; set; }

    /// <summary>
    /// The item the box has just dealt the player and they have not decided on, by id, or
    /// empty between deals.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Banked"/> because the rules make them separate things: an
    /// item that has been dealt is a decision waiting to be taken, and using it or putting it
    /// away is that decision. Version 1 had only the one list and could not express the
    /// difference.
    /// </remarks>
    public string Dealt { get; set; } = string.Empty;

    /// <summary>
    /// Whether the player was dealt this round without being allowed to look.
    /// </summary>
    /// <remarks>
    /// <see cref="ItemId.Rotgut"/> buys a life with sight, and this is where that debt is
    /// collected: the item in <see cref="Dealt"/> is real and will resolve normally, and the
    /// player simply is not shown which one it is until it has been played. The debt itself
    /// is <see cref="NextDealBlind"/>, and it becomes this on the next deal.
    /// </remarks>
    public bool DealtBlind { get; set; }

    /// <summary>
    /// Whether the player's next deal is to be made blind.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="DealtBlind"/> because a rotgut can be drunk out of a pocket
    /// in the middle of a turn, with an item already in the hand that the player has looked
    /// at. Blinding that one would be blinding a hand they have seen; the debt is on the
    /// next one, which is what the bottle says.
    /// </remarks>
    public bool NextDealBlind { get; set; }

    /// <summary>
    /// The player's pockets: items put away for a later turn, by id, in pocket order.
    /// </summary>
    /// <remarks>
    /// Never more than <see cref="RoundRules.PocketSlots"/> of them. Version 2 had no limit
    /// and called this the bank; the pocket is the same list with a size, and
    /// <see cref="SaveSystem.Upgrade"/> is where an older save's overflow goes.
    /// </remarks>
    public List<string> Banked { get; set; } = new();

    /// <summary>The item across the table, undecided. The player is not shown this.</summary>
    public string OpponentDealt { get; set; } = string.Empty;

    /// <summary>Whether the opponent is playing this round blind. See <see cref="DealtBlind"/>.</summary>
    public bool OpponentDealtBlind { get; set; }

    /// <summary>Whether the opponent's next deal is to be made blind. See <see cref="NextDealBlind"/>.</summary>
    public bool OpponentNextDealBlind { get; set; }

    /// <summary>The opponent's pockets. How many are full is on the table; <see cref="ItemId.Tally"/> shows what is in them.</summary>
    public List<string> OpponentBanked { get; set; } = new();

    /// <summary>How warmly the opponent is speaking. See <see cref="Disposition"/>.</summary>
    public int OpponentWarmth { get; set; }

    /// <summary>How little the opponent is giving away. See <see cref="Disposition"/>.</summary>
    /// <remarks>
    /// Defaults to the neutral 50 rather than to zero, because zero is not neutral here -- it
    /// is perfectly candid, and a save arriving without this field would otherwise open every
    /// opponent's mouth for free.
    /// </remarks>
    public int OpponentGuard { get; set; } = Disposition.Neutral.Guard;

    /// <summary>
    /// What is standing between the player and the next thing used on them, by id, or empty.
    /// </summary>
    /// <remarks>
    /// <see cref="ItemId.AshVeil"/> and <see cref="ItemId.Mirror"/> are both played before
    /// they are needed, so what they do is sit here until something arrives. Only one can be
    /// up at a time -- playing a second replaces the first, which is a real cost and is why
    /// holding a guard is not simply free value.
    /// </remarks>
    public string PlayerWard { get; set; } = string.Empty;

    /// <summary>What is standing in front of the opponent. See <see cref="PlayerWard"/>.</summary>
    public string OpponentWard { get; set; } = string.Empty;

    /// <summary>The last item used by anyone at this table, which is what a wild card copies.</summary>
    public string LastUsed { get; set; } = string.Empty;

    /// <summary>Whether the player has bought a look at what the opponent is holding.</summary>
    /// <remarks>Cleared when the round ends, because it was a look at this round's item.</remarks>
    public bool SeesOpponentItem { get; set; }

    /// <summary>Whether the player has bought a look inside the opponent's pockets.</summary>
    /// <remarks>Cleared when the round ends, like <see cref="SeesOpponentItem"/>. What they carry can change.</remarks>
    public bool SeesOpponentPockets { get; set; }

    /// <summary>What the box has already decided to deal next, by id, or empty if it has not.</summary>
    /// <remarks>
    /// <see cref="ItemId.MarkedDeck"/> shows the player the next deal, which means the next
    /// deal has to exist before it happens. Rolled here and then spent by the next hand, so
    /// what the marked deck promised is what actually arrives.
    /// </remarks>
    public string NextDeal { get; set; } = string.Empty;

    /// <summary>
    /// Everything that has happened and has to be remembered, by id: dialogue taken, lore
    /// found, endings seen.
    /// </summary>
    /// <remarks>
    /// A flat set of string ids rather than a field per event. The game is going to sprout
    /// far more of these than anyone wants to add properties for, and adding one to this list
    /// does not change the shape of the file, so it costs no version bump.
    /// </remarks>
    public List<string> Flags { get; set; } = new();

    /// <summary>
    /// Fields in the file that this build has no property for.
    /// </summary>
    /// <remarks>
    /// This is how a removed field survives long enough to be migrated. Without it,
    /// <c>System.Text.Json</c> silently drops anything it does not recognise, so version 1's
    /// <c>Hand</c> would be gone before <see cref="SaveSystem.Upgrade"/> ever saw the object
    /// and the player's items would quietly vanish. Upgrade reads what it needs from here and
    /// removes it; anything left is written back out untouched, so a save passed through an
    /// older build does not lose the fields that build did not know about.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; }

    /// <summary>Lives each side starts a run with.</summary>
    public const int StartingLives = 3;

    /// <summary><see cref="PlaytimeSeconds"/>, in the form the rest of the game wants it.</summary>
    [JsonIgnore]
    public TimeSpan Playtime
    {
        get => TimeSpan.FromSeconds(PlaytimeSeconds);
        set => PlaytimeSeconds = value.TotalSeconds;
    }

    /// <summary>
    /// What the opponent currently thinks of the player, as the discussion sees it.
    /// </summary>
    /// <remarks>
    /// Two plain integers on disk and one value in code. A <see cref="Disposition"/> written
    /// straight to the file would be a nested object for two numbers that want to be legible
    /// and editable while the game is being balanced.
    /// </remarks>
    [JsonIgnore]
    public Disposition OpponentDisposition
    {
        get => new(OpponentWarmth, OpponentGuard);
        set
        {
            OpponentWarmth = value.Value;
            OpponentGuard = value.Guard;
        }
    }

    /// <summary>
    /// Builds the save a brand new run starts from.
    /// </summary>
    /// <returns>A <see cref="SaveData"/> stamped with the current time.</returns>
    public static SaveData NewRun()
    {
        DateTime now = DateTime.UtcNow;
        return new SaveData { CreatedUtc = now, SavedUtc = now };
    }

    /// <summary>Whether <paramref name="id"/> has been recorded in <see cref="Flags"/>.</summary>
    /// <param name="id">The flag to look for.</param>
    public bool HasFlag(string id) => Flags.Contains(id);

    /// <summary>Records <paramref name="id"/> in <see cref="Flags"/> if it is not there already.</summary>
    /// <param name="id">The flag to set.</param>
    public void SetFlag(string id)
    {
        if (!Flags.Contains(id)) Flags.Add(id);
    }

    /// <summary>Whether there is no room left in the player's pockets.</summary>
    [JsonIgnore]
    public bool PocketsFull => Banked.Count >= RoundRules.PocketSlots;

    /// <summary>Whether there is no room left in the opponent's.</summary>
    [JsonIgnore]
    public bool OpponentPocketsFull => OpponentBanked.Count >= RoundRules.PocketSlots;

    /// <summary>Whether the player has sat down with <see cref="OpponentId"/> before.</summary>
    /// <remarks>
    /// What decides whether a discussion opens on the script's own disposition or on the one
    /// this save is carrying. It is a flag rather than a field because it is exactly the kind
    /// of one-bit fact <see cref="Flags"/> exists to absorb without a version bump.
    /// </remarks>
    [JsonIgnore]
    public bool HasMetOpponent => HasFlag(MetFlag(OpponentId));

    /// <summary>Records that the player has now sat down with <paramref name="opponentId"/>.</summary>
    /// <param name="opponentId">The opponent who has now been met.</param>
    public void RecordMeeting(string opponentId) => SetFlag(MetFlag(opponentId));

    /// <summary>The <see cref="Flags"/> id recording that an opponent has been met.</summary>
    /// <param name="opponentId">The opponent in question.</param>
    public static string MetFlag(string opponentId) => "met." + opponentId;
}
