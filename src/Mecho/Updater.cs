using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mecho;

/// <summary>
/// Проверява в GitHub Releases за нова версия, сваля Mecho.zip и сменя файловете.
/// Всяка версия носи files.txt – списък на своите файлове. След копирането се трият
/// файловете, които са били в старата версия, а в новата ги няма (стари рисунки, папки…);
/// ако старата версия няма такъв списък – само ненужното в assets. Записът е в
/// %AppData%\Mecho и никога не се пипа.
/// Версия N е GitHub release с таг "vN"; текущата е третото число от версията на програмата.
/// </summary>
public sealed class Updater
{
    private const string LatestUrl = "https://api.github.com/repos/MrRein/bear_companion/releases/latest";

    public enum Result { UpToDate, Available, Failed }

    private static readonly HttpClient Http = CreateClient();

    public static int CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.Build ?? 0;

    /// <summary>Версията за хората: „1.0.3“ (вътрешно номерът е този на сглобяването – виж build.yml).</summary>
    public static string CurrentName
    {
        get
        {
            var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(info)) return CurrentVersion.ToString();
            int plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
    }

    public int LatestVersion { get; private set; }

    /// <summary>Новата версия за хората (от заглавието „Версия 1.0.3“).</summary>
    public string LatestName { get; private set; } = "";
    public bool IsAvailable => LatestVersion > CurrentVersion && _zipUrl != null;
    public bool IsBusy { get; private set; }

    private string? _zipUrl, _hashUrl;

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mecho-updater");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    public async Task<Result> CheckAsync()
    {
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(LatestUrl));
            var root = doc.RootElement;
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!int.TryParse(tag.TrimStart('v', 'V'), out int latest)) return Result.Failed;

            string? Asset(string name) => root.GetProperty("assets").EnumerateArray()
                .Where(a => a.GetProperty("name").GetString() == name)
                .Select(a => a.GetProperty("browser_download_url").GetString())
                .FirstOrDefault();
            _zipUrl = Asset("Mecho.zip");
            _hashUrl = Asset("Mecho.zip.sha256");
            LatestVersion = latest;
            string title = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            LatestName = title.StartsWith("Версия ") ? title["Версия ".Length..].Trim() : latest.ToString();
            return IsAvailable ? Result.Available : Result.UpToDate;
        }
        catch (Exception)
        {
            return Result.Failed;
        }
    }

    /// <summary>
    /// Сваля и разархивира новата версия, после пуска скрипт, който изчаква
    /// мечока да излезе, копира файловете върху старите и го пуска отново.
    /// Връща false, ако нещо се провали (тогава старата версия си работи).
    /// </summary>
    public async Task<bool> DownloadAndStartInstallAsync()
    {
        if (!IsAvailable || IsBusy || Environment.ProcessPath is not string exe) return false;
        IsBusy = true;
        try
        {
            // Отделна папка за всяка версия (стара, недовършена папка не пречи).
            string work = Path.Combine(Path.GetTempPath(), $"MechoUpdate_v{LatestVersion}");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);

            string zip = Path.Combine(work, "Mecho.zip");
            await using (var stream = await Http.GetStreamAsync(_zipUrl))
            await using (var file = File.Create(zip))
                await stream.CopyToAsync(file);

            // Отпечатъкът (SHA-256) от версията трябва да съвпада – иначе архивът е
            // повреден или подменен и не се инсталира. (Старите версии нямат отпечатък.)
            if (_hashUrl != null)
            {
                string expected = (await Http.GetStringAsync(_hashUrl)).Trim().ToLowerInvariant();
                string actual;
                await using (var file = File.OpenRead(zip))
                    actual = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(file)).ToLowerInvariant();
                if (expected.Length == 0 || actual != expected)
                {
                    CrashLog.Note($"Обновяването е спряно: отпечатъкът на Mecho.zip не съвпада ({actual} ≠ {expected}).");
                    return false;
                }
            }

            string extracted = Path.Combine(work, "new");
            ZipFile.ExtractToDirectory(zip, extracted);

            // В архива файловете са в папка Mecho\ (или направо в корена).
            string source = File.Exists(Path.Combine(extracted, "Mecho.exe"))
                ? extracted
                : Directory.GetDirectories(extracted).First(d => File.Exists(Path.Combine(d, "Mecho.exe")));

            string appDir = Path.GetDirectoryName(exe)!;
            // Какво да се изтрие след копирането (по реда: файлове, после празни папки).
            string deleteList = Path.Combine(work, "delete.txt");
            File.WriteAllLines(deleteList, Obsolete(appDir, source));
            string script = Path.Combine(work, "install.ps1");
            File.WriteAllText(script, $$"""
                $ErrorActionPreference = 'Stop'
                try { Wait-Process -Id {{Environment.ProcessId}} -Timeout 30 } catch { }
                Start-Sleep -Milliseconds 500
                for ($i = 0; $i -lt 10; $i++) {
                    try { Copy-Item -Path '{{Escape(source)}}\*' -Destination '{{Escape(appDir)}}' -Recurse -Force; break }
                    catch { Start-Sleep -Seconds 1 }
                }
                # Старите файлове, които новата версия вече не ползва.
                foreach ($rel in Get-Content -LiteralPath '{{Escape(deleteList)}}') {
                    if ($rel) { Remove-Item -LiteralPath (Join-Path '{{Escape(appDir)}}' $rel) -Force -ErrorAction SilentlyContinue }
                }
                # Празните папки в assets (останали след изтриването).
                $assets = Join-Path '{{Escape(appDir)}}' 'assets'
                if (Test-Path -LiteralPath $assets) {
                    Get-ChildItem -LiteralPath $assets -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending |
                        Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } |
                        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue }
                }
                Start-Process -FilePath '{{Escape(exe)}}'
                """);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Escape(string path) => path.Replace("'", "''");

    /// <summary>Списъкът files.txt на една версия (пътищата – относителни, с „/“).</summary>
    private static string[]? Manifest(string dir)
    {
        string path = Path.Combine(dir, "files.txt");
        return File.Exists(path)
            ? File.ReadAllLines(path).Select(l => l.Trim().Replace('\\', '/')).Where(l => l.Length > 0).ToArray()
            : null;
    }

    /// <summary>
    /// Кои файлове от старата версия да се изтрият: бяха в стария files.txt, а ги няма в
    /// новия. Без стар списък (по-стара версия) – само файловете в assets, които ги няма в
    /// новата. Никога не излиза от папката на програмата.
    /// </summary>
    internal static string[] Obsolete(string appDir, string newDir)
    {
        var keep = new System.Collections.Generic.HashSet<string>(
            Manifest(newDir) ?? Directory.GetFiles(newDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(newDir, f).Replace('\\', '/')),
            StringComparer.OrdinalIgnoreCase);
        var old = Manifest(appDir) ?? (Directory.Exists(Path.Combine(appDir, "assets"))
            ? Directory.GetFiles(Path.Combine(appDir, "assets"), "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(appDir, f).Replace('\\', '/')).ToArray()
            : Array.Empty<string>());
        string root = Path.GetFullPath(appDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return old
            .Where(rel => !keep.Contains(rel))
            .Where(rel => !Path.IsPathRooted(rel) && Path.GetFullPath(Path.Combine(appDir, rel)).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            .Select(rel => rel.Replace('/', Path.DirectorySeparatorChar))
            .ToArray();
    }
}
