using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Clc;
using System.Reflection;
using System.IO;
using System.Configuration;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.Media;
using Clc.Sip.Helpers;

namespace Clc.Sip
{
    public partial class MainForm : Form
    {
        private sip2 sipclient;
        List<Button> cmdButtons = new List<Button>();
        private bool allowCmd = false;
        Color darkTextColor = ColorTranslator.FromHtml("#FFFF33");
        Color darkBackgroundColor = ColorTranslator.FromHtml("#374147");
        public static bool darkTheme = false;
        SettingsForm settingsForm;
        NoticeUpdateForm noticeUpdateForm;
        public MainForm()
        {
            InitializeComponent();

            portTextBox.KeyPress += portTextBox_KeyPress;

            cmdButtons.Add(this.patronInformationButton);
            cmdButtons.Add(this.patronStatusButton);
            cmdButtons.Add(this.renewAllButton);
            cmdButtons.Add(this.renewButton);
            cmdButtons.Add(this.itemInformationButton);
            cmdButtons.Add(this.checkoutButton);
            cmdButtons.Add(this.checkInButton);
            cmdButtons.Add(this.endSessionButton);
            cmdButtons.Add(this.loginButton);
            cmdButtons.Add(this.feePaidButton);
            cmdButtons.Add(this.noticeUpdateButton);

            bool.TryParse(ConfigurationManager.AppSettings["dark_theme_enabled"], out darkTheme);
            if (darkTheme) SwitchTheme(darkTextColor, darkBackgroundColor);

            DisableButtons();
        }

        void portTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !(e.KeyChar == 8);
        }

        void DisableButtons()
        {
            allowCmd = false;
            foreach (var b in cmdButtons)
            {
                if (darkTheme)
                {
                    cmdButtons.Where(t => t.Enabled == false).ToList().ForEach(t => t.Enabled = true);
                    b.ForeColor = Color.Red;
                }
                else
                {
                    b.Enabled = false;
                }
            }
        }

        void EnableButtons()
        {
            allowCmd = true;
            foreach (var b in cmdButtons)
            {
                b.ForeColor = darkTheme ? darkTextColor : Color.Black;
                if (!darkTheme || cmdButtons.Any(t => t.Enabled == false))
                {
                    b.Enabled = true;
                }
            }
        }

        void SwitchToDefaultTheme()
        {
            this.BackColor = Color.FromName("Control");
            this.ForeColor = Color.FromName("ControlText");

            foreach (Control c in this.Controls)
            {
                ChangeControlColors(c, Control.DefaultForeColor, Control.DefaultBackColor, Color.FromName("Window"));
            }
        }

        void SwitchTheme(Color textColor, Color backgroundColor, Color? textBoxBackground = null)
        {
            this.BackColor = backgroundColor;
            this.statusStrip1.BackColor = backgroundColor;
            textBoxBackground = textBoxBackground ?? backgroundColor;

            foreach (Control c in this.Controls)
            {
                ChangeControlColors(c, textColor, backgroundColor, textBoxBackground);
            }
        }

        void ChangeControlColors(Control parent, Color textColor, Color backgroundColor, Color? textBoxBackground = null)
        {
            parent.ForeColor = textColor;
            parent.BackColor = backgroundColor;
            if (new Type[] { typeof(TextBox), typeof(RichTextBox), typeof(ComboBox) }.Contains(parent.GetType()))
            {
                if (textBoxBackground.HasValue) parent.BackColor = textBoxBackground.Value;
            }
            foreach (Control c in parent.Controls)
            {
                c.ForeColor = textColor;
                c.BackColor = backgroundColor;
                if (new Type[] { typeof(TextBox), typeof(RichTextBox), typeof(ComboBox) }.Contains(c.GetType()))
                {
                    if (textBoxBackground.HasValue) c.BackColor = textBoxBackground.Value;
                }

                ChangeControlColors(c, textColor, backgroundColor, textBoxBackground);
            }
        }

        void LoadTextboxDefaults()
        {
            serverTextBox.Text = ConfigurationManager.AppSettings["default_server_hostname"];
            portTextBox.Text = ConfigurationManager.AppSettings["default_port"];
            aoTextBox.Text = ConfigurationManager.AppSettings["default_ao"];
            apTextBox.Text = ConfigurationManager.AppSettings["default_ap"];
            usernameTextBox.Text = ConfigurationManager.AppSettings["default_sip_username"];
            passwordTextBox.Text = ConfigurationManager.AppSettings["default_sip_password"];
            vendorProfileComboBox.Text = ConfigurationManager.AppSettings["default_vendor_profile"];
            patronBarcodeTextBox.Text = ConfigurationManager.AppSettings["default_patron_barcode"];
            patronPinTextBox.Text = ConfigurationManager.AppSettings["default_patron_pin"];
            itemBarcodeTextBox.Text = ConfigurationManager.AppSettings["default_item_barcode"];
        }

