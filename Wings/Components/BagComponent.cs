using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    public interface IBagComponent<TItem> : IComponent<IEnumerable<TItem>>
    {
        void Add(IComponent<TItem> component);
    }

    class BagComponent<TItem> : Component<IEnumerable<TItem>>, IBagComponent<TItem>
    {
        readonly object syncLock = new object();
        readonly List<IComponent<TItem>> components = new List<IComponent<TItem>>();
        IDisposable[] locks;

        public void Add(IComponent<TItem> component)
        {
            if (component == null)
                throw new ArgumentNullException("component");

            if (!this.Send("Freeze"))
                throw new InvalidOperationException("Cannot add component.");

            this.components.Add(component);
            this.Send("Unfreeze");
        }

        protected override void Start()
        {
            Task.WhenAll(components.Select(component => component.StartAndLock()))
                .ContinueWith(t =>
                    {
                        if (t.IsCanceled || t.IsFaulted)
                        {
                            this.Failed(t.Exception);
                        }
                        else
                        {
                            this.locks = t.Result;
                            this.Started(components.Select(ComponentExtensions.GetResult));
                        }
                    });
        }

        protected override void Stop()
        {
            foreach (var @lock in this.locks)
                @lock.Dispose();
            this.Stopped();
        }
    }

}
