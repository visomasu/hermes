using System.Text.Json.Serialization;

namespace Hermes.Controllers.Models
{
    /// <summary>
    /// Represents the input for a chat request.
    /// </summary>
    public class ChatInput
    {
        /// <summary>
        /// The text message sent by the user.
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Optional user identifier (Teams user ID or email) for the user sending the message.
        /// Used to provide user context for tool calls and personalized responses.
        /// </summary>
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        /// <summary>
        /// Optional session identifier for tracking multi-turn conversations.
        /// If not provided, a new session ID will be generated.
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }

        /// <summary>
        /// Initializes a new instance of the ChatInput class with the specified text content.
        /// </summary>
        /// <param name="text">The text to be used as the input message. Cannot be null.</param>
        public ChatInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));
            }

            this.Text = text;
        }
    }
}