        public void LogOutput(SipTransaction txn)
        {
            if (logRichTextBox.Text.Length > 0)
            {
                logRichTextBox.AppendText("\n\n");
            }
            var message = txn.Message.Replace("\r", "").Replace("\n", "");
            var response = txn.Response.Replace("\r", "").Replace("\n", "");

            logRichTextBox.AppendText(string.Format("--> {0}\n", message), darkTheme ? Color.Red : Color.Crimson);
            logRichTextBox.AppendText(string.Format("<-- {0}", response), darkTheme ? Color.PowderBlue : Color.RoyalBlue);
            logRichTextBox.SelectionStart = this.logRichTextBox.Text.Length;
            logRichTextBox.ScrollToCaret();
        }

        void ConnectToSIPServer(string hostname, int port, string AO, string AP)
        {
            try
            {
                sipclient = new sip2(serverTextBox.Text, int.Parse(portTextBox.Text), AO, AP);
                sipclient.Connect();
                toolStripStatusLabel1.Text = string.Format("Connected to {0}:{1} | AO = {2} | AP = {3} | VP = {4} |", sipclient.hostname, sipclient.port, sipclient.AO, sipclient.AP, sipclient.VP);
                EnableButtons();
            }
            catch (Exception ex)
            {
                DisableButtons();
                toolStripStatusLabel1.Text = string.Format("Error connecting to SIP Server: {0}", ex.Message);
            }
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            int port;
            if (int.TryParse(portTextBox.Text, out port))
            {
                ConnectToSIPServer(serverTextBox.Text, port, aoTextBox.Text, apTextBox.Text);
            }
            else
            {
                MessageBox.Show("Port must be numeric");
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            bool start;

            bool.TryParse(ConfigurationManager.AppSettings["connect_on_start"], out start);
            if (start)
            {
                ConnectToSIPServer(serverTextBox.Text, int.Parse(portTextBox.Text), aoTextBox.Text, apTextBox.Text);
            }
        }

        #region SIP Methods

        private void EndSession(string barcode)
        {
            LogOutput(sipclient.EndSession(patronBarcodeTextBox.Text, patronPinTextBox.Text));
        }

        private void loginButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            var result = sipclient.Login(usernameTextBox.Text, passwordTextBox.Text, locationCodeTextBox.Text, vendorProfileComboBox.Text);
            LogOutput(result);
            toolStripStatusLabel1.Text = string.Format("Connected to {0}:{1} | AO = {2} | AP = {3} | CP = {4} | VP = {5} |", sipclient.hostname, sipclient.port, sipclient.AO, sipclient.AP, sipclient.CP, sipclient.VP);
        }

