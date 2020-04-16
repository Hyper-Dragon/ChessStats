using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ChessDotComSharp.Models
{
    internal class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new UnixDateTimeConverter(),
                new StringEnumConverter()
            },
        };
    }
}
