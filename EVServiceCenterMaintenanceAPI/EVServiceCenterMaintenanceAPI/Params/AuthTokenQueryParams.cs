using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class AuthTokenQueryParams : QueryParams
    {
        public TokenType? TokenType { get; set; }
        public int? UserId { get; set; }
    }
}