        private void patronStatusButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronStatus(patronBarcodeTextBox.Text, patronPinTextBox.Text));
        }

        private void patronInformationButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            patronInformationContextMenu.Show(patronInformationButton, new Point(0, patronInformationButton.Height));
        }

        private void endSessionButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            EndSession(itemBarcodeTextBox.Text);
        }

        private void checkoutButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.ItemCheckOut(patronBarcodeTextBox.Text, patronPinTextBox.Text, itemBarcodeTextBox.Text));
        }

        private void checkInButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.ItemCheckIn(itemBarcodeTextBox.Text));
        }

        private void itemInformationButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.ItemInformation(itemBarcodeTextBox.Text));
        }

        private void noneMenuItem_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text));
        }

        private void holdItemsMenuItem_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text, "hold", bpTextBox.Text, bqTextBox.Text));
        }

        private void overdueItemsMenuItem_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text, "overdue", bpTextBox.Text, bqTextBox.Text));
        }

        private void chargedItemsMenuItem_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text, "charged", bpTextBox.Text, bqTextBox.Text));
        }

        private void fineItemsMenuItem_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text, "fine", bpTextBox.Text, bqTextBox.Text));
        }

        private void recallItemsMenuItem_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text, "recall", bpTextBox.Text, bqTextBox.Text));
        }

        private void unavailableHoldsMenuItem_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text, "unavail", bpTextBox.Text, bqTextBox.Text));
        }

        private void renewButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.Renew(patronBarcodeTextBox.Text, patronPinTextBox.Text, DateTime.Now.AddDays(14), itemBarcodeTextBox.Text));
        }

        private void renewAllButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            LogOutput(sipclient.RenewAll(patronBarcodeTextBox.Text, patronPinTextBox.Text));
        }

        private void feePaidButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd) return;
            int feeType, paymentType;
            double amount;

            if (!int.TryParse(feeTypeTextBox.Text, out feeType) || (feeType < 0 || feeType > 99))
            {
                MessageBox.Show("Fee type must be a number between 0 and 99");
                return;
            }

            if (!int.TryParse(paymentTypeTextBox.Text, out paymentType) || (paymentType < 0 || paymentType > 99))
            {
                MessageBox.Show("Payment type must be a number between 0 and 99");
                return;
            }

            if (!double.TryParse(amountTextBox.Text.Replace("$", ""), out amount))
            {
                MessageBox.Show("Payment amount must be a number");
                return;
            }

            LogOutput(sipclient.FeePaid(feeType, paymentType, amount, patronBarcodeTextBox.Text, patronPinTextBox.Text, "USD", feeIdentifierTextBox.Text, transactionIdTextBox.Text));
        }

        #endregion

        private void changeDefaultSettingsButton_Click(object sender, EventArgs e)
        {
            if (settingsForm == null || settingsForm.IsDisposed) settingsForm = new SettingsForm();
            settingsForm.Location = this.Location;
            settingsForm.Visible = true;
            settingsForm.Focus();
        }

        private void reloadDefaultSettingsButton_Click(object sender, EventArgs e)
        {
            LoadTextboxDefaults();

            bool.TryParse(ConfigurationManager.AppSettings["dark_theme_enabled"], out darkTheme);
            if (darkTheme)
            {
                SwitchTheme(darkTextColor, darkBackgroundColor);
            }
            else
            {
                SwitchToDefaultTheme();
            }

            if (!allowCmd) DisableButtons();
        }

        private void clearLogButton_Click(object sender, EventArgs e)
        {
            logRichTextBox.Text = "";
        }

        private void exportLogButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text Files (*.txt)|*.txt";
            var result = sfd.ShowDialog();

            if (result == DialogResult.OK)
            {
                using (var sw = new StreamWriter(sfd.FileName))
                {
                    sw.Write(logRichTextBox.Text.Replace("\n", "\r\n"));
                    sw.Flush();
                }
            }
        }

        private void exportSettingsButton_Click(object sender, EventArgs e)
        {
            var settings = new
            {
                server = serverTextBox.Text,
                port = portTextBox.Text,
                ao = aoTextBox.Text,
                ap = apTextBox.Text,
                username = usernameTextBox.Text,
                password = passwordTextBox.Text,
                vendorProfile = vendorProfileComboBox.Text,
                patronBarcode = patronBarcodeTextBox.Text,
                patronPIN = patronPinTextBox.Text,
                bp = bpTextBox.Text,
                bq = bqTextBox.Text,
                itemBarcode = itemBarcodeTextBox.Text
            };

            var js = new JavaScriptSerializer();

            var json = js.Serialize(settings);

            var sfd = new SaveFileDialog();
            sfd.Filter = "Sip Testing Tool Configuration (*.sipc)|*.sipc";

            var dialogResult = sfd.ShowDialog();

            if (dialogResult == DialogResult.OK)
            {
                using (var sw = new StreamWriter(sfd.FileName))
                {
                    try
                    {
                        sw.Write(json);
                        sw.Flush();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        exportSettingsButton_Click(sender, e);
                    }
                }
            }
        }

        private void importSettingsButton_Click(object sender, EventArgs e)
        {
            var js = new JavaScriptSerializer();

            var ofd = new OpenFileDialog();
            ofd.Filter = "Sip Testing Tool Configuration (*.sipc)|*.sipc";

            var dialogResult = ofd.ShowDialog();


            if (dialogResult == DialogResult.OK)
            {
                SipSettings settings = new SipSettings();
                using (var sr = new StreamReader(ofd.FileName))
                {
                    try
                    {
                        settings = js.Deserialize<SipSettings>(sr.ReadToEnd());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        importSettingsButton_Click(sender, e);
                    }
                }
                serverTextBox.Text = settings.server;
                portTextBox.Text = settings.port;
                aoTextBox.Text = settings.ao;
                apTextBox.Text = settings.ap;
                usernameTextBox.Text = settings.username;
                passwordTextBox.Text = settings.password;
                vendorProfileComboBox.Text = settings.vendorProfile;
                patronBarcodeTextBox.Text = settings.patronBarcode;
                patronPinTextBox.Text = settings.patronPIN;
                bpTextBox.Text = settings.bp;
                bqTextBox.Text = settings.bq;
                itemBarcodeTextBox.Text = settings.itemBarcode;
            }
        }

        private void noticeUpdateButton_Click(object sender, EventArgs e)
        {
            if (noticeUpdateForm == null || noticeUpdateForm.IsDisposed) noticeUpdateForm = new NoticeUpdateForm(this);
            noticeUpdateForm.Location = this.Location;
            noticeUpdateForm.Visible = true;
            noticeUpdateForm.Focus();
        }

        public string PatronBarcode { get { return patronBarcodeTextBox.Text; } }
        public string ItemBarcode { get { return itemBarcodeTextBox.Text; } }
        public sip2 SipClient { get { return sipclient; } }


    }

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
