using System;

namespace AutoThemeTray
{
    // Simple sunrise/sunset calculator using NOAA algorithm (approximate, good for scheduling)
    public static class SunriseCalculator
    {
        // Returns sunrise and sunset in UTC for the given date and lat/lon
        public static (DateTime sunrise, DateTime sunset) GetSunriseSunsetTimes(DateTime date, double lat, double lon)
        {
            // Convert to UTC date (date only)
            DateTime dateUtc = date.ToUniversalTime();
            double jd = JulianDay(dateUtc.Year, dateUtc.Month, dateUtc.Day);
            double n = jd - 2451545.0 + 0.0008;

            double Jstar = n - lon / 360.0;
            double M = (357.5291 + 0.98560028 * Jstar) % 360.0;
            double C = 1.9148 * Math.Sin(Deg2Rad(M)) + 0.0200 * Math.Sin(Deg2Rad(2 * M)) + 0.0003 * Math.Sin(Deg2Rad(3 * M));
            double lambda = (M + C + 180 + 102.9372) % 360.0;
            double Jtransit = 2451545.0 + Jstar + 0.0053 * Math.Sin(Deg2Rad(M)) - 0.0069 * Math.Sin(Deg2Rad(2 * lambda));

            double delta = Math.Asin(Math.Sin(Deg2Rad(lambda)) * Math.Sin(Deg2Rad(23.44)));
            double latRad = Deg2Rad(lat);
            double hourAngle = Math.Acos((Math.Sin(Deg2Rad(-0.83)) - Math.Sin(latRad) * Math.Sin(delta)) / (Math.Cos(latRad) * Math.Cos(delta)));

            double Jset = Jtransit + Rad2Deg(hourAngle) / 360.0;
            double Jrise = Jtransit - Rad2Deg(hourAngle) / 360.0;

            DateTime sunriseUtc = FromJulianDay(Jrise);
            DateTime sunsetUtc = FromJulianDay(Jset);
            return (sunriseUtc, sunsetUtc);
        }

        private static double Deg2Rad(double d) => d * Math.PI / 180.0;
        private static double Rad2Deg(double r) => r * 180.0 / Math.PI;

        // Julian day at noon UTC
        private static double JulianDay(int year, int month, int day)
        {
            if (month <= 2)
            {
                year -= 1;
                month += 12;
            }
            int A = year / 100;
            int B = 2 - A + A / 4;
            double jd = Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + B - 1524.5;
            return jd;
        }

        private static DateTime FromJulianDay(double jd)
        {
            double J = jd + 0.5;
            int Z = (int)Math.Floor(J);
            double F = J - Z;
            int A = Z;
            if (Z >= 2299161)
            {
                int alpha = (int)((Z - 1867216.25) / 36524.25);
                A += 1 + alpha - alpha / 4;
            }
            int B = A + 1524;
            int C = (int)((B - 122.1) / 365.25);
            int D = (int)(365.25 * C);
            int E = (int)((B - D) / 30.6001);
            double day = B - D - (int)(30.6001 * E) + F;
            int month = (E < 14) ? E - 1 : E - 13;
            int year = (month > 2) ? C - 4716 : C - 4715;

            int dayInt = (int)Math.Floor(day);
            double dayFraction = day - dayInt;
            int hours = (int)Math.Floor(dayFraction * 24);
            int minutes = (int)Math.Floor((dayFraction * 24 - hours) * 60);
            int seconds = (int)Math.Floor((((dayFraction * 24 - hours) * 60) - minutes) * 60);

            return new DateTime(year, month, dayInt, hours, minutes, seconds, DateTimeKind.Utc);
        }
    }
}