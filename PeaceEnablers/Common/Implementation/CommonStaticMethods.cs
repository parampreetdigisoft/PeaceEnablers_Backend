using System.Net;
using System.Text.RegularExpressions;

namespace PeaceEnablers.Common.Implementation
{
    public class CommonStaticMethods
    {
        public static string GetConditionByScore(decimal score)
        {
            if (score <= 20)
                return "Critical";

            if (score <= 40)
                return "Fragile";

            if (score <= 60)
                return "Developing";

            if (score <= 80)
                return "Stable";

            return "Strong";
        }
        public static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove HTML tags
            var noTags = Regex.Replace(input, "<.*?>", string.Empty);

            // Decode HTML entities (e.g., &mdash;)
            return WebUtility.HtmlDecode(noTags);
        }
    }
}
