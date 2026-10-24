namespace GoldMeridian.Reflection;

partial class EventReference<TTarget, THandler>
{
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

        public static BoundEvent operator +(BoundEvent @event, THandler? handler)
        {
            if (@event.@event is null)
            {
                throw new System.InvalidOperationException("The event binding is uninitialized.");
            }

            @event.@event.Add(@event.target, handler);
            return @event;
        }

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

    public BoundEvent Bind(TTarget? target)
    {
        ValidateTarget(target);
        return new BoundEvent(this, target);
    }
}
