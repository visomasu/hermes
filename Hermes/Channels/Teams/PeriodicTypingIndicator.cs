using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;

namespace Hermes.Channels.Teams
{
	/// <summary>
	/// Manages periodic typing indicator activity for Teams conversations.
	/// Sends typing activities at regular intervals to indicate that the bot is processing.
	/// </summary>
	public sealed class PeriodicTypingIndicator : IDisposable
	{
		private readonly ITurnContext _turnContext;
		private readonly string _phrase;
		private readonly CancellationTokenSource _cancellationTokenSource;
		private readonly Task _typingTask;

		private const int TypingIntervalMilliseconds = 2500; // 2.5 seconds

		/// <summary>
		/// Initializes a new instance of the PeriodicTypingIndicator class and starts sending typing indicators.
		/// </summary>
		/// <param name="turnContext">The turn context for sending activities.</param>
		/// <param name="phrase">The waiting phrase to store with the typing activity.</param>
		public PeriodicTypingIndicator(ITurnContext turnContext, string phrase)
		{
			_turnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
			_phrase = phrase ?? string.Empty;
			_cancellationTokenSource = new CancellationTokenSource();

			// Start the background typing task immediately
			_typingTask = SendTypingIndicatorsAsync(_cancellationTokenSource.Token);
		}

		/// <summary>
		/// Stops sending typing indicators and waits for the background task to complete.
		/// </summary>
		public async Task StopAsync()
		{
			// Signal cancellation
			_cancellationTokenSource.Cancel();

			try
			{
				// Wait for the typing task to complete
				await _typingTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
			}
			catch (Exception)
			{
				// Gracefully handle any other exceptions during stop
				// Don't let exceptions during cleanup crash the application
			}
		}

		/// <summary>
		/// Background task that sends typing indicators at regular intervals.
		/// </summary>
		private async Task SendTypingIndicatorsAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					// Send typing activity
					var typingActivity = new Activity
					{
						Type = ActivityTypes.Typing,
						Value = _phrase // Store phrase for potential future UI enhancements
					};

					try
					{
						await _turnContext.SendActivityAsync(typingActivity, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception)
					{
						// Gracefully handle failures to send typing indicator
						// Don't let typing failures crash the main orchestration flow
					}

					// Wait for the next interval
					await Task.Delay(TypingIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
			}
			catch (Exception)
			{
				// Gracefully handle any unexpected exceptions
				// Typing indicator is a non-critical feature
			}
		}

		/// <summary>
		/// Disposes the typing indicator and stops sending activities.
		/// </summary>
		public void Dispose()
		{
			_cancellationTokenSource.Cancel();
			_cancellationTokenSource.Dispose();

			// Best effort wait for task completion
			try
			{
				_typingTask.Wait(TimeSpan.FromSeconds(1));
			}
			catch
			{
				// Ignore exceptions during disposal
			}
		}
	}
}
