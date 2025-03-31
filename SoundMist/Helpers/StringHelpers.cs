﻿using System;
using System.Collections.Generic;
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
                return time.ToString(@"hh\:mm\:ss");
            return time.ToString(@"mm\:ss");
        }

        internal static string CreatedAgo(DateTime createdAt)
        {
            var createdLocalTime = createdAt.ToLocalTime();
            DateTime diff = new(DateTime.Now.Ticks - createdLocalTime.Ticks);
            if (diff.Year - 1 > 0)
                return $"{diff.Year - 1} years ago";
            if (diff.Month - 1 > 0)
                return $"{diff.Month - 1} months ago";
            if (diff.Day - 1 > 0)
                return $"{diff.Day - 1} days ago";
            if (diff.Hour - 1 > 0)
                return $"{diff.Hour - 1} hours ago";
            return $"{diff.Minute - 1} minutes ago";
        }

        internal static string ShortenedNumber(int num)
        {
            if (num > 1_000_000)
                return $"{(float)num / 1_000_000:0.##}M";
            if (num > 10_000)
                return $"{(float)num / 1_000:0.##}K";
            return num.ToString();
        }
    }
}
