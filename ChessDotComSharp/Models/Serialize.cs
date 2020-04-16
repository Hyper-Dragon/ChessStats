using Newtonsoft.Json;

namespace ChessDotComSharp.Models
{
    public static class Serialize
    {
        public static string ToJson(this PlayerProfile self)
        {
            return JsonConvert.SerializeObject(self, Converter.Settings);
        }

        public static string ToJson(this PlayerStats self)
        {
            return JsonConvert.SerializeObject(self, Converter.Settings);
        }

        public static string ToJson(this GameList self)
        {
            return JsonConvert.SerializeObject(self, Converter.Settings);
        }
    }
}
