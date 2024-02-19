using Newtonsoft.Json;

namespace SportsData.Core.Extensions
{
    // TODO: Convert this to System.Text.Json
    public static class JsonExtensions
    {

        public static T FromJson<T>(this string json, JsonSerializerSettings jsonSerializerSettings = null)
            where T : class
        {
            return string.IsNullOrEmpty(json) ? default : JsonConvert.DeserializeObject<T>(json, jsonSerializerSettings);
        }

        public static object FromJson(this string value)
        {
            return JsonConvert.DeserializeObject<object>(value);
        }

        public static string ToJson(this object obj, JsonSerializerSettings jsonSerializerSettings = null)
        {
            var defaultSerializerSettings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            return JsonConvert.SerializeObject(obj, Formatting.None, jsonSerializerSettings ?? defaultSerializerSettings);
        }
    }
}
