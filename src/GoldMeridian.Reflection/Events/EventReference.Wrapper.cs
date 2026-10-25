namespace GoldMeridian.Reflection;

partial class EventReference<TTarget, THandler> : IEventAccessors<THandler>
{
    public EventWrapper<THandler> Wrap(TTarget? target)
    {
        ValidateTarget(target);
        return new EventWrapper<THandler>(this, IsStatic ? null : target);
    }

    void IEventAccessors<THandler>.Add(object? target, THandler? handler)
    {
        Add(IsStatic ? default(TTarget?) : (TTarget?)target, handler);
    }

    void IEventAccessors<THandler>.Remove(object? target, THandler? handler)
    {
        Remove(IsStatic ? default(TTarget?) : (TTarget?)target, handler);
    }
}
