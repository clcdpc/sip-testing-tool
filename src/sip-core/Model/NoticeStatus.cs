using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Clc.Sip
{
    public enum NoticeStatus
    {
        UnsuccessfulDelivery = 00,
        SuccessfulDelivery = 01,
        InterceptedRejected = 02
    }
}
