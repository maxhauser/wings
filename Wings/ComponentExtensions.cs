using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace Wings
{
    public static class ComponentExtensions
    {
        public static IDisposable Lock(this IStateMachine<IComponentState> component)
        {
            if (!component.Send(ComponentMessages.Lock))
                throw new InvalidOperationException();
            return Disposable.Create(() => component.Send(ComponentMessages.Unlock));
        }

        public static async Task Start(this IStateMachine<IComponentState> component)
        {
            if (!component.Send(ComponentMessages.Start))
                throw new InvalidOperationException("Cannot start component in current state.");
            await component.FirstAsync(s => s.Type == ComponentStateType.Started);
        }

        public static async Task<TResult> Start<TResult>(this IComponent<TResult> component)
        {
            if (!component.Send(ComponentMessages.Start))
                throw new InvalidOperationException("Cannot start component in current state.");
            await component.FirstAsync(s => s.Type == ComponentStateType.Started);
            return component.GetResult();
        }

        public static async Task<IDisposable> StartAndLock(this IStateMachine<IComponentState> component)
        {
            while (true)
            {
                // first try to lock
                if (component.Send(ComponentMessages.Lock))
                    return Disposable.Create(delegate { component.Send(ComponentMessages.Unlock); });

                if (component.Send(ComponentMessages.Start))
                    await component.FirstAsync(s => s.Type == ComponentStateType.Started || s.Type == ComponentStateType.Failed);
                else
                    await component.FirstAsync(s => s.Type == ComponentStateType.Stopped || s.Type == ComponentStateType.Failed);

                if (component.State.Type == ComponentStateType.Failed)
                    throw ((FailedComponentState)component.State).Exception;
            }
        }

        public static async Task<IDisposable> StartAndLock(this IStateMachine<IComponentState> component, CancellationToken cancellationToken)
        {
            while (true)
            {
                // first try to lock
                if (component.Send(ComponentMessages.Lock))
                    return Disposable.Create(delegate { component.Send(ComponentMessages.Unlock); });

                if (component.Send(ComponentMessages.Start))
                    await component.FirstAsync(s => s.Type == ComponentStateType.Started || s.Type == ComponentStateType.Failed).ToTask(cancellationToken);
                else
                    await component.FirstAsync(s => s.Type == ComponentStateType.Stopped || s.Type == ComponentStateType.Failed).ToTask(cancellationToken);

                if (component.State.Type == ComponentStateType.Failed)
                    throw ((FailedComponentState)component.State).Exception;
            }
        }

        public static async Task Restart(this IStateMachine<IComponentState> component)
        {
            if (!component.Send(ComponentMessages.Stop))
                throw new InvalidOperationException("Component not started.");
            await component.FirstAsync(s => s.Type == ComponentStateType.Stopped);
            if (!component.Send(ComponentMessages.Start))
                throw new InvalidOperationException("Cannot start component.");
            await component.FirstAsync(s => s.Type == ComponentStateType.Started);
        }

        public static TResult GetResult<TResult>(this IComponent<TResult> component)
        {
            var state = component.State as IHasResult<TResult>;
            if (state == null)
                throw new InvalidOperationException("Component not started.");
            return state.Result;
        }

        public static async Task WhenStopped(this IStateMachine<IComponentState> component)
        {
            await component.FirstAsync(e => e.Type == ComponentStateType.Stopped);
        }

        public static Task WhenStopped(this IStateMachine<IComponentState> component, CancellationToken token)
        {
            return component.FirstAsync(e => e.Type == ComponentStateType.Stopped).ToTask(token);
        }

        public static async Task WhenStarted(this IStateMachine<IComponentState> component)
        {
            await component.FirstAsync(e => e.Type == ComponentStateType.Started);
        }

        public static Task WhenStarted(this IStateMachine<IComponentState> component, CancellationToken token)
        {
            return component.FirstAsync(e => e.Type == ComponentStateType.Started).ToTask(token);
        }

        public static async Task Stop(this IStateMachine<IComponentState> component)
        {
            if (!component.Send(ComponentMessages.Stop))
                throw new InvalidOperationException("Cannot stop component.");
            await component.FirstAsync(e => e.Type == ComponentStateType.Stopped);
        }
    }
}
