using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ConversationDao
    {
        private readonly EvserviceCenterDbContext _context;

        public ConversationDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Conversation?> GetConversationByIdAsync(int conversationId)
        {
            return await _context.Conversations
                .Include(c => c.Chats)
                .FirstOrDefaultAsync(c => c.ConversationId == conversationId);
        }
    }
}
