namespace GoldMeridian.Reflection;

partial class EventReference<TTarget, THandler>
{
    public readonly ref struct BoundEvent
    {
        private readonly EventReference<TTarget, THandler> @event;
        private readonly TTarget? target;

        internal BoundEvent(
            EventReference<TTarget, THandler> @event,
            TTarget? target
        )
        {
            this.@event = @event;
            this.target = target;
        }

        public static BoundEvent operator +(BoundEvent @event, THandler handler)
        {
            @event.@event.Add(@event.target, handler);
            return @event;
        }

        public static BoundEvent operator -(BoundEvent @event, THandler handler)
        {
            @event.@event.Remove(@event.target, handler);
            return @event;
        }
    }

    public BoundEvent Bind(TTarget? target)
    {
        return new BoundEvent(this, target);
    }
}
