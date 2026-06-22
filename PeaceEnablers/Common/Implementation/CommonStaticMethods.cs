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
    }
}
