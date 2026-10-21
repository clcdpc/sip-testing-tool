using System;
using System.Collections.Generic;
using System.Text;

namespace Clc.Sip
{
    public class SipTransaction
    {
        public string Message { get; set; }
        public string Response { get; set; }
        public bool ChecksumValid { get; set; } = true;
        public long ResponseTime { get; set; }
        public bool ConnectionReset { get; set; }
        public override string ToString()
        {
            return string.Format("--> {0} \r\n<-- {1}\r\n", Message, Response);
        }
    }
}
