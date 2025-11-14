using EVServiceCenterMaintenanceAPI.DAO;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReminderController : ControllerBase
    {
        private readonly ReminderDao _reminderDao;

        public ReminderController(ReminderDao reminderDao)
        {
            _reminderDao = reminderDao;
        }
    }
}
