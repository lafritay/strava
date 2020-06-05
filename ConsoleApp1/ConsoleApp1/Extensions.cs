using System;

namespace ConsoleApp1
{
    public static class Extensions
    {
        public static string InMiles(
            this float meters)
        {
            double miles = meters * 0.000621371;
            return Math.Round(miles, 2).ToString();
        }

        public static string InHours(
            this long seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString();
        }
    }
}