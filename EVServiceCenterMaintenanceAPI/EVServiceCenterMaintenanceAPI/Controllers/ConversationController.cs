using EVServiceCenterMaintenanceAPI.DAO;
using Microsoft.AspNetCore.Mvc;

namespace EVServiceCenterMaintenanceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly ConversationDao _conversationDao;

        public ConversationController(ConversationDao conversationDao)
        {
            _conversationDao = conversationDao;
        }
    }
}
