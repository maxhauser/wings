using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Wings.Components
{
    class IsolatedComponent<TResult> : MarshalByRefObject, IComponent<TResult>
    {
        readonly object gate = new object();
        readonly Func<IComparable<TResult>> rootComponentFactory;
        readonly ISubject<IComponentState> subject = new BehaviorSubject<IComponentState>(new StoppedComponentState());
        IComponent<TResult> remotedComponent;

        public IsolatedComponent(Func<IComparable<TResult>> rootComponentFactory)
        {
            if (rootComponentFactory == null)
                throw new ArgumentNullException("component");

            this.rootComponentFactory = rootComponentFactory;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public IComponentState State
        {
            get
            {
                lock (this.gate)
                {
                    if (this.started)
                        return this.remotedComponent.State;
                    else
                        return this.state;
                }
            }
        }

        bool started;
        IComponentState state = new StoppedComponentState();

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
                if (started)
                    return this.remotedComponent.Send(message);

                switch (this.state.Type)
                {
                    case ComponentStateType.Frozen:
                        {
                            var frozen = (FrozenComponentState)this.state;
                            if (message == ComponentMessages.Freeze)
                            {
                                frozen.IncrementLevel();
                                return true;
                            }
                            else if (message == ComponentMessages.Unfreeze)
                            {
                                if (frozen.DecrementLevel() == 0)
                                    SetState(new StoppedComponentState());
                                return true;
                            }
                        }
                        break;

                    case ComponentStateType.Stopped:
                        {
                            if (message == ComponentMessages.Freeze)
                            {
                                SetState(new FrozenComponentState());
                                return true;
                            }
                            else if (message == ComponentMessages.Start)
                            {
                                SetState(new StartingComponentState());
                                Start();
                                return true;
                            }
                        }
                        break;

                }
                return false;
            }
        }

        AppDomain appDomain;
        private async Task Start()
        {
            try
            {
                this.appDomain = AppDomain.CreateDomain("component domain");
            }
            catch (Exception exception)
            {
                if (this.appDomain != null)
                    AppDomain.Unload(this.appDomain);
                Failed(exception);
            }
        }

        void Failed(Exception exception)
        {
            lock (this.gate)
            {
                SetState(new FailedComponentState(exception));
            }
        }

        public IDisposable Subscribe(IObserver<IComponentState> observer)
        {
            return this.subject.Subscribe(observer);
        }
    }
}
