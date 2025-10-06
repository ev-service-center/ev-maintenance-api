using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class Conversation
{
    public int ConversationId { get; set; }

    public int CustomerId { get; set; }

    public int StaffId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();

    public virtual User Customer { get; set; } = null!;

    public virtual User Staff { get; set; } = null!;
}
