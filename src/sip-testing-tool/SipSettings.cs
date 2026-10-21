using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clc.Sip
{
    public class SipSettings
    {
        public string server { get; set; }
        public string port { get; set; }
        public string ao { get; set; }
        public string ap { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string vendorProfile { get; set; }
        public string patronBarcode { get; set; }
        public string patronPIN { get; set; }
        public string bp { get; set; }
        public string bq { get; set; }
        public string itemBarcode { get; set; }

    }
}
