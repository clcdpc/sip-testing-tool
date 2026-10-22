using System.Configuration;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clc.Sip.Helpers;
using Newtonsoft.Json;

namespace Clc.Sip
{
    public partial class MainForm : Form
    {
        private enum WorkbenchCommand
        {
            Checkout,
            Checkin,
            Renew,
            RenewAll,
            ItemInformation,
            PatronInformation,
            PatronStatus,
            EndSession,
            FeePaid,
            NoticeUpdate,
            Login,
            AcsStatus
        }

        private sip2 sipclient;
        private readonly List<Button> commandButtons = new();
        private bool allowCmd = false;
        private bool syncingThemeMenu;
        private SettingsForm settingsForm;
        private readonly MessageLogView messageLogView;

        public MainForm()
        {
            AppTheme.Initialize();
            InitializeComponent();
            messageLogView = new MessageLogView(logRichTextBox) { Dock = DockStyle.Fill };
            logLayout.Controls.Add(messageLogView, 0, 1);
            _ = new MessageLogToolTips(logRichTextBox, components);
            ResetCheckoutOptions();
            InitializeCommandOptions();

            portTextBox.KeyPress += portTextBox_KeyPress;

            commandButtons.Add(this.loginButton);
            commandButtons.Add(this.executeCommandButton);

            var alwaysOnTop = new ToolStripMenuItem("Always On Top") { CheckOnClick = true };
            alwaysOnTop.CheckedChanged += (sender, args) => TopMost = alwaysOnTop.Checked;
            ContextMenuStrip = new ContextMenuStrip(components);
            ContextMenuStrip.Items.Add(alwaysOnTop);
            AppTheme.Attach(this, ThemeApplied);
            LoadTextboxDefaults();
            DisableButtons();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                if (connectionLayout.ContainsFocus)
                {
                    connectButton.PerformClick();
                    return true;
                }

                // An open profile list uses Enter to accept the highlighted choice.
                if ((loginLayout.ContainsFocus && !vendorProfileComboBox.DroppedDown)
                    || loginUidAlgorithmTextBox.ContainsFocus || loginPwdAlgorithmTextBox.ContainsFocus)
                {
                    loginButton.PerformClick();
                    return true;
                }
            }

            return base.ProcessCmdKey(ref message, keyData);
        }

        private void commandComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (commandComboBox.SelectedIndex < 0)
            {
                return;
            }

            var command = (WorkbenchCommand)commandComboBox.SelectedIndex;
            commandDetailsLayout.SuspendLayout();
            checkoutFieldsLayout.Visible = command == WorkbenchCommand.Checkout;
            patronQueryLayout.Visible = command == WorkbenchCommand.PatronInformation;
            feeFieldsLayout.Visible = command == WorkbenchCommand.FeePaid;
            foreach (var option in commandOptions)
            {
                option.Value.Visible = option.Key == command;
            }

