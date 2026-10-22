using System.Globalization;
using Clc.Sip.Helpers;

namespace Clc.Sip;

public partial class MainForm
{
    private readonly Dictionary<WorkbenchCommand, CommandOptionsPanel> commandOptions = [];

    private CheckBox checkinNoBlockCheckBox;
    private ThemedDateTimePicker checkinReturnDatePicker;
    private TextBox checkinLocationTextBox;
    private TextBox checkinPropertiesTextBox;
    private TextBox checkinPasswordTextBox;
    private ComboBox checkinCancelComboBox;

    private CheckBox renewThirdPartyCheckBox;
    private CheckBox renewNoBlockCheckBox;
    private CheckBox renewUseItemCheckBox;
    private ThemedDateTimePicker renewDueDatePicker;
    private TextBox renewTitleTextBox;
    private TextBox renewPropertiesTextBox;
    private TextBox renewPasswordTextBox;
    private ComboBox renewFeeComboBox;

    private TextBox renewAllPasswordTextBox;
    private ComboBox renewAllFeeComboBox;

    private TextBox itemInfoPasswordTextBox;

    private TextBox patronInfoPasswordTextBox;
    private TextBox patronInfoLanguageTextBox;

    private TextBox patronStatusPasswordTextBox;
    private TextBox patronStatusLanguageTextBox;

    private TextBox endSessionPasswordTextBox;

    private TextBox feeCurrencyTextBox;
    private TextBox feePasswordTextBox;

    private ThemedDateTimePicker noticeDeliveryDatePicker;
    private TextBox noticeCommentTextBox;
    private TextBox noticePasswordTextBox;
    private ComboBox noticeStatusComboBox;
    private ComboBox noticeMediumComboBox;
    private ComboBox notificationTypeComboBox;

    private TextBox loginUidAlgorithmTextBox;
    private TextBox loginPwdAlgorithmTextBox;

    private TextBox acsPrintWidthTextBox;
    private TextBox acsVersionTextBox;
    private ComboBox acsStatusComboBox;

    private void InitializeCommandOptions()
    {
        commandDetailsPanel.SuspendLayout();
        commandDetailsLayout.SuspendLayout();
        try
        {
            InitializeCheckinOptions();
            InitializeRenewOptions();
            InitializeRenewAllOptions();
            InitializeItemInformationOptions();
            InitializePatronInformationOptions();
            InitializePatronStatusOptions();
            InitializeEndSessionOptions();
            InitializeFeePaidOptions();
            InitializeNoticeUpdateOptions();
            InitializeLoginOptions();
            InitializeAcsStatusOptions();
            commandComboBox_SelectedIndexChanged(commandComboBox, EventArgs.Empty);
        }
        finally
        {
            foreach (var panel in commandOptions.Values)
            {
                panel.ResumeLayout(true);
            }
            commandDetailsLayout.ResumeLayout(true);
            commandDetailsPanel.ResumeLayout(true);
        }
    }

    private void InitializeCheckinOptions()
    {
        var checkin = AddCommandOptionsPanel(WorkbenchCommand.Checkin, "checkin", "checkin");
        checkinNoBlockCheckBox = checkin.AddFlag("checkinNoBlockCheckBox", "No block", false,
            "Y reports an already completed offline checkin; unchecked sends N.");
        checkinReturnDatePicker = checkin.AddDateField("checkinReturnDatePicker", "Return date/time", false, () => DateTime.Now,
            "Check to override the return date/time. Unchecked uses the time each request is sent.");
        checkinLocationTextBox = checkin.AddTextField("checkinLocationTextBox", "Return location (AP)", "",
            "Leave blank to use the shared Terminal (AP) value.");
        checkinPropertiesTextBox = checkin.AddTextField("checkinPropertiesTextBox", "Item properties (CH)");
        checkinPasswordTextBox = checkin.AddTextField("checkinPasswordTextBox", "Terminal password (AC)", "", "Blank sends AC with an empty value.");
        checkinCancelComboBox = checkin.AddOptionalFlag("checkinCancelComboBox", "Cancel (BI)");
        checkin.AddResetButton();
    }

    private void InitializeRenewOptions()
    {
        var renew = AddCommandOptionsPanel(WorkbenchCommand.Renew, "renew", "renew");
        renewThirdPartyCheckBox = renew.AddFlag("renewThirdPartyCheckBox", "Allow third-party renewals", true);
        renewNoBlockCheckBox = renew.AddFlag("renewNoBlockCheckBox", "No block", false,
            "Y reports an already completed offline renewal; unchecked sends N.");
        renewDueDatePicker = renew.AddDateField("renewDueDatePicker", "No-block due date/time", true, () => DateTime.Now.AddDays(14),
            "Checked sends this date/time. Unchecked sends a blank due date. Reset suggests fourteen days from now.");
        renewUseItemCheckBox = renew.AddFlag("renewUseItemCheckBox", "Use shared item barcode (AB)", true,
            "Uncheck to omit AB, for example when renewing by title identifier AJ instead.");
        renewTitleTextBox = renew.AddTextField("renewTitleTextBox", "Title identifier (AJ)");
        renewPropertiesTextBox = renew.AddTextField("renewPropertiesTextBox", "Item properties (CH)");
        renewPasswordTextBox = renew.AddTextField("renewPasswordTextBox", "Terminal password (AC)");
        renewFeeComboBox = renew.AddOptionalFlag("renewFeeComboBox", "Fee acknowledged (BO)");
        renew.AddResetButton();
    }

