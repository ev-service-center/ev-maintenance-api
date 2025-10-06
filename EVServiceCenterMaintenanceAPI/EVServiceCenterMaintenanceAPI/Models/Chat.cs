using System;
using System.Collections.Generic;

namespace EVServiceCenterMaintenanceAPI.Models;

public partial class Chat
{
    public int ChatId { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime SentDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
