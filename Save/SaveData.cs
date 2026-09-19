using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheBlackBox;

/// <summary>
/// Everything one save slot remembers about a run.
/// </summary>
/// <remarks>
/// Plain data only, no sprites or textures, so it serialises and I can hand-edit a save while
/// testing. Version comes first because the fields keep changing and SaveSystem.Upgrade has
/// already had to fix old files twice.
/// </remarks>
public class SaveData
{
    /// <summary>Which shape of save file this is. See <see cref="SaveSystem.CurrentVersion"/>.</summary>
    public int Version { get; set; } = SaveSystem.CurrentVersion;

    /// <summary>When the slot was first started, in UTC.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>When the slot was last written, in UTC. This is the date the form shows.</summary>
    public DateTime SavedUtc { get; set; }

    /// <summary>How long the player has spent in this run, in seconds.</summary>
    /// <remarks>A number, not a TimeSpan, so the file stays easy to edit by hand. The game uses <see cref="Playtime"/>.</remarks>
    public double PlaytimeSeconds { get; set; }

    /// <summary>What the player called themselves. Swapped into any line with {name} in it.</summary>
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

    /// <summary>The item just dealt to the player and not decided on yet, by id, or empty.</summary>
    /// <remarks>Kept apart from <see cref="Banked"/> because a dealt item is still a decision. Version 1 had one list and could not tell them apart.</remarks>
    public string Dealt { get; set; } = string.Empty;

    /// <summary>Whether the player was dealt this round without being allowed to look.</summary>
    /// <remarks>This is the rotgut's price being paid. The item in Dealt is real, the player just cannot see it.</remarks>
    public bool DealtBlind { get; set; }

    /// <summary>Whether the player's next deal is to be made blind.</summary>
    /// <remarks>Separate from DealtBlind because a rotgut can be drunk mid-turn with an item already seen. The debt lands on the next deal.</remarks>
    public bool NextDealBlind { get; set; }

    /// <summary>The player's pockets: items put away for later, by id, in pocket order.</summary>
    /// <remarks>Never more than <see cref="RoundRules.PocketSlots"/>. Version 2 had no limit and called this the bank.</remarks>
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
    /// <remarks>Defaults to the neutral 50, not zero. Zero here means fully candid, and an old save without this field would get that for free.</remarks>
    public int OpponentGuard { get; set; } = Disposition.Neutral.Guard;

    /// <summary>The guard in front of the player, by id, or empty. Ash veil and mirror sit here until something hits it.</summary>
    /// <remarks>Only one at a time. Playing a second replaces the first, so a guard is not free value.</remarks>
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

    /// <summary>What the box has already decided to deal next, by id, or empty.</summary>
    /// <remarks>The marked deck shows the next deal, so the next deal has to be rolled ahead of time and kept here.</remarks>
    public string NextDeal { get; set; } = string.Empty;

    /// <summary>Everything that has happened and has to be remembered, by id: dialogue taken, lore found, endings seen.</summary>
    /// <remarks>A flat list of strings instead of a property per event, so adding one never needs a version bump.</remarks>
    public List<string> Flags { get; set; } = new();

    /// <summary>Fields in the file that this build has no property for.</summary>
    /// <remarks>
    /// Without this System.Text.Json drops unknown fields on read, so a removed field like
    /// version 1's Hand would be gone before Upgrade could migrate it. Whatever Upgrade does
    /// not take gets written back out as it was.
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

    /// <summary>What the opponent currently thinks of the player, as the discussion sees it.</summary>
    /// <remarks>Two plain ints on disk, one Disposition in code. Easier to read and tweak in the file that way.</remarks>
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

    /// <summary>Builds the save a brand new run starts from.</summary>
    /// <returns>A fresh save stamped with the current time.</returns>
    public static SaveData NewRun()
    {
        DateTime now = DateTime.UtcNow;
        return new SaveData { CreatedUtc = now, SavedUtc = now };
    }

    /// <summary>Clears the table for another go: lives, pockets, hands, wards, the round count.</summary>
    /// <remarks>The name, chapter, opponent, disposition, flags and clock all stay. Same table, same person, and they remember.</remarks>
    public void Restart()
    {
        Round = 0;
        PlayerLives = StartingLives;
        OpponentLives = StartingLives;
        HandsFed = 0;
        Dealt = string.Empty;
        DealtBlind = false;
        NextDealBlind = false;
        OpponentDealt = string.Empty;
        OpponentDealtBlind = false;
        OpponentNextDealBlind = false;
        Banked.Clear();
        OpponentBanked.Clear();
        PlayerWard = string.Empty;
        OpponentWard = string.Empty;
        LastUsed = string.Empty;
        SeesOpponentItem = false;
        SeesOpponentPockets = false;
        NextDeal = string.Empty;
    }

    /// <summary>The player got up from the table. Next chapter, cleared table, empty seat.</summary>
    /// <remarks>
    /// The seat is emptied here and not in Restart, because losing means sitting back down
    /// with the same person. <see cref="Opponents.ForChapter"/> fills it next time the run is
    /// entered. The old disposition is left over but harmless: a new opponent has not been met, so
    /// their script writes over it.
    /// </remarks>
    public void Advance()
    {
        Chapter++;
        Restart();
        OpponentId = string.Empty;
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
    /// <remarks>Decides whether a discussion opens on the script's disposition or the saved one. A flag, so no version bump.</remarks>
    [JsonIgnore]
    public bool HasMetOpponent => HasFlag(MetFlag(OpponentId));

    /// <summary>Records that the player has now sat down with <paramref name="opponentId"/>.</summary>
    /// <param name="opponentId">The opponent who has now been met.</param>
    public void RecordMeeting(string opponentId) => SetFlag(MetFlag(opponentId));

    /// <summary>The <see cref="Flags"/> id recording that an opponent has been met.</summary>
    /// <param name="opponentId">The opponent in question.</param>
    public static string MetFlag(string opponentId) => "met." + opponentId;
}
