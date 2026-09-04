using System;

namespace GoldMeridian.Reflection;

partial class EventReference<TTarget, THandler>
{
    public readonly struct Subscription : IDisposable
    {
        private readonly EventReference<TTarget, THandler> reference;
        private readonly TTarget target;
        private readonly THandler handler;
        
        internal Subscription(EventReference<TTarget, THandler> reference, TTarget target, THandler handler)
        {
            this.reference = reference;
            this.target = target;
            this.handler = handler;
        }
        
        public void Dispose()
        {
            reference.Remove(target, handler);
        }
    }

    public Subscription Subscribe(TTarget target, THandler handler)
    {
        Add(target, handler);
        return new Subscription(this, target, handler);
    }
}
