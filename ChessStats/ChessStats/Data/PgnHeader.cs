using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ChessStats.Data
{
    public class PgnHeader
    {
        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();

        public enum SupportedAttribute
        {
            Event,
            Site,
            Date,
            Round,
            White,
            Black,
            Result,
            ECO,
            ECOUrl,
            CurrentPosition,
            Timezone,
            UTCTime,
            WhiteElo,
            BlackElo,
            TimeControl,
            Termination,
            StartTime,
            EndDate,
            EndTime,
            Link,
            EventDate,
            EventTime,
            Rules
        }

        public string GetAttributeAsNullOrString(SupportedAttribute attributeName)
        {
            return this.Attributes.ContainsKey(attributeName.ToString()) ? this.Attributes[attributeName.ToString()] : null;
        }

        public Nullable<short> GetAttributeAsNullOrShort(SupportedAttribute attributeName)
        {
            try
            {
                return this.Attributes.ContainsKey(attributeName.ToString()) ? (short?)short.Parse(this.Attributes[attributeName.ToString()]) : null;
            }
            catch
            {
                return null;
            }
        }

        public DateTime? GetAttributeAsNullOrDateTime(SupportedAttribute attributeNameDate, SupportedAttribute attributeNameTime)
        {
            try
            {
                //if we have a date and time join them/if there is only a date return that/return null if there is only a time
                if (this.Attributes.ContainsKey(attributeNameDate.ToString()) && this.Attributes.ContainsKey(attributeNameTime.ToString()))
                {
                    return (DateTime?)DateTime.Parse($"{this.Attributes[attributeNameDate.ToString()]} {this.Attributes[attributeNameTime.ToString()]}");
                }
                else if (this.Attributes.ContainsKey(attributeNameDate.ToString()))
                {
                    return DateTime.Parse($"{this.Attributes[attributeNameDate.ToString()]}");
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static PgnHeader GetHeaderFromText(string gameText)
        {
            var pgnHeader = new PgnHeader();

            foreach (string attribute in Regex.Split(gameText, @"^\[(.*?)\]", RegexOptions.Multiline))
            {
                string[] nameVal = attribute.TrimStart('[').TrimEnd(']').Split(' ', 2);

                if (nameVal.Length == 2)
                {
                    try
                    {
                        var attrib = ((PgnHeader.SupportedAttribute)Enum.Parse(typeof(PgnHeader.SupportedAttribute), nameVal[0], true)).ToString();
                        var attribValue = nameVal[1].TrimStart('\"').TrimEnd('\"');

                        //HACK: Fix for KingBase Dates (some missing days/month)
                        if (attrib.ToLower().Contains("date")) attribValue = attribValue.Replace("??", "01").Replace("?", "01");

                        pgnHeader.Attributes.Add(
                            ((PgnHeader.SupportedAttribute)Enum.Parse(typeof(PgnHeader.SupportedAttribute), nameVal[0], true)).ToString(),
                            nameVal[1].TrimStart('\"').TrimEnd('\"'));
                    }
                    catch
                    {
                        //Not Supported So Skip
                    }
                }
            }

            return pgnHeader;
        }
    }
}

