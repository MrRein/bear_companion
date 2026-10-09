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
/// Версия N е GitHub release с таг "vN"; текущата е третото число от версията на програмата.
/// </summary>
public sealed class Updater
{
    private const string LatestUrl = "https://api.github.com/repos/MrRein/bear_companion/releases/latest";

    public enum Result { UpToDate, Available, Failed }

    private static readonly HttpClient Http = CreateClient();

    public static int CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.Build ?? 0;

    public int LatestVersion { get; private set; }
    public bool IsAvailable => LatestVersion > CurrentVersion && _zipUrl != null;
    public bool IsBusy { get; private set; }

    private string? _zipUrl;

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

            _zipUrl = root.GetProperty("assets").EnumerateArray()
                .Where(a => a.GetProperty("name").GetString() == "Mecho.zip")
                .Select(a => a.GetProperty("browser_download_url").GetString())
                .FirstOrDefault();
            LatestVersion = latest;
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
            string work = Path.Combine(Path.GetTempPath(), "MechoUpdate");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);

            string zip = Path.Combine(work, "Mecho.zip");
            await using (var stream = await Http.GetStreamAsync(_zipUrl))
            await using (var file = File.Create(zip))
                await stream.CopyToAsync(file);

            string extracted = Path.Combine(work, "new");
            ZipFile.ExtractToDirectory(zip, extracted);

            // В архива файловете са в папка Mecho\ (или направо в корена).
            string source = File.Exists(Path.Combine(extracted, "Mecho.exe"))
                ? extracted
                : Directory.GetDirectories(extracted).First(d => File.Exists(Path.Combine(d, "Mecho.exe")));

            string appDir = Path.GetDirectoryName(exe)!;
            string script = Path.Combine(work, "install.ps1");
            File.WriteAllText(script, $$"""
                $ErrorActionPreference = 'Stop'
                try { Wait-Process -Id {{Environment.ProcessId}} -Timeout 30 } catch { }
                Start-Sleep -Milliseconds 500
                for ($i = 0; $i -lt 10; $i++) {
                    try { Copy-Item -Path '{{Escape(source)}}\*' -Destination '{{Escape(appDir)}}' -Recurse -Force; break }
                    catch { Start-Sleep -Seconds 1 }
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
}
