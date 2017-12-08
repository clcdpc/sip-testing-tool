using System;
using System.Collections.Generic;
using System.Text;

namespace sip_net_core
{
    public enum NotificationType
    {
        AnyOrUnknown = 000,
        Overdue = 001,
        Recall = 002,
        Hold = 003,
        FineOrBilling = 004
    }
}
