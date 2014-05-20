using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    public static class Component
    {
        public static IComponent<TResult> Create<TResult>(Func<Run<TResult>, Task> start)
        {
            return new AsyncActionFuncComponent<TResult>(start);
        }

        public static IComponent<TResult> Create<TResult, TCmp1>(Func<Run<TResult>, TCmp1, Task> start,
            IComponent<TCmp1> cmp1)
        {
            return new ComponentWithDependencies<TResult>(start, cmp1);
        }

        public static IComponent<TResult> Create<TResult, TCmp1, TCmp2>(Func<Run<TResult>, TCmp1, TCmp2, Task> start,
            IComponent<TCmp1> cmp1, IComponent<TCmp2> cmp2)
        {
            return new ComponentWithDependencies<TResult>(start, cmp1, cmp2);
        }

        public static IComponent<TResult> Create<TResult, TCmp1, TCmp2, TCmp3>(Func<Run<TResult>, TCmp1, TCmp2, TCmp3, Task> start,
            IComponent<TCmp1> cmp1, IComponent<TCmp2> cmp2, IComponent<TCmp3> cmp3)
        {
            return new ComponentWithDependencies<TResult>(start, cmp1, cmp2, cmp3);
        }

        public static IComponent<TResult> Create<TResult, TCmp1, TCmp2, TCmp3, TCmp4>(Func<Run<TResult>, TCmp1, TCmp2, TCmp3, TCmp4, Task> start,
            IComponent<TCmp1> cmp1, IComponent<TCmp2> cmp2, IComponent<TCmp3> cmp3, IComponent<TCmp4> cmp4)
        {
            return new ComponentWithDependencies<TResult>(start, cmp1, cmp2, cmp3, cmp4);
        }

        public static IComponent<TResult> Create<TResult, TCmp1, TCmp2, TCmp3, TCmp4, TCmp5>(Func<Run<TResult>, TCmp1, TCmp2, TCmp3, TCmp4, TCmp5, Task> start,
            IComponent<TCmp1> cmp1, IComponent<TCmp2> cmp2, IComponent<TCmp3> cmp3, IComponent<TCmp4> cmp4, IComponent<TCmp5> cmp5)
        {
            return new ComponentWithDependencies<TResult>(start, cmp1, cmp2, cmp3, cmp4, cmp5);
        }

        public interface IFacadeBuilder<TResult>
        {
            IFacadeBuilder<TResult> Set<TComponent>(IComponent<TComponent> component, Expression<Func<TResult, TComponent>> property);
        }

        class FacadeBuilder<TResult> : IFacadeBuilder<TResult>, IDisposable
        {
            readonly FacadeComponent<TResult> facade;
            bool disposed;

            public FacadeBuilder(FacadeComponent<TResult> facade)
            {
                this.facade = facade;
            }

            public IFacadeBuilder<TResult> Set<TComponent>(IComponent<TComponent> component, Expression<Func<TResult, TComponent>> property)
            {
                if (disposed)
                    throw new ObjectDisposedException("FacadeBuilder");
                this.facade.Add(component, property);
                return this;
            }

            public void Dispose()
            {
                this.disposed = true;
            }
        }

        public static IComponent<TResult> CreateFacade<TResult>(Action<IFacadeBuilder<TResult>> build)
        {
            var facade = new FacadeComponent<TResult>();
            using (var builder = new FacadeBuilder<TResult>(facade))
                build(builder);
            return facade;
        }

        public static IBagComponent<TResult> CreateBag<TResult>(params IComponent<TResult>[] components)
        {
            var bag = new BagComponent<TResult>();
            foreach (var component in components)
                bag.Add(component);
            return bag;
        }

        public static IProxyComponent<TResult> CreateProxy<TResult>()
        {
            return CreateProxy<TResult>(null);
        }

        public static IProxyComponent<TResult> CreateProxy<TResult>(IComponent<TResult> target)
        {
            var proxy = new ProxyCompnent<TResult>();
            if (target != null)
                proxy.Attach(target);
            return proxy;
        }
    }
}
