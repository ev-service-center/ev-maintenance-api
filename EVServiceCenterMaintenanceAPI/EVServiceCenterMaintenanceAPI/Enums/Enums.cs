namespace EVServiceCenterMaintenanceAPI.Enums
{
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
        Suspended
    }

    public enum TokenType
    {
        OTP,
        Activation,
        Refresh
    }

    public static class DefaultAvatar
    {
        public const string Local = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSAhEOYTOMNLDkzULpt0bj-RdWGvRsfw5S-aQ&s";
        public const string Production = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSAhEOYTOMNLDkzULpt0bj-RdWGvRsfw5S-aQ&s";
    }
}
