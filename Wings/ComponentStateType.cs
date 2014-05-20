using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    public enum ComponentStateType
    {
        Stopped,
        Starting,
        Started,
        Stopping,
        Failed,
        Locked,
        Frozen
    }
}
