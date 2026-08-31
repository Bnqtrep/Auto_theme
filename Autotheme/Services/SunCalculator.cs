using System;

namespace Autotheme.Services
{
    // Simple sunrise/sunset calculator using NOAA algorithm (approximate)
    public static class SunCalculator
    {
        // Returns sunrise and sunset in local time
        public static void GetSunriseSunset(DateTime date, double lat, double lon, out DateTime sunriseLocal, out DateTime sunsetLocal)
        {
            // Based on NOAA Solar Calculator. Keep implementation compact.
            var day = date.Date;
            var N = day.DayOfYear;

            double lngHour = lon / 15.0;

            double tRise = N + ((6 - lngHour) / 24);
            double tSet = N + ((18 - lngHour) / 24);

            double M_rise = (0.9856 * tRise) - 3.289;
            double M_set = (0.9856 * tSet) - 3.289;

            double[] Ms = { M_rise, M_set };
            double[] times = new double[2];

            for (int i = 0; i < 2; i++)
            {
                double M = Ms[i];
                double L = M + (1.916 * Math.Sin(Deg2Rad(M))) + (0.020 * Math.Sin(Deg2Rad(2 * M))) + 282.634;
                L = NormalizeDegrees(L);
                double RA = Rad2Deg(Math.Atan(0.91764 * Math.Tan(Deg2Rad(L))));
                RA = NormalizeDegrees(RA);

                double Lquadrant = Math.Floor(L / 90) * 90;
                double RAquadrant = Math.Floor(RA / 90) * 90;
                RA = RA + (Lquadrant - RAquadrant);
                RA /= 15;

                double sinDec = 0.39782 * Math.Sin(Deg2Rad(L));
                double cosDec = Math.Cos(Math.Asin(sinDec));

                double cosH = (Math.Cos(Deg2Rad(90.833)) - (sinDec * Math.Sin(Deg2Rad(lat)))) / (cosDec * Math.Cos(Deg2Rad(lat)));
                if (cosH > 1) // always night
                {
                    times[i] = double.NaN;
                    continue;
                }
                if (cosH < -1) // always day
                {
                    times[i] = double.NaN;
                    continue;
                }

                double H = (i == 0) ? 360 - Rad2Deg(Math.Acos(cosH)) : Rad2Deg(Math.Acos(cosH));
                H /= 15;

                double T = H + RA - (0.06571 * ((i == 0) ? tRise : tSet)) - 6.622;
                double UT = T - lngHour;
                UT = UT % 24;
                if (UT < 0) UT += 24;

                var resultUtc = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Utc).AddHours(UT);
                times[i] = resultUtc.ToLocalTime().TimeOfDay.TotalHours;
            }

            if (double.IsNaN(times[0]))
                sunriseLocal = DateTime.MinValue;
            else
                sunriseLocal = day.AddHours(times[0]);

            if (double.IsNaN(times[1]))
                sunsetLocal = DateTime.MinValue;
            else
                sunsetLocal = day.AddHours(times[1]);
        }

        private static double Deg2Rad(double d) => d * Math.PI / 180.0;
        private static double Rad2Deg(double r) => r * 180.0 / Math.PI;
        private static double NormalizeDegrees(double d)
        {
            d = d % 360;
            if (d < 0) d += 360;
            return d;
        }
    }
}
