using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;

namespace Wings
{
    public interface IProxyComponent<TResult> : IComponent<TResult>
    {
        void Attach(IComponent<TResult> component);
    }

    class ProxyCompnent<TResult> : IProxyComponent<TResult>
    {
        readonly object syncLock = new object();
        readonly ISubject<IComponentState> subject = new BehaviorSubject<IComponentState>(new StoppedComponentState());

        IComponent<TResult> component;
        IDisposable subscription;

        public void Attach(IComponent<TResult> component)
        {
            if (component == null)
                throw new ArgumentNullException("component");

            lock (this.syncLock)
            {
                if (!component.Send(ComponentMessages.Freeze))
                    throw new InvalidOperationException("Component has invalid state.");

                try
                {
                    if (this.subscription != null)
                        this.subscription.Dispose();

                    this.component = component;
                    this.subscription = component.Skip(2).Subscribe(this.subject); // skip current (frozen) and next (stopped)
                }
                finally
                {
                    component.Send(ComponentMessages.Unfreeze);
                }
            }
        }

        public IComponentState State
        {
            get { return this.component == null ? new StoppedComponentState() : this.component.State; }
        }

        public bool Send(string message)
        {
            if (this.component == null)
                return false;

            return this.component.Send(message);
        }

        public IDisposable Subscribe(IObserver<IComponentState> observer)
        {
            return this.subject.Subscribe(observer);
        }
    }
}
