using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pocket
{
    public enum MessageType
    {
        Join,
        JoinAccepted,
        JoinRejected,
        Leave,
        ParticipantJoined,
        ParticipantLeft,
        AudioReady
    }
}
