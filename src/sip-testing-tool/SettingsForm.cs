using System.Configuration;
using System.Text;
using Clc.Sip.Helpers;

namespace Clc.Sip
{
    public partial class SettingsForm : Form
    {
        Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        private bool syncingTheme;
        public SettingsForm()
        {
            AppTheme.Initialize();
            InitializeComponent();
            darkThemeCheckBox.CheckedChanged += (_, _) =>
            {
                if (!syncingTheme)
                {
                    AppTheme.SetDarkMode(darkThemeCheckBox.Checked);
                }
            };
            AppTheme.Attach(this, () =>
            {
                syncingTheme = true;
                try
                {
                    darkThemeCheckBox.Checked = AppTheme.IsDarkMode;
                }
                finally { syncingTheme = false; }
            });
        }

        void RefreshSettingValue(string setting, string value)
        {
            if (!config.AppSettings.Settings.AllKeys.Contains(setting))
            {
                config.AppSettings.Settings.Add(setting, value);
            }
            else
            {
                config.AppSettings.Settings[setting].Value = value;
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                Encoding.GetEncoding(encodingComboBox.Text);
            }
            catch
            {
                MessageBox.Show($"{encodingComboBox.Text} is not a valid encoding, please try another value");
                return;
            }

            RefreshSettingValue("default_server_hostname", hostnameTextBox.Text);
            RefreshSettingValue("default_port", portTextBox.Text);
            RefreshSettingValue("default_ao", aoTextBox.Text);
            RefreshSettingValue("default_ap", apTextBox.Text);
            RefreshSettingValue("default_sip_username", usernameTextBox.Text);
            RefreshSettingValue("default_sip_password", passwordTextBox.Text);
            RefreshSettingValue("default_vendor_profile", vendorProfileTextBox.Text);
            RefreshSettingValue("default_patron_barcode", patronBarcodeTextBox.Text);
            RefreshSettingValue("default_patron_pin", patronPinTextBox.Text);
            RefreshSettingValue("default_item_barcode", itemBarcodeTextBox.Text);
            RefreshSettingValue("connect_on_start", connectOnStartupCheckBox.Checked.ToString());
            RefreshSettingValue("dark_theme_enabled", darkThemeCheckBox.Checked.ToString());
            RefreshSettingValue("encoding", encodingComboBox.Text);
            RefreshSettingValue("check_checksum", checkChecksumCheckbox.Checked.ToString());
            try
            {
                AppTheme.SavePreference();
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                MessageBox.Show("The theme changed, but the preference could not be saved: " + ex.Message);
            }

            this.Close();
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            hostnameTextBox.Text = ConfigurationManager.AppSettings["default_server_hostname"];
            portTextBox.Text = ConfigurationManager.AppSettings["default_port"];
            aoTextBox.Text = ConfigurationManager.AppSettings["default_ao"];
            apTextBox.Text = ConfigurationManager.AppSettings["default_ap"];
            usernameTextBox.Text = ConfigurationManager.AppSettings["default_sip_username"];
            passwordTextBox.Text = ConfigurationManager.AppSettings["default_sip_password"];
            vendorProfileTextBox.Text = ConfigurationManager.AppSettings["default_vendor_profile"];
            patronBarcodeTextBox.Text = ConfigurationManager.AppSettings["default_patron_barcode"];
            patronPinTextBox.Text = ConfigurationManager.AppSettings["default_patron_pin"];
            itemBarcodeTextBox.Text = ConfigurationManager.AppSettings["default_item_barcode"];
            encodingComboBox.Text = ConfigurationManager.AppSettings["encoding"];
            bool connectOnStartup, checkChecksum;
            bool.TryParse(ConfigurationManager.AppSettings["connect_on_start"], out connectOnStartup);
            bool.TryParse(ConfigurationManager.AppSettings["check_checksum"], out checkChecksum);
            connectOnStartupCheckBox.Checked = connectOnStartup;
            darkThemeCheckBox.Checked = AppTheme.IsDarkMode;
            checkChecksumCheckbox.Checked = checkChecksum;
        }

        private void encodingComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
    }
}
