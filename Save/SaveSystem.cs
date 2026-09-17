using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TheBlackBox;

/// <summary>
/// Reads, writes and erases the three save slots.
/// </summary>
/// <remarks>
/// <para>
/// One slot is one JSON file in the player's local application data, written with
/// <c>System.Text.Json</c> -- which ships with .NET, so the project still builds on a clean
/// machine with nothing installed but the SDK and MonoGame.
/// </para>
/// <para>
/// Saves deliberately do not live next to the executable. <c>bin/</c> is deleted by every
/// clean build and is absent from every fresh clone of the repository, so a save written
/// there is a save the player loses to <c>dotnet clean</c>. <see cref="Folder"/> survives all
/// of that, and is where Windows expects a game to keep this.
/// </para>
/// <para>
/// Nothing here throws. A save system that throws turns a bad sector into a crash on the
/// title screen, so every operation catches, records what went wrong, and reports failure --
/// the form shows the slot as unreadable and the player can still erase it and carry on.
/// </para>
/// </remarks>
public static class SaveSystem
{
    /// <summary>How many slots the game offers.</summary>
    public const int SlotCount = 3;

    /// <summary>
    /// The shape of save file this build writes. Bump it whenever an existing field changes
    /// meaning or leaves, and teach <see cref="Upgrade"/> how to bring the old shape forward.
    /// Adding a new field with a sensible default does not need a bump -- an older file simply
    /// arrives without it and gets the default.
    /// </summary>
    public const int CurrentVersion = 3;

    /// <summary>Written to disk indented, so a save can be read and edited while the game is built.</summary>
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Why the last read, write or erase failed, or null if it did not.</summary>
    public static string LastError { get; private set; }

    /// <summary>Where the slots live.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheBlackBox", "Saves");

    /// <summary>
    /// Reads every slot, in order.
    /// </summary>
    /// <returns>One <see cref="SaveSlot"/> per slot, empty and unreadable ones included.</returns>
    public static SaveSlot[] ReadAll()
    {
        var slots = new SaveSlot[SlotCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = Read(i);
        return slots;
    }

    /// <summary>
    /// Reads one slot.
    /// </summary>
    /// <param name="slot">Which slot, from 0 to <see cref="SlotCount"/> - 1.</param>
    /// <returns>What the slot turned out to be. Never null, and never throws.</returns>
    public static SaveSlot Read(int slot)
    {
        string path = PathFor(slot);

        try
        {
            if (!File.Exists(path)) return new SaveSlot { Index = slot, State = SaveSlotState.Empty };

            SaveData data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Options);

            // Deserialize returns null for a file holding the literal "null", which is valid
            // JSON and not a valid save, so it is caught here rather than downstream.
            if (data is null) return Unreadable(slot, "the file is empty");

            return new SaveSlot { Index = slot, State = SaveSlotState.Occupied, Data = Upgrade(data) };
        }
        catch (JsonException e)
        {
            return Unreadable(slot, "the file is not readable as a save: " + e.Message);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unreadable(slot, e.Message);
        }
    }

