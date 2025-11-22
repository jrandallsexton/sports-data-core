#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Text.Json.Serialization;

namespace SportsData.Producer.Infrastructure.Geo
{
    public interface IGeocodingService
    {
        Task<(double? lat, double? lng)> TryGeocodeAsync(string formattedAddress);
    }

    public class GeoCodingService : IGeocodingService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public GeoCodingService(HttpClient http, IConfiguration config)
        {
            _http = http;
            var key = config["CommonConfig:GoogleMaps:ApiKey"];

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException("Google Maps API key is not configured.");

            _apiKey = key;
        }

        public async Task<(double?, double?)> TryGeocodeAsync(string address)
        {
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_apiKey}";
            var response = await _http.GetFromJsonAsync<GeocodeResponse>(url);

            var location = response?.Results?.FirstOrDefault()?.Geometry?.Location;
            return location != null ? (location.Lat, location.Lng) : (null, null);
        }

        public class GeocodeResponse
        {
            [JsonPropertyName("results")]
            public List<Result> Results { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; } = null!;

            public class AddressComponent
            {
                [JsonPropertyName("long_name")]
                public string LongName { get; set; }

                [JsonPropertyName("short_name")]
                public string ShortName { get; set; }

                [JsonPropertyName("types")]
                public List<string> Types { get; set; }
            }

            public class Geometry
            {
                [JsonPropertyName("location")]
                public Location Location { get; set; }

                [JsonPropertyName("location_type")]
                public string LocationType { get; set; }

                [JsonPropertyName("viewport")]
                public Viewport Viewport { get; set; }
            }

            public class Location
            {
                [JsonPropertyName("lat")]
                public double? Lat { get; set; }

                [JsonPropertyName("lng")]
                public double? Lng { get; set; }

                [JsonPropertyName("latitude")]
                public double? Latitude { get; set; }

                [JsonPropertyName("longitude")]
                public double? Longitude { get; set; }
            }

            public class NavigationPoint
            {
                [JsonPropertyName("location")]
                public Location Location { get; set; }

                [JsonPropertyName("restricted_travel_modes")]
                public List<string> RestrictedTravelModes { get; set; }
            }

            public class Northeast
            {
                [JsonPropertyName("lat")]
                public double? Lat { get; set; }

                [JsonPropertyName("lng")]
                public double? Lng { get; set; }
            }

            public class PlusCode
            {
                [JsonPropertyName("compound_code")]
                public string CompoundCode { get; set; }

                [JsonPropertyName("global_code")]
                public string GlobalCode { get; set; }
            }

            public class Result
            {
                [JsonPropertyName("address_components")]
                public List<AddressComponent> AddressComponents { get; set; }

                [JsonPropertyName("formatted_address")]
                public string FormattedAddress { get; set; }

                [JsonPropertyName("geometry")]
                public Geometry Geometry { get; set; }

                [JsonPropertyName("navigation_points")]
                public List<NavigationPoint> NavigationPoints { get; set; }

                [JsonPropertyName("place_id")]
                public string PlaceId { get; set; }

                [JsonPropertyName("plus_code")]
                public PlusCode PlusCode { get; set; }

                [JsonPropertyName("types")]
                public List<string> Types { get; set; }
            }

            public class Southwest
            {
                [JsonPropertyName("lat")]
                public double? Lat { get; set; }

                [JsonPropertyName("lng")]
                public double? Lng { get; set; }
            }

            public class Viewport
            {
                [JsonPropertyName("northeast")]
                public Northeast Northeast { get; set; }

                [JsonPropertyName("southwest")]
                public Southwest Southwest { get; set; }
            }

        }
    }
}
