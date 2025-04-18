using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIMCOMVoiceDialer
{
    public enum CallForwardReason
    {
        Unconditional = 0,
        Busy = 1,
        NoReply = 2,
        NotReachable = 3,
        AllForwarding = 4,
        AllConditional = 5
    }
}