    /// <summary>
    /// Writes one slot, stamping it with the current time and version on the way out.
    /// </summary>
    /// <remarks>
    /// The write goes to a temporary file first and only then replaces the real one, because
    /// a save is overwritten at exactly the moments a game is most likely to be killed --
    /// quitting, or dying. Truncating the old save and then failing to finish the new one
    /// would lose the run; this way the slot holds either the old save or the new one and
    /// never half of either. <see cref="File.Replace(string, string, string)"/> also keeps the
    /// save it displaced as a <c>.bak</c>, which is one more copy standing between the player
    /// and a lost run.
    /// </remarks>
    /// <param name="slot">Which slot, from 0 to <see cref="SlotCount"/> - 1.</param>
    /// <param name="data">The run to write.</param>
    /// <returns>True if the slot now holds this run.</returns>
    public static bool Write(int slot, SaveData data)
    {
        if (data is null) return Fail("there is nothing to save");

        string path = PathFor(slot);
        string temporary = path + ".tmp";

        data.Version = CurrentVersion;
        data.SavedUtc = DateTime.UtcNow;

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, Options));

            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);

            LastError = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A half-written temporary is not a save, and must not be left lying around
            // looking like one.
            TryDelete(temporary);
            return Fail(e.Message);
        }
    }

    /// <summary>
    /// Erases one slot, and the backup behind it.
    /// </summary>
    /// <param name="slot">Which slot, from 0 to <see cref="SlotCount"/> - 1.</param>
    /// <returns>True if the slot is now empty, which includes it having been empty already.</returns>
    public static bool Delete(int slot)
    {
        string path = PathFor(slot);

        try
        {
            TryDelete(path + ".tmp");
            TryDelete(path + ".bak");
            TryDelete(path);

            LastError = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fail(e.Message);
        }
    }

    /// <summary>
    /// Brings a save written by an older build up to <see cref="CurrentVersion"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each step moves a file forward exactly one version and falls through, so a version 1
    /// file is carried all the way up by running every step in turn rather than by one
    /// migration per pair of versions.
    /// </para>
    /// <para>
    /// Version 1 to 2: version 1 kept a single <c>Hand</c> list, which was the player's whole
    /// inventory. The rules since then separate the item that has just been dealt and is still
    /// a decision from the items that were abstained with and kept, so <c>Hand</c> becomes
    /// <see cref="SaveData.Banked"/> -- everything in an old save had already been dealt and
    /// not used, which is precisely what banked now means. It is read out of
    /// <see cref="SaveData.Extra"/> because the property it used to land in no longer exists.
    /// </para>
    /// <para>
    /// Version 2 to 3: the bank became a pocket, and a pocket has <see cref="RoundRules.PocketSlots"/>
    /// slots. A version 2 save could hold any number of items, so anything past the last
    /// slot is dropped -- the oldest are kept, because they are the ones the player chose
    /// first, and nothing else about the file changes.
    /// </para>
    /// </remarks>
    /// <param name="data">The save as it was read off disk.</param>
    /// <returns>The same run, in the shape this build expects.</returns>
    private static SaveData Upgrade(SaveData data)
    {
        if (data.Version < 2)
        {
            foreach (string id in TakeLegacyList(data, "Hand"))
            {
                if (ItemCatalog.TryParse(id, out ItemId item))
                    data.Banked.Add(ItemCatalog.ToSaveId(item));
            }

            // Version 1 predates the opponent remembering anything, so it opens neutral.
            data.OpponentDisposition = Disposition.Neutral;
        }

        if (data.Version < 3)
        {
            TrimToPockets(data.Banked);
            TrimToPockets(data.OpponentBanked);
        }

        data.Version = CurrentVersion;
        return data;
    }

    /// <summary>Drops whatever would not fit in a pocket, keeping what was put there first.</summary>
    /// <param name="pockets">The list to trim in place.</param>
    private static void TrimToPockets(List<string> pockets)
    {
        if (pockets.Count > RoundRules.PocketSlots)
            pockets.RemoveRange(RoundRules.PocketSlots, pockets.Count - RoundRules.PocketSlots);
    }

    /// <summary>
    /// Reads a list of strings out of a field this build no longer has a property for, and
    /// takes it out of the save so it is not written back.
    /// </summary>
    /// <remarks>
    /// Anything that is not a list of strings comes back empty rather than throwing. A save
    /// being migrated is a save that has already survived being read, and refusing it at this
    /// point over one malformed field would turn a recoverable run into an unreadable slot.
    /// </remarks>
    /// <param name="data">The save being brought forward.</param>
    /// <param name="name">The field to drain.</param>
    /// <returns>What the field held, or nothing.</returns>
    private static List<string> TakeLegacyList(SaveData data, string name)
    {
        var found = new List<string>();

        if (data.Extra is null || !data.Extra.Remove(name, out JsonElement value)) return found;
        if (value.ValueKind != JsonValueKind.Array) return found;

        foreach (JsonElement entry in value.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String) found.Add(entry.GetString());
        }

        return found;
    }

    /// <summary>The file one slot lives in.</summary>
    /// <param name="slot">Which slot, from 0 to <see cref="SlotCount"/> - 1.</param>
    private static string PathFor(int slot)
    {
        if (slot < 0 || slot >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "There are only " + SlotCount + " slots.");

        return Path.Combine(Folder, "slot" + slot + ".json");
    }

    /// <summary>Deletes a file if it is there, and says nothing if it is not.</summary>
    /// <param name="path">The file to remove.</param>
    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Records why an operation failed, and reports the failure to the caller.</summary>
    /// <param name="reason">What went wrong.</param>
    private static bool Fail(string reason)
    {
        LastError = reason;
        return false;
    }

    /// <summary>Records why a slot could not be read, and describes it as unreadable.</summary>
    /// <param name="slot">Which slot could not be read.</param>
    /// <param name="reason">What went wrong.</param>
    private static SaveSlot Unreadable(int slot, string reason)
    {
        LastError = reason;
        return new SaveSlot { Index = slot, State = SaveSlotState.Unreadable, Error = reason };
    }
}
