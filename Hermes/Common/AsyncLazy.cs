namespace Hermes.Common
{
    /// <summary>
    /// Provides lazy initialization for asynchronous operations with thread-safe single execution guarantee.
    /// Useful for caching async operations that should only execute once even with concurrent access.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public class AsyncLazy<T>
    {
        private readonly Lazy<Task<T>> _instance;

        /// <summary>
        /// Initializes a new instance of the AsyncLazy class with the specified asynchronous factory function.
        /// </summary>
        /// <param name="factory">The asynchronous factory function that produces the value.</param>
        public AsyncLazy(Func<Task<T>> factory)
        {
            _instance = new Lazy<Task<T>>(() => Task.Run(factory));
        }

        /// <summary>
        /// Gets the lazily initialized value.
        /// </summary>
        public Task<T> Value => _instance.Value;

        /// <summary>
        /// Gets a value indicating whether the value has been created.
        /// </summary>
        public bool IsValueCreated => _instance.IsValueCreated;
    }
}
