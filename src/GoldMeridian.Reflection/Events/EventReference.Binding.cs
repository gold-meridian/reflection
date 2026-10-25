namespace GoldMeridian.Reflection;

partial class EventReference<TTarget, THandler>
{
    /// <summary>
    ///     An event bound to a target, supporting compound addition and
    ///     subtraction syntax.
    /// </summary>
    /// <remarks>
    ///     Create a binding with <see cref="Bind"/> before using it.
    /// </remarks>
    public readonly struct BoundEvent
    {
        private readonly EventReference<TTarget, THandler>? @event;
        private readonly TTarget? target;

        internal BoundEvent(
            EventReference<TTarget, THandler> @event,
            TTarget? target
        )
        {
            this.@event = @event;
            this.target = target;
        }

        /// <summary>
        ///     Adds a handler to the bound event and returns the binding.
        /// </summary>
        public static BoundEvent operator +(BoundEvent @event, THandler? handler)
        {
            if (@event.@event is null)
            {
                throw new System.InvalidOperationException("The event binding is uninitialized.");
            }

            @event.@event.Add(@event.target, handler);
            return @event;
        }

        /// <summary>
        ///     Removes a handler from the bound event and returns the binding.
        /// </summary>
        public static BoundEvent operator -(BoundEvent @event, THandler? handler)
        {
            if (@event.@event is null)
            {
                throw new System.InvalidOperationException("The event binding is uninitialized.");
            }

            @event.@event.Remove(@event.target, handler);
            return @event;
        }
    }

    /// <summary>
    ///     Binds the event to a target for use with <c>+=</c> and <c>-=</c>.
    /// </summary>
    /// <param name="target">
    ///     The event source.  Ignored for static events.
    /// </param>
    public BoundEvent Bind(TTarget? target)
    {
        ValidateTarget(target);
        return new BoundEvent(this, target);
    }
}
