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

    public enum ReminderType
    {
        Maintenance,
        Payment,
        Renewal,
        Rating
    }

    public static class DefaultAvatar
    {
        public const string Local = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSAhEOYTOMNLDkzULpt0bj-RdWGvRsfw5S-aQ&s";
        public const string Production = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSAhEOYTOMNLDkzULpt0bj-RdWGvRsfw5S-aQ&s";
    }
}