    private void InitializeRenewAllOptions()
    {
        var renewAll = AddCommandOptionsPanel(WorkbenchCommand.RenewAll, "renewAll", "renew all");
        renewAllPasswordTextBox = renewAll.AddTextField("renewAllPasswordTextBox", "Terminal password (AC)");
        renewAllFeeComboBox = renewAll.AddOptionalFlag("renewAllFeeComboBox", "Fee acknowledged (BO)");
        renewAll.AddResetButton();
    }

    private void InitializeItemInformationOptions()
    {
        var item = AddCommandOptionsPanel(WorkbenchCommand.ItemInformation, "itemInfo", "item information");
        itemInfoPasswordTextBox = item.AddTextField("itemInfoPasswordTextBox", "Terminal password (AC)", "", "Blank preserves the existing empty AC field.");
        item.AddResetButton();
    }

    private void InitializePatronInformationOptions()
    {
        var patronInfo = AddCommandOptionsPanel(WorkbenchCommand.PatronInformation, "patronInfo", "patron information");
        patronInfoLanguageTextBox = patronInfo.AddTextField("patronInfoLanguageTextBox", "Language code", "000", "Three digits; 000 requests the default language.", 3);
        patronInfoPasswordTextBox = patronInfo.AddTextField("patronInfoPasswordTextBox", "Terminal password (AC)");
        checkoutToolTip.SetToolTip(bpTextBox, "Optional start item. Clear to omit BP, including when no summary is selected.");
        checkoutToolTip.SetToolTip(bqTextBox, "Optional end item. Clear to omit BQ, including when no summary is selected.");
        bqTextBox.Margin = new Padding(0, 0, 0, 8);
        patronInfo.AddResetButton(() =>
        {
            patronSummaryComboBox.SelectedIndex = 0;
            bpTextBox.Text = "1";
            bqTextBox.Text = "5";
        });
    }

    private void InitializePatronStatusOptions()
    {
        var patronStatus = AddCommandOptionsPanel(WorkbenchCommand.PatronStatus, "patronStatus", "patron status");
        patronStatusLanguageTextBox = patronStatus.AddTextField("patronStatusLanguageTextBox", "Language code", "000", "Three digits; 000 requests the default language.", 3);
        patronStatusPasswordTextBox = patronStatus.AddTextField("patronStatusPasswordTextBox", "Terminal password (AC)", "", "Blank sends AC with an empty value.");
        patronStatus.AddResetButton();
    }

    private void InitializeEndSessionOptions()
    {
        var endSession = AddCommandOptionsPanel(WorkbenchCommand.EndSession, "endSession", "end session");
        endSessionPasswordTextBox = endSession.AddTextField("endSessionPasswordTextBox", "Terminal password (AC)");
        endSession.AddResetButton();
    }

    private void InitializeFeePaidOptions()
    {
        var fee = AddCommandOptionsPanel(WorkbenchCommand.FeePaid, "fee", "fee paid");
        foreach (var field in new[] { feeTypeTextBox, paymentTypeTextBox, amountTextBox, feeIdentifierTextBox, transactionIdTextBox })
        {
            field.Margin = new Padding(0, 0, 0, 8);
        }

        feeCurrencyTextBox = fee.AddTextField("feeCurrencyTextBox", "Currency code", "USD", "Three-letter currency code, such as USD, CAD or EUR.", 3);
        feePasswordTextBox = fee.AddTextField("feePasswordTextBox", "Terminal password (AC)");
        fee.AddResetButton(() =>
        {
            foreach (var field in new[] { feeTypeTextBox, paymentTypeTextBox, amountTextBox, feeIdentifierTextBox, transactionIdTextBox })
            {
                field.Clear();
            }
        });
    }

    private void InitializeNoticeUpdateOptions()
    {
        var notice = AddCommandOptionsPanel(WorkbenchCommand.NoticeUpdate, "notice", "notice update");
        noticeStatusComboBox = notice.AddEnumField<NoticeStatus>("noticeStatusComboBox", "Delivery status");
        noticeDeliveryDatePicker = notice.AddDateField("noticeDeliveryDatePicker", "Delivery date/time", false, () => DateTime.Now,
            "Check to specify when the notice was delivered. Unchecked uses the time each request is sent.");
        noticeMediumComboBox = notice.AddEnumField<NoticeMedium>("noticeMediumComboBox", "Delivery medium");
        notificationTypeComboBox = notice.AddEnumField<NotificationType>("notificationTypeComboBox", "Notice type");
        noticeCommentTextBox = notice.AddTextField("noticeCommentTextBox", "Comment (DE)");
        noticePasswordTextBox = notice.AddTextField("noticePasswordTextBox", "Terminal password (AC)", "", "Blank sends AC with an empty value.");
        notice.AddResetButton();
    }

