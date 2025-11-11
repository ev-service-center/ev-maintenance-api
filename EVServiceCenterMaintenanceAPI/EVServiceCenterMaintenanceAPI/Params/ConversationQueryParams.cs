using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class ConversationQueryParams : QueryParams
    {
        public ConversationStatus? StatusConversation { get; set; }
        public int? UserId { get; set; }
        public bool? IsCustomer { get; set; }
    }
}