            selectedCommandLabel.Text = commandComboBox.Text;
            executeCommandButton.Text = "Execute " + commandComboBox.Text;
            commandHintLabel.Text = command switch
            {
                WorkbenchCommand.Checkin or WorkbenchCommand.ItemInformation => "Uses the shared item barcode.",
                WorkbenchCommand.PatronInformation or WorkbenchCommand.PatronStatus or
                    WorkbenchCommand.EndSession or WorkbenchCommand.RenewAll or WorkbenchCommand.FeePaid
                    => "Uses the shared patron barcode and PIN.",
                WorkbenchCommand.Login => "Uses the shared login settings.",
                WorkbenchCommand.AcsStatus => "Uses the current connection.",
                WorkbenchCommand.NoticeUpdate => "Uses the shared patron and item barcodes.",
                _ => "Uses the shared patron and item values."
            };
            commandDetailsLayout.ResumeLayout(true);
            commandDetailsPanel.AutoScrollPosition = Point.Empty;
        }

        private void patronSummaryComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bpTextBox.Enabled = bqTextBox.Enabled = true;
        }

        private void ResetCheckoutOptions()
        {
            checkoutRenewalCheckBox.Checked = true;
            checkoutNoBlockCheckBox.Checked = false;
            checkoutDueDatePicker.Value = DateTime.Now.AddDays(14);
            checkoutDueDatePicker.Checked = false;
            checkoutItemPropertiesTextBox.Clear();
            checkoutTerminalPasswordTextBox.Clear();
            checkoutFeeAcknowledgedComboBox.SelectedIndex = 1;
            checkoutCancelComboBox.SelectedIndex = 0;
        }

        private void resetCheckoutOptionsButton_Click(object sender, EventArgs e) => ResetCheckoutOptions();

        private static string GetOptionalFlagValue(ComboBox field) => field.SelectedIndex switch
        {
            1 => "N",
            2 => "Y",
            _ => ""
        };

        private void executeCommandButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd || commandComboBox.SelectedIndex < 0)
            {
                return;
            }

            SyncSharedProtocolFields();
            EventHandler execute = (WorkbenchCommand)commandComboBox.SelectedIndex switch
            {
                WorkbenchCommand.Checkout => checkoutButton_Click,
                WorkbenchCommand.Checkin => checkInButton_Click,
                WorkbenchCommand.Renew => renewButton_Click,
                WorkbenchCommand.RenewAll => renewAllButton_Click,
                WorkbenchCommand.ItemInformation => itemInformationButton_Click,
                WorkbenchCommand.PatronInformation => patronInformationButton_Click,
                WorkbenchCommand.PatronStatus => patronStatusButton_Click,
                WorkbenchCommand.EndSession => endSessionButton_Click,
                WorkbenchCommand.FeePaid => feePaidButton_Click,
                WorkbenchCommand.NoticeUpdate => noticeUpdateButton_Click,
                WorkbenchCommand.Login => loginButton_Click,
                WorkbenchCommand.AcsStatus => btnAcsStatus_Click,
                _ => throw new InvalidOperationException("Unknown SIP command.")
            };
            execute(sender, e);
        }

        private void portTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !(e.KeyChar == 8);
        }

        private void DisableButtons()
        {
            allowCmd = false;
            foreach (var button in commandButtons)
            {
                button.Enabled = false;
            }
        }

        private void EnableButtons()
        {
            allowCmd = true;
            foreach (var button in commandButtons)
            {
                button.Enabled = true;
            }
        }

        private void ThemeApplied()
        {
            syncingThemeMenu = true;
            try
            {
                darkModeMenuItem.Checked = AppTheme.IsDarkMode;
            }
            finally { syncingThemeMenu = false; }
            logRichTextBox.RecolorTranscript();
            messageLogView.RefreshTheme();
        }

        private void darkModeMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (syncingThemeMenu)
            {
                return;
            }

            AppTheme.SetDarkMode(darkModeMenuItem.Checked);
            try
            {
                AppTheme.SavePreference();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                toolStripStatusLabel1.Text = "Theme changed, but the preference could not be saved: " + ex.Message;
            }
        }

        private void LoadTextboxDefaults()
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
            if (txn.ConnectionReset)
            {
                DisableButtons();
                allowCmd = false;
                toolStripStatusLabel1.Text = "Connection reset";
                MessageBox.Show("Connection reset");
                return;
            }

            if (!txn.ChecksumValid)
            {
                MessageBox.Show("Invalid response checksum");
            }

            if (logRichTextBox.Text.Length > 0)
            {
                logRichTextBox.AppendText("\n\n");
            }
            var message = txn.Message.Replace("\r", "").Replace("\n", "");
            var response = txn.Response.Replace("\r", "").Replace("\n", "");

            logRichTextBox.AppendText(string.Format("--> {0}\n", message), AppTheme.Sent);
            logRichTextBox.AppendText(string.Format("<-- {0}", response), AppTheme.Received);
            logRichTextBox.SelectionStart = this.logRichTextBox.Text.Length;
            logRichTextBox.ScrollToCaret();
            if (sipclient?.IsConnected() == true)
            {
                toolStripStatusLabel1.Text = $"Connected to {sipclient.hostname}:{sipclient.port} | AO = {sipclient.AO} | AP = {sipclient.AP} | CP = {sipclient.CP} | VP = {sipclient.VP} |";
            }
        }

        private void ConnectToSIPServer(string hostname, int port, string AO, string AP)
        {
            try
            {
                sipclient?.Dispose();
                sipclient = new sip2(hostname, port, AO, AP);
                ConfigureSipClient();
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
            if (string.IsNullOrWhiteSpace(serverTextBox.Text))
            {
                MessageBox.Show("Server is required");
                serverTextBox.Focus();
                return;
            }

            if (!int.TryParse(portTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Port must be a number between 1 and 65535");
                portTextBox.Focus();
                return;
            }

            ConnectToSIPServer(serverTextBox.Text, port, aoTextBox.Text, apTextBox.Text);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            bool start;

            bool.TryParse(ConfigurationManager.AppSettings["connect_on_start"], out start);
            if (start)
            {
                connectButton_Click(sender, e);
            }
        }

        #region SIP Methods

        private void EndSession()
        {
            LogOutput(sipclient.EndSession(patronBarcodeTextBox.Text, patronPinTextBox.Text, endSessionPasswordTextBox.Text));
        }

        private void loginButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!ValidateAlgorithmOption(loginUidAlgorithmTextBox) || !ValidateAlgorithmOption(loginPwdAlgorithmTextBox))
            {
                return;
            }

            SyncSharedProtocolFields();
            var result = sipclient.Login(usernameTextBox.Text, passwordTextBox.Text, locationCodeTextBox.Text, vendorProfileComboBox.Text,
                loginUidAlgorithmTextBox.Text, loginPwdAlgorithmTextBox.Text);
            toolStripStatusLabel1.Text = string.Format("Connected to {0}:{1} | AO = {2} | AP = {3} | CP = {4} | VP = {5} |", sipclient.hostname, sipclient.port, sipclient.AO, sipclient.AP, sipclient.CP, sipclient.VP);
            LogOutput(result);
        }

        private void patronStatusButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!TryLanguageOption(patronStatusLanguageTextBox, out var language))
            {
                return;
            }

            LogOutput(sipclient.PatronStatus(patronBarcodeTextBox.Text, patronPinTextBox.Text, patronStatusPasswordTextBox.Text, language));
        }

        private void patronInformationButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!TryLanguageOption(patronInfoLanguageTextBox, out var language) || !ValidatePatronRanges())
            {
                return;
            }

            string[] summaries = { "", "hold", "overdue", "charged", "fine", "recall", "unavail" };
            var summary = summaries[Math.Max(0, patronSummaryComboBox.SelectedIndex)];
            LogOutput(sipclient.PatronInformation(patronBarcodeTextBox.Text, patronPinTextBox.Text,
                string.IsNullOrEmpty(summary) ? "none" : summary, bpTextBox.Text.Trim(), bqTextBox.Text.Trim(), patronInfoPasswordTextBox.Text, language));
        }

        private void endSessionButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            EndSession();
        }

        private void checkoutButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!checkoutDueDatePicker.TryGetValue(out var dueDate))
            {
                toolStripStatusLabel1.Text = "Enter a valid no-block due date/time (yyyy-MM-dd HH:mm:ss), or uncheck it to omit the date.";
                checkoutDueDatePicker.FocusEditor();
                return;
            }
            LogOutput(sipclient.ItemCheckOut(patronBarcodeTextBox.Text, patronPinTextBox.Text, itemBarcodeTextBox.Text,
                dueDate: checkoutDueDatePicker.Checked ? dueDate : null,
                renewalOk: checkoutRenewalCheckBox.Checked ? "Y" : "N",
                itemProperties: checkoutItemPropertiesTextBox.Text,
                feeAcknowledged: GetOptionalFlagValue(checkoutFeeAcknowledgedComboBox),
                noBlock: checkoutNoBlockCheckBox.Checked ? "Y" : "N",
                cancel: GetOptionalFlagValue(checkoutCancelComboBox),
                terminalPassword: checkoutTerminalPasswordTextBox.Text));
        }

        private void checkInButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!TryDateOption(checkinReturnDatePicker, "return date/time", out var returnDate))
            {
                return;
            }

            LogOutput(sipclient.ItemCheckIn(itemBarcodeTextBox.Text, returnDate, checkinLocationTextBox.Text, checkinPropertiesTextBox.Text,
                checkinNoBlockCheckBox.Checked ? "Y" : "N", GetOptionalFlagValue(checkinCancelComboBox), checkinPasswordTextBox.Text));
        }

        private void itemInformationButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            LogOutput(sipclient.ItemInformation(itemBarcodeTextBox.Text, itemInfoPasswordTextBox.Text));
        }

        private void renewButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!TryDateOption(renewDueDatePicker, "renewal due date/time", out var dueDate))
            {
                return;
            }

            LogOutput(sipclient.Renew(patronBarcodeTextBox.Text, patronPinTextBox.Text, dueDate,
                renewUseItemCheckBox.Checked ? itemBarcodeTextBox.Text : "", renewTitleTextBox.Text, renewPropertiesTextBox.Text,
                renewThirdPartyCheckBox.Checked ? "Y" : "N", renewNoBlockCheckBox.Checked ? "Y" : "N",
                GetOptionalFlagValue(renewFeeComboBox), renewPasswordTextBox.Text));
        }

        private void renewAllButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            LogOutput(sipclient.RenewAll(patronBarcodeTextBox.Text, patronPinTextBox.Text, GetOptionalFlagValue(renewAllFeeComboBox), renewAllPasswordTextBox.Text));
        }

        private void btnAcsStatus_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!TryIntegerOption(acsPrintWidthTextBox, 0, 999, "Maximum print width", out var width))
            {
                return;
            }

            var version = acsVersionTextBox.Text.Trim();
            if (!Regex.IsMatch(version, "^[0-9]\\.[0-9]{2}$"))
            {
                RejectOption(acsVersionTextBox, "Protocol version must use x.xx format, such as 2.00.");
                return;
            }
            LogOutput(sipclient.AcsStatus(acsStatusComboBox.SelectedIndex.ToString(CultureInfo.InvariantCulture), width, version));
        }

        private void feePaidButton_Click(object sender, EventArgs e)
        {
            if (!allowCmd)
            {
                return;
            }

            if (!TryIntegerOption(feeTypeTextBox, 0, 99, "Fee type", out var feeType)
                || !TryIntegerOption(paymentTypeTextBox, 0, 99, "Payment type", out var paymentType))
            {
                return;
            }

            if (!double.TryParse(amountTextBox.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || !double.IsFinite(amount))
            {
                RejectOption(amountTextBox, "Payment amount must be a finite number.");
                return;
            }
            var currency = feeCurrencyTextBox.Text.Trim().ToUpperInvariant();
            if (currency.Length != 3 || !currency.All(c => c is >= 'A' and <= 'Z'))
            {
                RejectOption(feeCurrencyTextBox, "Currency must be a three-letter code, such as USD.");
                return;
            }
            LogOutput(sipclient.FeePaid(feeType, paymentType, amount, patronBarcodeTextBox.Text, patronPinTextBox.Text,
                currency, feeIdentifierTextBox.Text, transactionIdTextBox.Text, feePasswordTextBox.Text));
        }

        #endregion

        private void changeDefaultSettingsButton_Click(object sender, EventArgs e)
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm();
            }

            settingsForm.Location = this.Location;
            settingsForm.Visible = true;
            settingsForm.Focus();
        }

        private void reloadDefaultSettingsButton_Click(object sender, EventArgs e)
        {
            LoadTextboxDefaults();

            AppTheme.ReloadPreference();

            ConfigureSipClient();
            if (!allowCmd)
            {
                DisableButtons();
            }
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

            var json = JsonConvert.SerializeObject(settings);

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
                        settings = JsonConvert.DeserializeObject<SipSettings>(sr.ReadToEnd());
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
            if (!allowCmd)
            {
                return;
            }

            if (!TryDateOption(noticeDeliveryDatePicker, "notice delivery date/time", out var deliveryDate))
            {
                return;
            }

            LogOutput(sipclient.NotificationUpdate((NoticeStatus)noticeStatusComboBox.SelectedItem, deliveryDate ?? DateTime.Now,
                (NoticeMedium)noticeMediumComboBox.SelectedItem, (NotificationType)notificationTypeComboBox.SelectedItem,
                patronBarcodeTextBox.Text, itemBarcodeTextBox.Text, noticeCommentTextBox.Text, noticePasswordTextBox.Text));
        }

        private void ConfigureSipClient()
        {
            if (sipclient == null)
            {
                return;
            }

            var encoding = ConfigurationManager.AppSettings["encoding"];
            sipclient.TextEncoding = Encoding.GetEncoding(string.IsNullOrWhiteSpace(encoding) ? "UTF-8" : encoding);
            sipclient.ValidateResponseChecksum = !bool.TryParse(ConfigurationManager.AppSettings["check_checksum"], out var check) || check;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            sipclient?.Dispose();
            settingsForm?.Dispose();
            base.OnFormClosed(e);
        }

        public string PatronBarcode
        {
            get
            {
                return patronBarcodeTextBox.Text;
            }
        }
        public string ItemBarcode
        {
            get
            {
                return itemBarcodeTextBox.Text;
            }
        }
        public sip2 SipClient
        {
            get
            {
                return sipclient;
            }
        }

    }
}
