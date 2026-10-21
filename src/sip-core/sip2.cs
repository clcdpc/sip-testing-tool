using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Clc.Sip
{
    public class sip2 : IDisposable
    {
        public Encoding TextEncoding { get; set; } = Encoding.UTF8;
        public bool ValidateResponseChecksum { get; set; } = true;

        public string hostname = "";
        public int port = 5002;

        public string language = "000";

        public string AP = "";
        public string AO = "";
        public string VP = "";
        public string CP = "";

        string AC = "";
        string noBlock = "N";

        StringBuilder messageBuilder = new StringBuilder();
        bool _noFixed = false;

        // inject socket from gui
        TcpClient socket = new TcpClient();

        private const char FieldTerminator = '|';
        private const char MessageTerminator = '\r';

        int _seq = -1;

        public sip2()
        {
        }

        public sip2(string hostname, int port) : this(hostname, port, "", "")
        {
        }

        public sip2(string hostname, int port, string AO, string AP)
        {
            this.hostname = hostname;
            this.port = port;
            this.AO = AO;
            this.AP = AP;
        }

        public void AddVariableField(string identifier, string value, bool optional = false)
        {
            if (optional && value == "") return;
            messageBuilder.Append(identifier + value + FieldTerminator);
        }

        public void AddVariableField(string identifier, int value, bool optional = false)
        {
            AddVariableField(identifier, value.ToString(), optional);
        }

        public void AddFixedField(string value, int length)
        {
            if (_noFixed) return;
            messageBuilder.Append(value.PadRight(length));
        }

        public void NewMessage(string code)
        {
            messageBuilder.Clear();
            messageBuilder.Append(code);
            _noFixed = false;
        }

        public void Connect()
        {
            socket.Connect(hostname, port);
        }

        public SipTransaction Login(string sipUsername, string sipPassword, string locationCode = "", string vendorProfile = "")
        {
            this.VP = vendorProfile;
            this.CP = locationCode;
            NewMessage("93");
            AddFixedField("0", 1);
            AddFixedField("0", 1);
            AddVariableField("CN", sipUsername);
            AddVariableField("CO", sipPassword);
            AddVariableField("CP", locationCode);
            AddVariableField("VP", VP, true);
            return SendMessage();
        }

        public SipTransaction EndSession(string patronBarcode, string patronPIN)
        {
            NewMessage("35");
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AC", AC, true);
            AddVariableField("AD", patronPIN, true);

            return SendMessage();
        }

        public SipTransaction PatronStatus(string patronBarcode, string patronPIN)
        {
            NewMessage("23");
            AddFixedField(language, 3);
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AC", AC);
            AddVariableField("AD", patronPIN, true);

            return SendMessage();
        }

        public SipTransaction PatronInformation(string patronBarcode, string patronPIN, string type = "none", string BP = "1", string BQ = "5")
        {
            var summary = new Dictionary<string, string>();
            summary["none"] = "      ";
            summary["hold"] = "Y     ";
            summary["overdue"] = " Y    ";
            summary["charged"] = "  Y   ";
            summary["fine"] = "   Y  ";
            summary["recall"] = "    Y ";
            summary["unavail"] = "     Y";

            NewMessage("63");
            AddFixedField(language, 3);
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddFixedField(summary[type], 10);
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AC", AC, true);
            AddVariableField("AD", patronPIN, true);
            AddVariableField("BP", BP, true);
            AddVariableField("BQ", BQ, true);

            return SendMessage();
        }

        public SipTransaction ItemCheckOut(string patronBarcode, string patronPIN, string itemBarcode, DateTime? dueDate = null, string renewalOk = "Y", string itemProperties = "", string feeAcknowledged = "N", string noBlock = "N", string cancel = "N")
        {
            NewMessage("11");
            AddFixedField(renewalOk, 1);
            AddFixedField(this.noBlock, 1);
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddFixedField(dueDate.HasValue ? dueDate.Value.ToSipString() : "", 18);
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AB", itemBarcode);
            AddVariableField("AC", AC);
            AddVariableField("AD", patronPIN);
            AddVariableField("CH", itemProperties, true);
            AddVariableField("BO", feeAcknowledged, true);
            //AddVariableField("BI", cancel, true);

            return SendMessage();
        }

        public SipTransaction ItemInformation(string itemBarcode)
        {
            NewMessage("17");
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddVariableField("AO", AO);
            AddVariableField("AB", itemBarcode);
            AddVariableField("AC", AC);

            return SendMessage();
        }

        public SipTransaction Renew(string patronBarcode, string patronPIN, DateTime dueDate, string itemBarcode, string title = "", string itemProperties = "", string thirdPartyAllowed = "Y", string noBlock = "Y", string feeAcknowledged = "")
        {
            NewMessage("29");
            AddFixedField(thirdPartyAllowed, 1);
            AddFixedField(this.noBlock, 1);
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddFixedField(dueDate.ToSipString(), 18);
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AD", patronPIN, true);
            AddVariableField("AB", itemBarcode, true);
            AddVariableField("AJ", title, true);
            AddVariableField("AC", AC, true);
            AddVariableField("CH", itemProperties, true);
            AddVariableField("BO", feeAcknowledged, true);

            return SendMessage();
        }

        public SipTransaction RenewAll(string patronBarcode, string patronPIN, string feeAcknowledged = "")
        {
            NewMessage("65");
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AD", patronPIN);
            AddVariableField("AC", AC, true);
            AddVariableField("BO", feeAcknowledged, true);

            return SendMessage();
        }

        public SipTransaction ItemCheckIn(string itemBarcode, DateTime? returnDate = null, string returnLocation = "", string itemProperties = "", string noBlock = "N", string cancel = "")
        {
            NewMessage("09");
            AddFixedField(this.noBlock, 1);
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddFixedField(returnDate.HasValue ? returnDate.Value.ToSipString() : DateTime.Now.ToSipString(), 18);
            AddVariableField("AP", returnLocation == "" ? AP : returnLocation);
            AddVariableField("AO", AO);
            AddVariableField("AB", itemBarcode);
            AddVariableField("AC", AC);
            AddVariableField("CH", itemProperties, true);
            AddVariableField("BI", cancel, true);

            return SendMessage();
        }

        public SipTransaction FeePaid(int feeType, int paymentType, double feeAmount, string patronBarcode, string patronPIN, string currencyType = "USD", string feeIdentifier = "", string transactionId = "")
        {
            NewMessage("37");
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddFixedField(feeType.ToString("00"), 2);
            AddFixedField(paymentType.ToString("00"), 2);
            AddFixedField(currencyType, 3);
            AddVariableField("BV", feeAmount.ToString());
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AD", patronPIN, true);
            AddVariableField("AC", AC, true);
            AddVariableField("CG", feeIdentifier, true);
            AddVariableField("BK", transactionId, true);

            return SendMessage();
        }

        public SipTransaction NotificationUpdate(NoticeStatus status, DateTime deliveryDate, NoticeMedium medium, NotificationType noticeType, string patronBarcode, string itemBarcode, string comment = "")
        {
            NewMessage("43");
            AddFixedField(((int)status).ToString("00"), 2);
            AddFixedField(DateTime.Now.ToSipString(), 18);
            AddFixedField(deliveryDate.ToSipString(), 18);
            AddFixedField(((int)medium).ToString("000"), 3);
            AddFixedField(((int)noticeType).ToString("000"), 3);
            AddVariableField("AO", AO);
            AddVariableField("AA", patronBarcode);
            AddVariableField("AB", itemBarcode);
            AddVariableField("AC", "");
            AddVariableField("DE", comment, true);
            return SendMessage();
        }

        public SipTransaction AcsStatus()
        {
            NewMessage("99");
            AddFixedField("0", 1);
            AddFixedField("100", 3);
            AddFixedField("2.00", 4);
            return SendMessage();
        }

        public void Dispose()
        {
            socket.Dispose();
        }

        public bool IsConnected()
        {
            return socket.Connected;
        }

        public SipTransaction Custom(string sipMessage)
        {
            messageBuilder.Clear();
            messageBuilder.Append(sipMessage);
            return SendMessage();
        }

        public SipTransaction SendMessage()
        {
            var msg = BuildMessage();
            var watch = Stopwatch.StartNew();
            try
            {
                socket.WriteToStream(msg, TextEncoding);
                var response = socket.ReadFromStream(TextEncoding);
                return new SipTransaction
                {
                    Message = msg,
                    Response = response,
                    ResponseTime = watch.ElapsedMilliseconds,
                    ConnectionReset = string.IsNullOrEmpty(response),
                    ChecksumValid = !ValidateResponseChecksum || CheckChecksum(response)
                };
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException)
            {
                socket.Close();
                return new SipTransaction
                {
                    Message = msg,
                    Response = string.Empty,
                    ResponseTime = watch.ElapsedMilliseconds,
                    ConnectionReset = true,
                    ChecksumValid = false
                };
            }
        }

        public bool CheckChecksum(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return false;
            response = response.Trim('\r', '\n');
            if (response.Length < 8) return false;
            var checksum = response.Substring(response.Length - 6);
            if (!checksum.StartsWith("AZ", StringComparison.Ordinal)) return true;
            var msg = response.Substring(0, response.Length - 4);
            return string.Equals(checksum.Substring(2, 4), CalculateChecksum(msg, TextEncoding), StringComparison.OrdinalIgnoreCase);
        }

        public string BuildMessage()
        {
            messageBuilder.Append("AY" + GetSeqNumber());
            messageBuilder.Append("AZ");
            messageBuilder.Append(CalculateChecksum(messageBuilder.ToString(), TextEncoding));
            messageBuilder.Append(MessageTerminator);
            var msg = messageBuilder.ToString();
            return msg;
        }

        public static string CalculateChecksum(string msg)
        {
            return CalculateChecksum(msg, Encoding.UTF8);
        }

        public static string CalculateChecksum(string msg, Encoding encoding)
        {
            int checksum = 0;
            foreach (byte value in encoding.GetBytes(msg))
            {
                checksum = (checksum + value) & 0xFFFF;
            }
            return ((-checksum) & 0xFFFF).ToString("X4");
        }

        public int GetSeqNumber()
        {
            _seq = _seq == 9 ? 0 : ++_seq;
            return _seq;
        }
    }

    public class SocketDisconnectedException : Exception { }
}
