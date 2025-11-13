using System.Runtime.InteropServices;

namespace EVServiceCenterMaintenanceAPI.Helpers
{
    public static class TimeZoneHelper
    {
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "SE Asia Standard Time"
                    : "Asia/Ho_Chi_Minh");

        public static DateTime NowInVietnam => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        public static DateTime TodayInVietnam => NowInVietnam.Date;

        public static DateTime ConvertToVietnamTime(this DateTime utcDateTime)
            => TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone);

        public static DateTime ConvertToUtc(this DateTime vietnamDateTime)
            => TimeZoneInfo.ConvertTimeToUtc(vietnamDateTime, VietnamTimeZone);
    }
}