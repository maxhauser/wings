using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    public delegate RunnerAwaitable Run<TResult>(TResult result);

    class AsyncActionFuncComponent<TResult> : AsyncActionComponent<TResult>
    {
        readonly Func<Run<TResult>, Task> start;
        public AsyncActionFuncComponent(Func<Run<TResult>, Task> start)
        {
            this.start = start;
        }

        protected override Task StartCore(Run<TResult> run)
        {
            return this.start(run);
        }
    }

    public interface RunnerAwaitable
    {
        RunnerAwaiter GetAwaiter();
    }

    public interface RunnerAwaiter : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }
        void GetResult();
    }

    abstract class AsyncActionComponent<TResult> : Component<TResult>
    {
        Action stop;

        void OnTaskCompleted(Task task)
        {
            if (task.IsCanceled || task.IsFaulted)
                this.Failed(task.Exception);
            else
                this.Stopped();
        }

        protected abstract Task StartCore(Run<TResult> run);

        protected sealed override void Start()
        {
            try
            {
                var runner = new Runner((result, continuation) =>
                {
                    this.stop = continuation;
                    this.Started(result);
                });
                StartCore(runner.Run).ContinueWith(this.OnTaskCompleted, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception e)
            {
                this.Failed(e);
            }
        }

        protected override void Stop()
        {
            this.stop();
        }

        class Runner
        {
            readonly Action<TResult, Action> onAwait;
            TResult result;

            public Runner(Action<TResult, Action> onAwait)
            {
                this.onAwait = onAwait;
            }

            public Awaitable Run(TResult result)
            {
                this.result = result;
                return new Awaitable(this);
            }

            void OnCompleted(Action continuation)
            {
                this.onAwait(result, continuation);
            }

            public class Awaitable : RunnerAwaitable, RunnerAwaiter
            {
                readonly Runner runner;

                internal Awaitable(Runner runner)
                {
                    this.runner = runner;
                }

                public RunnerAwaiter GetAwaiter()
                {
                    return this;
                }

                public void OnCompleted(Action continuation)
                {
                    this.runner.OnCompleted(delegate
                    {
                        this.IsCompleted = true;
                        continuation();
                    });
                }

                public void UnsafeOnCompleted(Action continuation)
                {
                    this.OnCompleted(continuation);
                }

                public bool IsCompleted { get; private set; }
                public void GetResult() { }
            }
        }
    }
}
