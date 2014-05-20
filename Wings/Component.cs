using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wings
{
    public static class ComponentMessages
    {
        public const string Start = "Start";
        public const string Stop = "Stop";
        public const string Freeze = "Freeze";
        public const string Unfreeze = "Unfreeze";
        public const string Lock = "Lock";
        public const string Unlock = "Unlock";
    }

    public interface IComponent<out TResult> : IStateMachine<IComponentState> { }

    abstract class Component<TResult> : IComponent<TResult>
    {
        readonly object gate = new object();
        readonly ISubject<IComponentState> subject = new BehaviorSubject<IComponentState>(new StoppedComponentState());
        IComponentState state = new StoppedComponentState();

        public IComponentState State
        {
            get { return this.state; }
        }

        void SetState(IComponentState state)
        {
            this.state = state;
            if (state.Type != ComponentStateType.Failed)
                this.subject.OnNext(state);
            else
                this.subject.OnError(((FailedComponentState)state).Exception);
        }

        public bool Send(string message)
        {
            lock (this.gate)
            {
                switch (this.state.Type)
                {
                    case ComponentStateType.Stopped:
                        if (message == ComponentMessages.Start)
                        {
                            SetState(new StartingComponentState());
                            Guard(Start);
                            return true;
                        } if (message == ComponentMessages.Freeze)
                        {
                            SetState(new FrozenComponentState());
                            return true;
                        }
                        break;

                    case ComponentStateType.Started:
                        if (message == ComponentMessages.Stop)
                        {
                            SetState(new StoppingComponentState());
                            Guard(Stop);
                            return true;
                        }
                        else if (message == ComponentMessages.Lock)
                        {
                            SetState(new LockedComponentState<TResult>((StartedComponentState<TResult>)this.state));
                            return true;
                        };
                        break;

                    case ComponentStateType.Locked:
                        if (message == ComponentMessages.Unlock)
                        {
                            var lockedState = (LockedComponentState<TResult>)this.state;
                            if (lockedState.DecrementLevel() == 0)
                                SetState(((LockedComponentState<TResult>)this.state).StartedState);
                            return true;
                        }
                        else if (message == ComponentMessages.Lock)
                        {
                            ((LeveledComponentState)this.state).IncrementLevel();
                            return true;
                        }
                        break;

                    case ComponentStateType.Frozen:
                        if (message == ComponentMessages.Unfreeze)
                        {
                            var frozenState = (FrozenComponentState)this.state;
                            if (frozenState.DecrementLevel() == 0)
                                SetState(new StoppedComponentState());
                            return true;
                        }
                        else if (message == ComponentMessages.Freeze)
                        {
                            ((FrozenComponentState)this.state).IncrementLevel();
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Failed(exception);
            }
        }

        protected void Failed(Exception exception)
        {
            lock (this.gate)
            {
                SetState(new FailedComponentState(exception));
            }
        }

        protected void Started(TResult result)
        {
            lock (this.gate)
            {
                if (this.state.Type != ComponentStateType.Starting)
                    throw new InvalidOperationException();
                SetState(new StartedComponentState<TResult>(result));
            }
        }

        protected void Stopped()
        {
            lock (this.gate)
            {
                if (this.state.Type != ComponentStateType.Stopping)
                    throw new InvalidOperationException();
                SetState(new StoppedComponentState());
            }
        }

        protected abstract void Start();

        protected virtual void Stop()
        {
            Stopped();
        }

        public IDisposable Subscribe(IObserver<IComponentState> observer)
        {
            return this.subject.Subscribe(observer);
        }
    }
}
