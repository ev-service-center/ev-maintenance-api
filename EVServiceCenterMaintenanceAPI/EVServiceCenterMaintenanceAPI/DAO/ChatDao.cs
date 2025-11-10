using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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

        public async Task<(List<Chat> Chats, int Total)> GetAllChatsAsync(ChatQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.Chats.AsQueryable();
            if (queryParams.ConversationId.HasValue)
                query = query.Where(c => c.ConversationId == queryParams.ConversationId.Value);
            if (queryParams.FromDate.HasValue)
                query = query.Where(c => c.SentDate >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(c => c.SentDate <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "sentdate":
                        query = isAscending ? query.OrderBy(c => c.SentDate) : query.OrderByDescending(c => c.SentDate);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(c => c.ChatId) : query.OrderByDescending(c => c.ChatId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var chats = await query
                .OrderByDescending(c => c.SentDate)
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (chats, total);
        }

        public async Task<List<Chat>> GetChatsByConversationIdAsync(int conversationId)
        {
            return await _context.Chats
                .Where(c => c.ConversationId == conversationId)
                .OrderBy(c => c.SentDate)
                .ToListAsync();
        }

        public async Task<Chat> UpdateChatAsync(Chat chat)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingChat = await _context.Chats.FirstOrDefaultAsync(c => c.ChatId == chat.ChatId);
                if (existingChat == null)
                    throw new Exception($"Chat with ID {chat.ChatId} not found.");

                existingChat.Message = chat.Message;
                existingChat.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return existingChat;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update chat with ID {chat.ChatId}.", ex);
            }
        }
    }
}
