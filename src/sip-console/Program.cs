using Clc.Sip;
using sip_console.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace sip_console
{
    class Program
    {
        static void Main(string[] args)
        {
            var settings = CustomSettings.Settings;
            var results = new Dictionary<string, int>();

            foreach(ServerElement server in settings.SipServers)
            {
                try
                {
                    results.Add(server.Hostname, CheckSIPServer(server.Hostname, server.Port, server.AO, server.Username, server.Password));
                }
                catch
                {
                    results.Add(server.Hostname, 0);
                }
            }

            var prtgElement = new XElement("prtg");

            foreach(var result in results)
            {
                var resultElement = new XElement("result");
                resultElement.Add(new XElement("channel", result.Key));
                resultElement.Add(new XElement("value", result.Value));
                resultElement.Add(new XElement("ValueLookup", settings.ValueLookupName));

                prtgElement.Add(resultElement);
            }

            XDocument doc = new XDocument(prtgElement);
                        
            Console.WriteLine(doc.ToString());
            Console.Read();
        }

        static int CheckSIPServer(string hostname, int port, int ao, string username, string password)
        {
            var sip = new sip2();
            sip.hostname = hostname;
            sip.port = port;
            sip.AO = ao.ToString();

            sip.Connect();
            var login = sip.Login(username, password);
            var status = sip.Custom("990 802.00");

            return Convert.ToInt32(status.Response.ToLower().Contains("system status ok"));
        }
    }
}
