using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CreeperGame;

/// <summary>One of the three save files the player can occupy.</summary>
public class SaveSlot
{
    public int Index { get; }

    /// <summary>False when the slot has never been played.</summary>
    public bool Exists { get; set; }

    /// <summary>Player-visible name of the run.</summary>
    public string Name { get; set; } = "EMPTY";

    /// <summary>Where the player currently is.</summary>
    public string Location { get; set; } = "---";

    /// <summary>Total seconds played, shown as HH:MM.</summary>
    public double PlayTimeSeconds { get; set; }

    public SaveSlot(int index)
    {
        Index = index;
    }

    /// <summary>Line shown under the slot name in the file-select screen.</summary>
    public string Summary => Exists
        ? $"{Location}    {FormatTime(PlayTimeSeconds)}"
        : "CREATE NEW GAME";

    public string Title => Exists ? Name : "EMPTY";

    private static string FormatTime(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)span.TotalHours:00}:{span.Minutes:00}";
    }
}

/// <summary>
/// Loads and stores the three save slots as a tiny key/value text file each.
/// A hand-rolled format keeps the game dependency-free and the files easy to
/// inspect or delete by hand.
/// </summary>
public class SaveSlots
{
    public const int SlotCount = 3;

    private readonly string _directory;
    private readonly SaveSlot[] _slots = new SaveSlot[SlotCount];

    public SaveSlot this[int index] => _slots[index];

    public IEnumerable<SaveSlot> All => _slots;

    public SaveSlots(string assetDir)
    {
        // Saves live beside the assets so the whole game folder stays portable.
        _directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(assetDir)) ?? ".", "saves");

        for (int i = 0; i < SlotCount; i++)
        {
            _slots[i] = new SaveSlot(i);
        }

        Load();
    }

    private void Load()
    {
        try
        {
            Directory.CreateDirectory(_directory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not create the saves folder: {ex.Message}");
            return;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            string path = SlotPath(i);
            if (!File.Exists(path)) continue;

            try
            {
                var values = File.ReadAllLines(path)
                    .Select(line => line.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(),
                        StringComparer.OrdinalIgnoreCase);

                _slots[i].Exists = true;
                _slots[i].Name = values.TryGetValue("name", out string? name) ? name : $"CREEPER {i + 1}";
                _slots[i].Location = values.TryGetValue("location", out string? loc) ? loc : "UNKNOWN";
                _slots[i].PlayTimeSeconds =
                    values.TryGetValue("playtime", out string? time) &&
                    double.TryParse(time, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
                        ? seconds
                        : 0;

                Console.WriteLine($"Save slot {i + 1}: {_slots[i].Name} ({_slots[i].Summary})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save slot {i + 1} could not be read: {ex.Message}");
                _slots[i].Exists = false;
            }
        }
    }

    /// <summary>Writes a slot back to disk. Called when a new run is created.</summary>
    public bool Save(int index)
    {
        SaveSlot slot = _slots[index];

        try
        {
            Directory.CreateDirectory(_directory);

            File.WriteAllLines(SlotPath(index), new[]
            {
                $"name={slot.Name}",
                $"location={slot.Location}",
                $"playtime={slot.PlayTimeSeconds.ToString("0.##", CultureInfo.InvariantCulture)}"
            });

            slot.Exists = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Save slot {index + 1} could not be written: {ex.Message}");
            return false;
        }
    }

    /// <summary>Initialises an empty slot for a brand new run.</summary>
    public void CreateNew(int index)
    {
        SaveSlot slot = _slots[index];
        slot.Name = $"CREEPER {index + 1}";
        slot.Location = "THE BEGINNING";
        slot.PlayTimeSeconds = 0;
        Save(index);
    }

    /// <summary>Deletes a slot, returning it to the EMPTY state.</summary>
    public void Erase(int index)
    {
        try
        {
            string path = SlotPath(index);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Save slot {index + 1} could not be erased: {ex.Message}");
        }

        _slots[index] = new SaveSlot(index);
    }

    private string SlotPath(int index) => Path.Combine(_directory, $"slot{index + 1}.sav");
}
