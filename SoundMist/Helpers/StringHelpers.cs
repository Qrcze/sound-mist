using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundMist.Helpers
{
    static class StringHelpers
    {

        public static string DurationFormatted(int fullDuration)
        {
            var time = TimeSpan.FromMilliseconds(fullDuration);
            if (time.Hours > 0)
                return time.ToString(@"h\:mm\:ss");
            return time.ToString(@"m\:ss");
        }

        public static string TimeAgo(DateTime createdAt)
        {
            var createdLocalTime = createdAt.ToLocalTime();
            DateTime diff = new(DateTime.Now.Ticks - createdLocalTime.Ticks);
            if (diff.Year - 1 > 0)
                return $"{diff.Year - 1} year{(diff.Year - 1 > 1 ? "s" : "")} ago";
            if (diff.Month - 1 > 0)
                return $"{diff.Month - 1} month{(diff.Month - 1 > 1 ? "s" : "")} ago";
            if (diff.Day - 1 > 0)
                return $"{diff.Day - 1} day{(diff.Day - 1 > 1 ? "s" : "")} ago";
            if (diff.Hour - 1 > 0)
                return $"{diff.Hour - 1} hour{(diff.Hour - 1 > 1 ? "s" : "")} ago";
            return $"{diff.Minute - 1} minute{(diff.Minute - 1 > 1 ? "s" : "")} ago";
        }

        public static string ShortenedNumber(int num)
        {
            if (num > 1_000_000)
                return $"{(float)num / 1_000_000:0.##}M";
            if (num > 10_000)
                return $"{(float)num / 1_000:0.##}K";
            return num.ToString();
        }

        public static string NumberWithSeparators(int num)
        {
            return num.ToString(CultureInfo.CurrentCulture);
        }
    }
}
