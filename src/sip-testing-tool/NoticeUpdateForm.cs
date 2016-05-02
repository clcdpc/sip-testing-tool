using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Clc.Sip
{
    public partial class NoticeUpdateForm : Form
    {
        MainForm mainForm;
        public NoticeUpdateForm(MainForm _mainForm)
        {
            mainForm = _mainForm;
            InitializeComponent();
            noticeMediumComboBox.DataSource = Enum.GetValues(typeof(NoticeMedium));
            noticeStatusComboBox.DataSource = Enum.GetValues(typeof(NoticeStatus));
            notificationTypeComboBox.DataSource = Enum.GetValues(typeof(NotificationType));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var status = (NoticeStatus)noticeStatusComboBox.SelectedItem;
            var medium = (NoticeMedium)noticeMediumComboBox.SelectedItem;
            var type = (NotificationType)notificationTypeComboBox.SelectedItem;
            DateTime deliveryDate;
            deliveryDate = DateTime.TryParse(deliveryDateTextBox.Text, out deliveryDate) ? deliveryDate : DateTime.Now;

            mainForm.LogOutput(mainForm.SipClient.NotificationUpdate(status, deliveryDate, medium, type, patronBarcodeTextBox.Text, itemBarcodeTextBox.Text, commentTextBox.Text));           
        }

        private void NoticeUpdateForm_Load(object sender, EventArgs e)
        {
            patronBarcodeTextBox.Text = mainForm.PatronBarcode;
            itemBarcodeTextBox.Text = mainForm.ItemBarcode;
            deliveryDateTextBox.Text = DateTime.Now.ToString();
        }
    }
}
