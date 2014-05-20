using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    class ComponentWithDependencies<TResult> : AsyncActionComponent<TResult>
    {
        readonly Delegate start;
        readonly IStateMachine<IComponentState>[] components;

        public ComponentWithDependencies(Delegate start, params IStateMachine<IComponentState>[] components)
        {
            var args = start.Method.GetParameters();
            if (args.Length != components.Length + 1)
                throw new InvalidOperationException("arguments do not match");

            this.start = start;
            this.components = components;
        }

        protected override async Task StartCore(Run<TResult> run)
        {
            using (new CompositeDisposable(await Task.WhenAll(components.Select(component => component.StartAndLock()))))
            {
                var args = new object[components.Length + 1];
                args[0] = run;
                for (int i = 0; i < components.Length; i++)
                {
                    var state = (IHasResult)components[i].State;
                    args[i + 1] = state.Result;
                }

                await (Task)start.DynamicInvoke(args);
            }
        }
    }
}
