namespace EVServiceCenterMaintenanceAPI.Enums
{

    public enum StringLength
    {
        FullName = 50,
        MinUsername = 3,
        MaxUsername = 20,
        MinPassWord = 8,
        MaxPassWord = 32,
        Email = 100,
        Token = 500,
        Otp = 6
    }
    public enum UserRole
    {
        Customer,
        Staff,
        Technician,
        Admin
    }

    public enum UserStatus
    {
        Pending,
        Active,
        Inactive,
        Suspended,
        Deleted
    }

    public enum TokenType
    {
        OTP,
        Activation,
        Refresh
    }

    public enum ServiceStatus
    {
        Active,
        Inactive
    }

    public enum ReminderType
    {
        Maintenance,
        Payment,
        Renewal,
        Rating
    }

    public enum PartStatus
    {
        Active,
        Inactive
    }

    public enum ServiceCenterStatus
    {
        Open,
        Closed,
        Maintenance,
        Deleted
    }

    public enum AppointmentStatus
    {
        Pending,
        Confirmed,
        InProgress,
        Completed,
        Cancelled
    }

    public enum WorkOrderStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }

    public enum VehicleStatus
    {
        Active,
        Inactive
    }

    public enum SlotDuration
    {
        ThirtyMinutes = 30,
        OneHour = 60,
        OneHourThirty = 90,
        TwoHours = 120
    }

    public static class DefaultAvatar
    {
        public const string Local = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSAhEOYTOMNLDkzULpt0bj-RdWGvRsfw5S-aQ&s";
        public const string Production = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSAhEOYTOMNLDkzULpt0bj-RdWGvRsfw5S-aQ&s";
    }

    public static class SlotTimeConfig
    {
        public static readonly TimeSpan StartTimeOfDay = TimeSpan.FromHours(7);   // 7:00 sáng
        public static readonly TimeSpan EndTimeOfDay = TimeSpan.FromHours(19);    // 7:00 tối
        public static readonly TimeSpan LunchBreakStart = TimeSpan.FromHours(11); // 11:00 sáng - bắt đầu nghỉ trưa
        public static readonly TimeSpan LunchBreakEnd = TimeSpan.FromHours(13);   // 13:00 chiều - kết thúc nghỉ trưa
        public static readonly SlotDuration DefaultDuration = SlotDuration.TwoHours; // 2 giờ cho mỗi slot

        public static TimeSpan GetLastSlotEndTime()
        {
            var durationMinutes = (int)DefaultDuration;
            var totalMinutes = (int)(EndTimeOfDay - StartTimeOfDay).TotalMinutes;
            var remainingMinutes = totalMinutes % durationMinutes;

            if (remainingMinutes == 0)
                return EndTimeOfDay;

            return EndTimeOfDay.Subtract(TimeSpan.FromMinutes(remainingMinutes));
        }

        /// <summary>
        /// Kiểm tra xem một slot có overlap với giờ nghỉ trưa không
        /// </summary>
        public static bool IsOverlapWithLunchBreak(TimeSpan slotStart, TimeSpan slotEnd)
        {
            // Slot overlap với lunch break nếu:
            // - Slot bắt đầu trước khi lunch break kết thúc VÀ
            // - Slot kết thúc sau khi lunch break bắt đầu
            return slotStart < LunchBreakEnd && slotEnd > LunchBreakStart;
        }
    }
    public static class HostBookingUrl
    {
        private const string Local = "http://localhost:3000/booking/callback/";
        private const string Production = "https://demo.com/booking/callback/";

        public static string GetBaseUrl(HostEnvironment env)
        {
            return env switch
            {
                HostEnvironment.Local => Local,
                HostEnvironment.Production => Production,
                _ => throw new ArgumentOutOfRangeException(nameof(env), env, null)
            };
        }

        public static string GetCancelUrl(HostEnvironment env, int bookingId)
        {
            return $"{GetBaseUrl(env)}cancel?bookingId={bookingId}";
        }

        public static string GetSuccessUrl(HostEnvironment env, int bookingId)
        {
            return $"{GetBaseUrl(env)}success?bookingId={bookingId}";
        }

        public enum HostEnvironment
        {
            Local,
            Production
        }
    }
}
