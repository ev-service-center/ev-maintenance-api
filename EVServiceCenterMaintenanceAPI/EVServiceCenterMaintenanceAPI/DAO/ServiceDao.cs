using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ServiceDao
    {
        private readonly EvserviceCenterDbContext _context;

        public ServiceDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Service?> GetActiveServiceByIdAsync(int serviceId)
        {
            return await _context.Services
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId && s.Status == ServiceStatus.Active.ToString());
        }
    }
}
