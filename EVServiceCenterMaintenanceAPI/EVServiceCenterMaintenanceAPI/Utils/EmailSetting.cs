namespace EVServiceCenterMaintenanceAPI.Utils
{
    public class EmailSetting
    {
        public required string Server { get; set; }
        public required int Port { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string Sender { get; set; }
        public bool UseApi { get; set; } = false; // Use SendGrid API instead of SMTP
    }
}
