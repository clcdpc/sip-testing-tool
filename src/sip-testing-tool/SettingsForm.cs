using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clc.Sip
{
	public partial class SettingsForm : Form
	{
		Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
		public SettingsForm()
		{
			InitializeComponent();
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

			bool connectOnStartup, darkTheme;
			bool.TryParse(ConfigurationManager.AppSettings["connect_on_start"], out connectOnStartup);
			bool.TryParse(ConfigurationManager.AppSettings["dark_theme_enabled"], out darkTheme);
			connectOnStartupCheckBox.Checked = connectOnStartup;
			darkThemeCheckBox.Checked = darkTheme;
		}
	}
}
