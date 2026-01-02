using System.Collections.Generic;
using Hermes.Storage.Core.Models;

namespace Hermes.Storage.Repositories.ConversationHistory
{
    /// <summary>
    /// Document model for storing conversation history per conversation id.
    /// </summary>
    public class ConversationHistoryDocument : Document
    {
        /// <summary>
        /// Gets or sets the conversation history as a list of messages.
        /// </summary>
        public List<ConversationMessage> History { get; set; } = new();
    }
}
