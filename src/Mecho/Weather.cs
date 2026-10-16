using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mecho;

/// <summary>
/// Температурата навън: мястото се познава по интернет връзката (ipwho.is, без
/// ключ), а температурата е от Open-Meteo (безплатно, без ключ). Без интернет –
/// няма температура и мечокът не говори за студ и топло.
/// </summary>
public static class Weather
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mecho-weather");
        return http;
    }

    /// <summary>
    /// Скалата (закръглени градуси): ≤0 мраз, 1–5 много студено, 6–10 студено,
    /// 11–15 хладно, 16–24 приятно (до 20 хладничко-топло, после топло), 25–30 горещо, 31+ жега.
    /// </summary>
    public const double Cool = 15, Cold = 10, Hot = 25, VeryHot = 31;

    /// <summary>Какво е времето сега (код по WMO: 0 ясно, 61 дъжд, 71 сняг, 95 гръмотевици…) и вятърът в км/ч.</summary>
    public static int Code { get; private set; } = -1;
    public static double Wind { get; private set; }

    /// <summary>Developer: измислено време (код по WMO и вятър).</summary>
    internal static void Fake(int code, double wind)
    {
        Code = code;
        Wind = wind;
    }

    /// <summary>Мястото (веднъж на ден) и температурата сега, или null.</summary>
    public static async Task<double?> FetchAsync(SaveData save)
    {
        try
        {
            if (double.IsNaN(save.WeatherLat) || save.WeatherPlaceDay != DateTime.Today.ToString("yyyy-MM-dd"))
            {
                using var place = JsonDocument.Parse(await Http.GetStringAsync("https://ipwho.is/"));
                var root = place.RootElement;
                if (root.TryGetProperty("latitude", out var lat) && root.TryGetProperty("longitude", out var lon))
                {
                    save.WeatherLat = lat.GetDouble();
                    save.WeatherLon = lon.GetDouble();
                    save.WeatherCity = root.TryGetProperty("city", out var city) ? city.GetString() ?? "" : "";
                    save.WeatherPlaceDay = DateTime.Today.ToString("yyyy-MM-dd");
                }
            }
            if (double.IsNaN(save.WeatherLat)) return null;
            string url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:0.###}&longitude={1:0.###}&current=temperature_2m,weather_code,wind_speed_10m",
                save.WeatherLat, save.WeatherLon);
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var current = doc.RootElement.GetProperty("current");
            if (current.TryGetProperty("weather_code", out var code)) Code = code.GetInt32();
            if (current.TryGetProperty("wind_speed_10m", out var wind)) Wind = wind.GetDouble();
            return current.GetProperty("temperature_2m").GetDouble();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Какво вали/как е навън – за репликите (раздел weather.&lt;вид&gt;) и менюто.</summary>
    public static string? Kind(double? temp) => Code switch
    {
        >= 95 => "storm",
        >= 71 and <= 77 or 85 or 86 => "snow",
        63 or 65 or 81 or 82 or 67 => "heavyrain",
        >= 51 and <= 67 or 80 => "rain",
        45 or 48 => "fog",
        _ when Wind >= 30 => "windy",
        _ when temp is { } t0 && Math.Round(t0) <= 0 => "frost",
        _ when temp is { } t1 && Math.Round(t1) >= VeryHot => "heat",
        _ when temp is { } t2 && Math.Round(t2) >= Hot => "hot",
        _ when temp is { } t3 && Math.Round(t3) <= Cold => "cold",
        _ when temp is { } t4 && Math.Round(t4) <= Cool => "cool",
        0 or 1 when DateTime.Now.Hour is >= 8 and < 19 => "sunny",
        2 or 3 => "cloudy",
        _ => null,
    };

    /// <summary>Пикселната иконка за небето (assets/ui/w_*.png), като в прогнозата.</summary>
    public static string SkyIcon(DateTime now) => Code switch
    {
        >= 95 => "w_storm",
        >= 71 and <= 77 or 85 or 86 => "w_snow",
        63 or 65 or 81 or 82 or 67 => "w_heavyrain",
        >= 51 and <= 67 or 80 => "w_rain",
        45 or 48 => "w_fog",
        _ when Wind >= 30 => "w_wind",
        0 or 1 => PetWindow.IsDark(now) ? "w_moon" : "w_sun",
        2 => PetWindow.IsDark(now) ? "w_cloud" : "w_partly",
        _ => "w_cloud",
    };

    /// <summary>Кратко за менюто: „вали“, „сняг“…</summary>
    public static string? KindText(double? temp) => Kind(temp) switch
    {
        "storm" => "буря", "snow" => "сняг", "heavyrain" => "силен дъжд", "rain" => "вали",
        "fog" => "мъгла", "windy" => "ветровито", "sunny" => "слънчево", "cloudy" => "облачно", _ => null,
    };
}
