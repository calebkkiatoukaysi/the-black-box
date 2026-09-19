using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TheBlackBox;

/// <summary>
/// Reads, writes and erases the three save slots.
/// </summary>
/// <remarks>
/// One slot is one JSON file in local app data, written with System.Text.Json since that ships
/// with .NET. Not next to the exe, because bin/ gets wiped by a clean build. Nothing in here
/// throws: a bad file shows up as an unreadable slot the player can erase, not a crash on the
/// title screen.
/// </remarks>
public static class SaveSystem
{
    /// <summary>How many slots the game offers.</summary>
    public const int SlotCount = 3;

    /// <summary>The shape of save file this build writes.</summary>
    /// <remarks>Bump it when an existing field changes meaning or goes away, and add a step to <see cref="Upgrade"/>. A new field with a default does not need a bump.</remarks>
    public const int CurrentVersion = 3;

    /// <summary>Written to disk indented, so a save can be read and edited while the game is built.</summary>
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Why the last read, write or erase failed, or null if it did not.</summary>
    public static string LastError { get; private set; }

    /// <summary>Where the slots live.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheBlackBox", "Saves");

    /// <summary>Reads every slot, in order.</summary>
    /// <returns>One SaveSlot per slot, empty and unreadable ones included.</returns>
    public static SaveSlot[] ReadAll()
    {
        var slots = new SaveSlot[SlotCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = Read(i);
        return slots;
    }

    /// <summary>Reads one slot.</summary>
    /// <param name="slot">Which slot, from 0 to SlotCount - 1.</param>
    /// <returns>What the slot turned out to be. Never null, never throws.</returns>
    public static SaveSlot Read(int slot)
    {
        string path = PathFor(slot);

        try
        {
            if (!File.Exists(path)) return new SaveSlot { Index = slot, State = SaveSlotState.Empty };

            SaveData data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Options);

            // A file holding just "null" is valid JSON and deserialises to null. Catch it here.
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

    /// <summary>Writes one slot, stamping it with the current time and version on the way out.</summary>
    /// <remarks>
    /// Written to a .tmp first and then swapped in, so if the game dies mid-write the slot
    /// still has the old save rather than half a new one. File.Replace keeps the old one as
    /// a .bak too.
    /// </remarks>
    /// <param name="slot">Which slot, from 0 to SlotCount - 1.</param>
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
            // Don't leave a half-written temp file lying around looking like a save.
            TryDelete(temporary);
            return Fail(e.Message);
        }
    }

    /// <summary>Erases one slot, and the backup behind it.</summary>
    /// <param name="slot">Which slot, from 0 to SlotCount - 1.</param>
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

    /// <summary>Brings a save written by an older build up to <see cref="CurrentVersion"/>.</summary>
    /// <remarks>
    /// Each step moves the file up one version and falls through to the next. 1 to 2: the old
    /// Hand list becomes Banked, read out of Extra since the property is gone. 2 to 3: the bank
    /// became a pocket with a size limit, so anything past the last slot is dropped, oldest kept.
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

            // Version 1 had no disposition, so it opens neutral.
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

    /// <summary>Pulls a list of strings out of a field this build no longer has, and removes it so it is not written back.</summary>
    /// <remarks>Anything malformed comes back empty instead of throwing. One bad field should not make the whole slot unreadable.</remarks>
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
    /// <param name="slot">Which slot, from 0 to SlotCount - 1.</param>
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
