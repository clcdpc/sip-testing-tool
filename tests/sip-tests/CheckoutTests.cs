using System.Net;
using System.Net.Sockets;
using System.Text;
using Clc.Sip.Tests.TestSupport;

namespace Clc.Sip.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class CheckoutTests
{
    [TestMethod(DisplayName = "Checkout fields and compatible defaults over TCP")]
    public void CheckoutSerializesOptionsAndCompatibleDefaults()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(() =>
        {
            using var peer = listener.AcceptTcpClient();
            var requests = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                requests.Add(SipRequest.ReadFrom(peer));
                peer.GetStream().Write(Encoding.ASCII.GetBytes("98YAY0AZFDD1\r"));
            }
            return requests;
        });
        using var client = new sip2("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "test", "desk");
        client.Connect();
        _ = client.ItemCheckOut("patron", "pin", "item");
        _ = client.ItemCheckOut("patron", "pin", "item", new DateTime(2026, 10, 28, 14, 30, 45),
            renewalOk: "N", itemProperties: "test-properties", feeAcknowledged: "Y", noBlock: "Y", cancel: "Y", terminalPassword: "terminal-secret");
        _ = client.ItemCheckOut("patron", "pin", "item", cancel: "N");
        _ = client.ItemCheckOut("patron", "pin", "item", feeAcknowledged: "");
        var sent = server.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.AreEqual("11YN", sent[0][..4]);
        Assert.AreEqual(new string(' ', 18), sent[0].Substring(22, 18));
        Assert.AreEqual("AOtest|AApatron|ABitem|AC|ADpin|BON|", SipRequest.VariableFields(sent[0], fixedLength: 40));
        Assert.AreEqual("11NY", sent[1][..4]);
        Assert.AreEqual("20261028    143045", sent[1].Substring(22, 18));
        Assert.AreEqual("AOtest|AApatron|ABitem|ACterminal-secret|ADpin|CHtest-properties|BOY|BIY|", SipRequest.VariableFields(sent[1], fixedLength: 40));
        Assert.AreEqual("AOtest|AApatron|ABitem|AC|ADpin|BON|BIN|", SipRequest.VariableFields(sent[2], fixedLength: 40));
        Assert.AreEqual("AOtest|AApatron|ABitem|AC|ADpin|", SipRequest.VariableFields(sent[3], fixedLength: 40));
        foreach (var message in sent)
        {
            Assert.IsTrue(client.CheckChecksum(message), "Checkout options broke the request checksum.");
        }
    }
}
