using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wings
{
    public interface IComponentState
    {
        ComponentStateType Type { get; }
    }

    public interface IHasResult<TResult>
    {
        TResult Result { get; }
    }

    interface IHasResult
    {
        object Result { get; }
    }

    [Serializable]
    public struct StartedComponentState<TResult> : IComponentState, IHasResult<TResult>, IHasResult
    {
        readonly TResult result;
        internal StartedComponentState(TResult result) { this.result = result; }
        public ComponentStateType Type { get { return ComponentStateType.Started; } }
        public TResult Result { get { return this.result; } }
        object IHasResult.Result { get { return this.result; } }
    }

    [Serializable]
    public struct StartingComponentState : IComponentState
    {
        public ComponentStateType Type { get { return ComponentStateType.Starting; } }
    }

    [Serializable]
    public struct StoppedComponentState : IComponentState
    {
        public ComponentStateType Type { get { return ComponentStateType.Stopped; } }
    }

    [Serializable]
    public struct StoppingComponentState : IComponentState
    {
        public ComponentStateType Type { get { return ComponentStateType.Stopping; } }
    }

    [Serializable]
    public struct FailedComponentState : IComponentState
    {
        readonly Exception exception;
        internal FailedComponentState(Exception exception) { this.exception = exception; }
        public ComponentStateType Type { get { return ComponentStateType.Failed; } }
        public Exception Exception { get { return this.exception; } }
    }

    public abstract class LeveledComponentState : IComponentState
    {
        [NonSerialized]
        int level;

        public LeveledComponentState()
        {
            this.level = 1;
        }

        public abstract ComponentStateType Type { get; }

        internal int IncrementLevel()
        {
            this.level++;
            return this.level;
        }

        internal int DecrementLevel()
        {
            this.level--;
            return this.level;
        }
    }

    [Serializable]
    public class LockedComponentState<TResult> : LeveledComponentState, IHasResult<TResult>, IHasResult
    {
        internal LockedComponentState(StartedComponentState<TResult> startedState)
        {
            this.StartedState = startedState;
        }

        public override ComponentStateType Type { get { return ComponentStateType.Locked; } }
        public TResult Result { get { return StartedState.Result; } }
        internal StartedComponentState<TResult> StartedState { get; private set; }
        object IHasResult.Result { get { return StartedState.Result; } }
    }

    [Serializable]
    public class FrozenComponentState : LeveledComponentState
    {
        public override ComponentStateType Type { get { return ComponentStateType.Frozen; } }
    }
}
