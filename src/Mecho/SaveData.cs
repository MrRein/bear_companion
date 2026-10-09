using System;
using System.IO;
using System.Text.Json;

namespace Mecho;

/// <summary>Всичко, което мечокът помни между пусканията.</summary>
public sealed class SaveData
{
    public string OwnerName { get; set; } = "Тут";
    public double X { get; set; } = double.NaN;
    public bool SleepingByChoice { get; set; }
    public DateTime QuietUntil { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public bool FirstRunDone { get; set; }
    public DateTime LastSeen { get; set; }

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mecho");

    private static string FilePath => Path.Combine(Folder, "save.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static SaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
        }
        catch (Exception)
        {
            // Повреден файл: започваме наново, но пазим стария за всеки случай.
            try { File.Copy(FilePath, FilePath + ".bad", true); } catch (Exception) { }
        }
        return new SaveData();
    }

    public void Save()
    {
        try
        {
            LastSeen = DateTime.Now;
            Directory.CreateDirectory(Folder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception)
        {
            // Записът не е толкова важен, че да спре мечока.
        }
    }
}
