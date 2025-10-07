namespace EVServiceCenterMaintenanceAPI.Utils
{
    public class EmailSetting
    {
        public required string Server { get; set; }
        public required int Port { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
    }
}
