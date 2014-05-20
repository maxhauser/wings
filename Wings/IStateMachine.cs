using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    public interface IStateMachine<out TState> : IObservable<TState>
    {
        TState State { get; }
        bool Send(string @message);
    }
}
