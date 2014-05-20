using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    class ActionComponent<TResult> : Component<TResult>
    {
        readonly Func<Task<TResult>> start;
        TResult result;

        public ActionComponent(Func<Task<TResult>> start)
        {
            this.start = start;
        }

        protected override void Start()
        {
            try
            {
                this.start().ContinueWith(t =>
                {
                    if (t.IsCanceled || t.IsFaulted)
                        this.Failed(t.Exception);
                    else
                    {
                        this.result = t.Result;
                        this.Started(t.Result);
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception e)
            {
                this.Failed(e);
            }
        }

        protected override void Stop()
        {
            var disposable = this.result as IDisposable;
            if (disposable != null)
                disposable.Dispose();
            this.result = default(TResult);
            this.Stopped();
        }
    }

}
