using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mecho;

/// <summary>Всичко, което мечокът помни между пусканията.</summary>
public sealed class SaveData
{
    public string OwnerName { get; set; } = "Тут";
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public bool SleepingByChoice { get; set; }
    public bool StayPut { get; set; }
    public double CouchX { get; set; } = double.NaN;
    public int CouchEdge { get; set; } = 1;
    public DateTime QuietUntil { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public bool FirstRunDone { get; set; }
    public DateTime LastSeen { get; set; }

    // Грижа (0–100)
    public double Fullness { get; set; } = 80;
    public double Energy { get; set; } = 90;

    // Прогрес
    public int Hazelnuts { get; set; }
    public int TasksDoneTotal { get; set; }
    public int PomodorosTotal { get; set; }
    public string PomodoroDay { get; set; } = "";
    public int PomodorosToday { get; set; }

    // Мед-доро, което тече в момента (за да продължи след рестарт)
    public PomodoroPhase PomodoroPhase { get; set; }
    public DateTime PomodoroEndsAt { get; set; }

    public List<TaskItem> Tasks { get; set; } = new();

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mecho");

    private static string FilePath => Path.Combine(Folder, "save.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // X и CouchX са NaN, докато не се знае къде са.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    public static SaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath), Options) ?? new SaveData();
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

public enum PomodoroPhase { Off, Focus, Break }

/// <summary>Задача от списъка на Тут.</summary>
public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";

    /// <summary>1 = малка, 2 = средна, 3 = голяма.</summary>
    public int Size { get; set; } = 1;

    public bool Done { get; set; }
    public DateTime? DoneAt { get; set; }

    public int Reward => Size switch { 3 => 5, 2 => 3, _ => 1 };
}
