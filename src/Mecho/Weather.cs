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

    /// <summary>Под толкова градуса му е студено, над толкова – горещо.</summary>
    public const double Cold = 10, Hot = 25;

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
                "https://api.open-meteo.com/v1/forecast?latitude={0:0.###}&longitude={1:0.###}&current=temperature_2m",
                save.WeatherLat, save.WeatherLon);
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            return doc.RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble();
        }
        catch
        {
            return null;
        }
    }
}
