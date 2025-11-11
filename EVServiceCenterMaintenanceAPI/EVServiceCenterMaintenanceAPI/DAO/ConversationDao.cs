using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
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

        public async Task<(List<Conversation> Conversations, int Total)> GetAllConversationsAsync(ConversationQueryParams queryParams)
        {
            var validation = queryParams.Validate();
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage);
            }

            var query = _context.Conversations
                .Include(c => c.Customer)
                .Include(c => c.Staff)
                .AsQueryable();

            if (queryParams.StatusConversation.HasValue)
                query = query.Where(c => c.Status == queryParams.StatusConversation.ToString());
            if (queryParams.UserId.HasValue && queryParams.IsCustomer.HasValue)
            {
                if (queryParams.IsCustomer.Value)
                    query = query.Where(c => c.CustomerId == queryParams.UserId.Value);
                else
                    query = query.Where(c => c.StaffId == queryParams.UserId.Value);
            }
            if (queryParams.FromDate.HasValue)
                query = query.Where(c => c.CreatedAt >= queryParams.FromDate.Value);
            if (queryParams.ToDate.HasValue)
                query = query.Where(c => c.CreatedAt <= queryParams.ToDate.Value);

            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                bool isAscending = queryParams.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);
                switch (queryParams.SortBy.ToLower())
                {
                    case "status":
                        query = isAscending ? query.OrderBy(c => c.Status) : query.OrderByDescending(c => c.Status);
                        break;
                    case "createdat":
                        query = isAscending ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt);
                        break;
                    default:
                        query = isAscending ? query.OrderBy(c => c.ConversationId) : query.OrderByDescending(c => c.ConversationId);
                        break;
                }
            }

            var total = await query.CountAsync();
            var conversations = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (conversations, total);
        }

        public async Task<Conversation> CreateConversationAsync(Conversation conversation)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                conversation.CreatedAt = DateTime.UtcNow;
                conversation.UpdatedAt = DateTime.UtcNow;
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return conversation;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Failed to create conversation.", ex);
            }
        }

        public async Task<List<Conversation>> GetConversationsByUserIdAsync(int userId, bool isCustomer)
        {
            var query = isCustomer ? _context.Conversations.Where(c => c.CustomerId == userId) : _context.Conversations.Where(c => c.StaffId == userId);
            return await query
                .Include(c => c.Chats)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }
    }
}
