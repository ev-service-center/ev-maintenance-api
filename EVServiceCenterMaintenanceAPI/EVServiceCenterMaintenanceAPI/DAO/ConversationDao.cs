using EVServiceCenterMaintenanceAPI.Models;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ConversationDao
    {
        private readonly EvserviceCenterDbContext _context;

        public ConversationDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }
    }
}
