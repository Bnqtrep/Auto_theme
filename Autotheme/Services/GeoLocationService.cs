using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Autotheme.Services
{
    // IP-based geolocation. This uses a third-party public API (https://ip-api.com/) to resolve
    // approximate latitude/longitude for the machine's public IP. This is not a Microsoft API.
    public static class GeoLocationService
    {
        private record IpApiResponse(string status, double lat, double lon);

        public static async Task<(double latitude, double longitude)> GetLocationAsync()
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Autotheme/1.0");
            var resp = await http.GetAsync("http://ip-api.com/json/");
            resp.EnsureSuccessStatusCode();
            var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("status", out var st) && st.GetString() == "success")
            {
                var lat = doc.RootElement.GetProperty("lat").GetDouble();
                var lon = doc.RootElement.GetProperty("lon").GetDouble();
                return (lat, lon);
            }

            // fallback: UTC+0 coordinates
            return (0.0, 0.0);
        }
    }
}
