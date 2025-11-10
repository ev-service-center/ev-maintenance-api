using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class ChatDao
    {
        private readonly EvserviceCenterDbContext _context;

        public ChatDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Chat> CreateChatAsync(Chat chat)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                chat.CreatedAt = DateTime.UtcNow;
                chat.UpdatedAt = DateTime.UtcNow;
                chat.SentDate = DateTime.UtcNow;
                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return chat;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create chat.", ex);
            }
        }

        public async Task<Chat?> GetChatByIdAsync(int chatId)
        {
            return await _context.Chats.FirstOrDefaultAsync(c => c.ChatId == chatId);
        }
    }
}
