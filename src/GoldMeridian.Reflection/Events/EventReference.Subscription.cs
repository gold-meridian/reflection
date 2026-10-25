using System;
using System.Threading;

namespace GoldMeridian.Reflection;

partial class EventReference<TTarget, THandler>
{
    // Split between Subscription scope and real state so copies and concurrency
    // don't result in attempts to dispose of Subscription twice.
    /// <summary>
    ///     An event subscription removed on disposal.  Use with
    ///     <see langword="using"/> to scope a handler's lifetime.
    /// </summary>
    public readonly struct Subscription : IDisposable
    {
        private readonly SubscriptionState? state;

        internal Subscription(EventReference<TTarget, THandler> reference, TTarget? target, THandler? handler)
        {
            state = new SubscriptionState(reference, target, handler);
        }

        /// <summary>
        ///     Removes the handler once.  Repeated disposal, including through
        ///     copies, has no effect.
        /// </summary>
        public void Dispose()
        {
            state?.Dispose();
        }
    }

    private sealed class SubscriptionState(EventReference<TTarget, THandler> reference, TTarget? target, THandler? handler)
    {
        private EventReference<TTarget, THandler>? reference = reference;
        private TTarget? target = target;
        private THandler? handler = handler;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref reference, null);
            if (owner is null)
            {
                return;
            }

            try
            {
                owner.Remove(target, handler);
            }
            finally
            {
                target = default(TTarget?);
                handler = null;
            }
        }
    }

    /// <summary>
    ///     Adds a handler and returns a subscription that removes it on
    ///     disposal. Use with <see langword="using"/>.
    /// </summary>
    /// <inheritdoc cref="Add" path="/param"/>
    public Subscription Subscribe(TTarget? target, THandler? handler)
    {
        if (!CanAdd || !CanRemove)
        {
            throw new InvalidOperationException($"Event '{Info.Name}' requires both accessors for a disposable subscription.");
        }

        var subscription = new Subscription(this, target, handler);
        {
            Add(target, handler);
        }
        return subscription;
    }
}
