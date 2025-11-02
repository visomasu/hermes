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