    private void InitializeLoginOptions()
    {
        var login = AddCommandOptionsPanel(WorkbenchCommand.Login, "login", "login");
        loginUidAlgorithmTextBox = login.AddTextField("loginUidAlgorithmTextBox", "User ID algorithm", "0", "0 sends the user ID as plain text. Other one-character identifiers require a server-specific encoded value.", 1);
        loginPwdAlgorithmTextBox = login.AddTextField("loginPwdAlgorithmTextBox", "Password algorithm", "0", "0 sends the password as plain text. Other one-character identifiers require a server-specific encoded value.", 1);
        login.AddHint("Credentials come from the ribbon and are sent as entered. Choosing an algorithm identifier does not encrypt them.");
        login.AddResetButton();
    }

    private void InitializeAcsStatusOptions()
    {
        var acs = AddCommandOptionsPanel(WorkbenchCommand.AcsStatus, "acs", "ACS status");
        acsStatusComboBox = acs.AddChoice("acsStatusComboBox", "Self-check status", ["OK (0)", "Out of paper (1)", "About to shut down (2)"]);
        acsPrintWidthTextBox = acs.AddTextField("acsPrintWidthTextBox", "Maximum print width", "100", "Number of characters, from 0 through 999.", 3);
        acsVersionTextBox = acs.AddTextField("acsVersionTextBox", "Protocol version", "2.00", "Four characters in x.xx format.", 4);
        acs.AddResetButton();
    }

    private CommandOptionsPanel AddCommandOptionsPanel(WorkbenchCommand command, string prefix, string caption)
    {
        var panel = new CommandOptionsPanel(prefix, caption, checkoutToolTip) { Visible = false };
        panel.SuspendLayout();
        var row = commandDetailsLayout.RowCount++;
        commandDetailsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        commandDetailsLayout.Controls.Add(panel, 0, row);
        commandOptions.Add(command, panel);
        return panel;
    }

    private void SyncSharedProtocolFields()
    {
        if (sipclient == null)
        {
            return;
        }

        sipclient.AO = aoTextBox.Text;
        sipclient.AP = apTextBox.Text;
    }

    private bool RejectOption(Control field, string message)
    {
        foreach (var (command, panel) in commandOptions)
        {
            if (panel.Contains(field))
            {
                commandComboBox.SelectedIndex = (int)command;
                break;
            }
        }

        toolStripStatusLabel1.Text = message;
        commandDetailsPanel.ScrollControlIntoView(field);
        if (field is ThemedDateTimePicker date)
        {
            date.FocusEditor();
        }
        else
        {
            field.Focus();
            if (field is TextBoxBase text)
            {
                text.SelectAll();
            }
        }
        return false;
    }

    private bool TryDateOption(ThemedDateTimePicker field, string label, out DateTime? value)
    {
        value = null;
        if (!field.TryGetValue(out var date))
        {
            return RejectOption(field, "Enter a valid " + label + " (yyyy-MM-dd HH:mm:ss), or uncheck the date override.");
        }

        if (field.Checked)
        {
            value = date;
        }

        return true;
    }

    private bool TryLanguageOption(TextBox field, out string code)
    {
        code = field.Text.Trim();
        if (code.Length == 3 && code.All(character => character is >= '0' and <= '9'))
        {
            return true;
        }

        return RejectOption(field, "Language must be a three-digit SIP code, such as 000.");
    }

    private bool TryIntegerOption(TextBox field, int min, int max, string label, out int value)
    {
        if (int.TryParse(field.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max)
        {
            return true;
        }

        return RejectOption(field, label + " must be a number between " + min + " and " + max + ".");
    }

    private bool ValidateAlgorithmOption(TextBox field)
    {
        var identifier = field.Text;
        if (identifier.Length == 1 && identifier[0] is >= '!' and <= '~' && identifier[0] != '|')
        {
            return true;
        }

        return RejectOption(field, "Algorithm identifiers must be one printable protocol character; use 0 for plain text.");
    }

    private bool ValidatePatronRanges()
    {
        foreach (var field in new[] { bpTextBox, bqTextBox })
        {
            if (field.Text.Length > 0 && !TryIntegerOption(field, 1, int.MaxValue, field == bpTextBox ? "Start item (BP)" : "End item (BQ)", out _))
            {
                return false;
            }
        }

        if (int.TryParse(bpTextBox.Text, out var start) && int.TryParse(bqTextBox.Text, out var end) && start > end)
        {
            return RejectOption(bqTextBox, "End item (BQ) must be greater than or equal to start item (BP).");
        }

        return true;
    }
}
