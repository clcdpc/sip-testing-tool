namespace Clc.Sip
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            usernameTextBox = new TextBox();
            passwordTextBox = new TextBox();
            label1 = new Label();
            label2 = new Label();
            loginButton = new Button();
            serverTextBox = new TextBox();
            portTextBox = new TextBox();
            label3 = new Label();
            label4 = new Label();
            groupBox1 = new GroupBox();
            aoTextBox = new TextBox();
            label10 = new Label();
            connectButton = new Button();
            label11 = new Label();
            apTextBox = new TextBox();
            label7 = new Label();
            patronInformationContextMenu = new ContextMenuStrip(components);
            noneMenuItem = new ToolStripMenuItem();
            holdItemsMenuItem = new ToolStripMenuItem();
            overdueItemsMenuItem = new ToolStripMenuItem();
            chargedItemsMenuItem = new ToolStripMenuItem();
            fineItemsMenuItem = new ToolStripMenuItem();
            recallItemsMenuItem = new ToolStripMenuItem();
            unavailableHoldsMenuItem = new ToolStripMenuItem();
            groupBox5 = new GroupBox();
            locationCodeTextBox = new TextBox();
            label20 = new Label();
            vendorProfileComboBox = new ComboBox();
            renewAllButton = new Button();
            renewButton = new Button();
            groupBox2 = new GroupBox();
            bpTextBox = new TextBox();
            label13 = new Label();
            patronBarcodeTextBox = new TextBox();
            label12 = new Label();
            label5 = new Label();
            bqTextBox = new TextBox();
            label6 = new Label();
            patronPinTextBox = new TextBox();
            checkoutButton = new Button();
            patronStatusButton = new Button();
            checkInButton = new Button();
            groupBox3 = new GroupBox();
            label8 = new Label();
            itemBarcodeTextBox = new TextBox();
            endSessionButton = new Button();
            itemInformationButton = new Button();
            patronInformationButton = new Button();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            splitContainer1 = new SplitContainer();
            noticeUpdateButton = new Button();
            feePaidButton = new Button();
            groupBox4 = new GroupBox();
            amountTextBox = new TextBox();
            transactionIdTextBox = new TextBox();
            label17 = new Label();
            feeIdentifierTextBox = new TextBox();
            label19 = new Label();
            paymentTypeTextBox = new TextBox();
            label18 = new Label();
            label16 = new Label();
            feeTypeTextBox = new TextBox();
            label15 = new Label();
            label14 = new Label();
            label9 = new Label();
            importSettingsButton = new Button();
            exportSettingsButton = new Button();
            exportLogButton = new Button();
            clearLogButton = new Button();
            reloadDefaultSettingsButton = new Button();
            changeDefaultSettingsButton = new Button();
            logRichTextBox = new RichTextBox();
            btnAcsStatus = new Button();
            groupBox1.SuspendLayout();
            patronInformationContextMenu.SuspendLayout();
            groupBox5.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            groupBox4.SuspendLayout();
            SuspendLayout();
            // 
            // usernameTextBox
            // 
            usernameTextBox.AccessibleName = "SIP Username";
            usernameTextBox.Location = new Point(97, 17);
            usernameTextBox.Margin = new Padding(4, 3, 4, 3);
            usernameTextBox.Name = "usernameTextBox";
            usernameTextBox.Size = new Size(145, 23);
            usernameTextBox.TabIndex = 0;
            // 
            // passwordTextBox
            // 
            passwordTextBox.AccessibleName = "SIP Password";
            passwordTextBox.Location = new Point(97, 46);
            passwordTextBox.Margin = new Padding(4, 3, 4, 3);
            passwordTextBox.Name = "passwordTextBox";
            passwordTextBox.PasswordChar = '*';
            passwordTextBox.Size = new Size(145, 23);
            passwordTextBox.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(2, 22);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(79, 15);
            label1.TabIndex = 3;
            label1.Text = "SIP Username";
            label1.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(5, 51);
            label2.Margin = new Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new Size(76, 15);
            label2.TabIndex = 3;
            label2.Text = "SIP Password";
            label2.TextAlign = ContentAlignment.MiddleRight;
            // 
            // loginButton
            // 
            loginButton.Location = new Point(97, 134);
            loginButton.Margin = new Padding(4, 3, 4, 3);
            loginButton.Name = "loginButton";
            loginButton.Size = new Size(75, 27);
            loginButton.TabIndex = 4;
            loginButton.Text = "Login";
            loginButton.UseVisualStyleBackColor = true;
            loginButton.Click += loginButton_Click;
            // 
            // serverTextBox
            // 
            serverTextBox.AccessibleName = "SIP Server";
            serverTextBox.Location = new Point(82, 22);
            serverTextBox.Margin = new Padding(4, 3, 4, 3);
            serverTextBox.Name = "serverTextBox";
            serverTextBox.Size = new Size(124, 23);
            serverTextBox.TabIndex = 0;
            // 
            // portTextBox
            // 
            portTextBox.AccessibleName = "SIP Server Port";
            portTextBox.Location = new Point(82, 48);
            portTextBox.Margin = new Padding(4, 3, 4, 3);
            portTextBox.Name = "portTextBox";
            portTextBox.Size = new Size(124, 23);
            portTextBox.TabIndex = 1;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(14, 25);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(58, 15);
            label3.TabIndex = 3;
            label3.Text = "SIP Server";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(24, 52);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(48, 15);
            label4.TabIndex = 3;
            label4.Text = "SIP Port";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(aoTextBox);
            groupBox1.Controls.Add(label10);
            groupBox1.Controls.Add(connectButton);
            groupBox1.Controls.Add(label11);
            groupBox1.Controls.Add(label4);
            groupBox1.Controls.Add(apTextBox);
            groupBox1.Controls.Add(serverTextBox);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(portTextBox);
            groupBox1.Location = new Point(14, 8);
            groupBox1.Margin = new Padding(4, 3, 4, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4, 3, 4, 3);
            groupBox1.Size = new Size(210, 170);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Connection Information";
            // 
            // aoTextBox
            // 
            aoTextBox.AccessibleName = "SIP AO Field";
            aoTextBox.Location = new Point(82, 76);
            aoTextBox.Margin = new Padding(4, 3, 4, 3);
            aoTextBox.Name = "aoTextBox";
            aoTextBox.Size = new Size(124, 23);
            aoTextBox.TabIndex = 2;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(48, 80);
            label10.Margin = new Padding(4, 0, 4, 0);
            label10.Name = "label10";
            label10.Size = new Size(24, 15);
            label10.TabIndex = 9;
            label10.Text = "AO";
            // 
            // connectButton
            // 
            connectButton.Location = new Point(62, 134);
            connectButton.Margin = new Padding(4, 3, 4, 3);
            connectButton.Name = "connectButton";
            connectButton.Size = new Size(88, 27);
            connectButton.TabIndex = 4;
            connectButton.Text = "Connect";
            connectButton.UseVisualStyleBackColor = true;
            connectButton.Click += connectButton_Click;
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(50, 107);
            label11.Margin = new Padding(4, 0, 4, 0);
            label11.Name = "label11";
            label11.Size = new Size(22, 15);
            label11.TabIndex = 10;
            label11.Text = "AP";
            // 
            // apTextBox
            // 
            apTextBox.AccessibleName = "SIP AP Field";
            apTextBox.Location = new Point(82, 104);
            apTextBox.Margin = new Padding(4, 3, 4, 3);
            apTextBox.Name = "apTextBox";
            apTextBox.Size = new Size(124, 23);
            apTextBox.TabIndex = 3;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(5, 80);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(81, 15);
            label7.TabIndex = 6;
            label7.Text = "Vendor Profile";
            label7.TextAlign = ContentAlignment.MiddleRight;
            // 
            // patronInformationContextMenu
            // 
            patronInformationContextMenu.Items.AddRange(new ToolStripItem[] { noneMenuItem, holdItemsMenuItem, overdueItemsMenuItem, chargedItemsMenuItem, fineItemsMenuItem, recallItemsMenuItem, unavailableHoldsMenuItem });
            patronInformationContextMenu.Name = "patronInformationContextMenu";
            patronInformationContextMenu.Size = new Size(170, 158);
            // 
            // noneMenuItem
            // 
            noneMenuItem.Name = "noneMenuItem";
            noneMenuItem.Size = new Size(169, 22);
            noneMenuItem.Text = "None";
            noneMenuItem.Click += noneMenuItem_Click;
            // 
            // holdItemsMenuItem
            // 
            holdItemsMenuItem.Name = "holdItemsMenuItem";
            holdItemsMenuItem.Size = new Size(169, 22);
            holdItemsMenuItem.Text = "Hold Items";
            holdItemsMenuItem.Click += holdItemsMenuItem_Click;
            // 
            // overdueItemsMenuItem
            // 
            overdueItemsMenuItem.Name = "overdueItemsMenuItem";
            overdueItemsMenuItem.Size = new Size(169, 22);
            overdueItemsMenuItem.Text = "Overdue Items";
            overdueItemsMenuItem.Click += overdueItemsMenuItem_Click;
            // 
            // chargedItemsMenuItem
            // 
            chargedItemsMenuItem.Name = "chargedItemsMenuItem";
            chargedItemsMenuItem.Size = new Size(169, 22);
            chargedItemsMenuItem.Text = "Charged Items";
            chargedItemsMenuItem.Click += chargedItemsMenuItem_Click;
            // 
            // fineItemsMenuItem
            // 
            fineItemsMenuItem.Name = "fineItemsMenuItem";
            fineItemsMenuItem.Size = new Size(169, 22);
            fineItemsMenuItem.Text = "Fine Items";
            fineItemsMenuItem.Click += fineItemsMenuItem_Click;
            // 
            // recallItemsMenuItem
            // 
            recallItemsMenuItem.Name = "recallItemsMenuItem";
            recallItemsMenuItem.Size = new Size(169, 22);
            recallItemsMenuItem.Text = "Recall Items";
            recallItemsMenuItem.Click += recallItemsMenuItem_Click;
            // 
            // unavailableHoldsMenuItem
            // 
            unavailableHoldsMenuItem.Name = "unavailableHoldsMenuItem";
            unavailableHoldsMenuItem.Size = new Size(169, 22);
            unavailableHoldsMenuItem.Text = "Unavailable Holds";
            unavailableHoldsMenuItem.Click += unavailableHoldsMenuItem_Click;
            // 
            // groupBox5
            // 
            groupBox5.Controls.Add(locationCodeTextBox);
            groupBox5.Controls.Add(label20);
            groupBox5.Controls.Add(label7);
            groupBox5.Controls.Add(vendorProfileComboBox);
            groupBox5.Controls.Add(usernameTextBox);
            groupBox5.Controls.Add(loginButton);
            groupBox5.Controls.Add(label1);
            groupBox5.Controls.Add(label2);
            groupBox5.Controls.Add(passwordTextBox);
            groupBox5.Location = new Point(231, 8);
            groupBox5.Margin = new Padding(4, 3, 4, 3);
            groupBox5.Name = "groupBox5";
            groupBox5.Padding = new Padding(4, 3, 4, 3);
            groupBox5.Size = new Size(250, 170);
            groupBox5.TabIndex = 1;
            groupBox5.TabStop = false;
            groupBox5.Text = "Login Information";
            // 
            // locationCodeTextBox
            // 
            locationCodeTextBox.AccessibleName = "Location Code";
            locationCodeTextBox.Location = new Point(97, 104);
            locationCodeTextBox.Margin = new Padding(4, 3, 4, 3);
            locationCodeTextBox.Name = "locationCodeTextBox";
            locationCodeTextBox.Size = new Size(145, 23);
            locationCodeTextBox.TabIndex = 3;
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.Location = new Point(2, 108);
            label20.Margin = new Padding(4, 0, 4, 0);
            label20.Name = "label20";
            label20.Size = new Size(84, 15);
            label20.TabIndex = 8;
            label20.Text = "Location Code";
            label20.TextAlign = ContentAlignment.MiddleRight;
            // 
            // vendorProfileComboBox
            // 
            vendorProfileComboBox.AccessibleName = "Vendor Profile";
            vendorProfileComboBox.FormattingEnabled = true;
            vendorProfileComboBox.Items.AddRange(new object[] { "Envisionware", "Comprise", "3M", "ITG" });
            vendorProfileComboBox.Location = new Point(97, 75);
            vendorProfileComboBox.Margin = new Padding(4, 3, 4, 3);
            vendorProfileComboBox.Name = "vendorProfileComboBox";
            vendorProfileComboBox.Size = new Size(145, 23);
            vendorProfileComboBox.TabIndex = 2;
            // 
            // renewAllButton
            // 
            renewAllButton.AccessibleName = "Renew All Items";
            renewAllButton.Location = new Point(1110, 69);
            renewAllButton.Margin = new Padding(4, 3, 4, 3);
            renewAllButton.Name = "renewAllButton";
            renewAllButton.Size = new Size(88, 39);
            renewAllButton.TabIndex = 9;
            renewAllButton.Text = "Renew All Items";
            renewAllButton.UseVisualStyleBackColor = true;
            renewAllButton.Click += renewAllButton_Click;
            // 
            // renewButton
            // 
            renewButton.AccessibleName = "Renew Item";
            renewButton.Location = new Point(1015, 113);
            renewButton.Margin = new Padding(4, 3, 4, 3);
            renewButton.Name = "renewButton";
            renewButton.Size = new Size(88, 39);
            renewButton.TabIndex = 11;
            renewButton.Text = "Renew   Item";
            renewButton.UseVisualStyleBackColor = true;
            renewButton.Click += renewButton_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(bpTextBox);
            groupBox2.Controls.Add(label13);
            groupBox2.Controls.Add(patronBarcodeTextBox);
            groupBox2.Controls.Add(label12);
            groupBox2.Controls.Add(label5);
            groupBox2.Controls.Add(bqTextBox);
            groupBox2.Controls.Add(label6);
            groupBox2.Controls.Add(patronPinTextBox);
            groupBox2.Location = new Point(486, 10);
            groupBox2.Margin = new Padding(4, 3, 4, 3);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new Padding(4, 3, 4, 3);
            groupBox2.Size = new Size(208, 103);
            groupBox2.TabIndex = 2;
            groupBox2.TabStop = false;
            groupBox2.Text = "Patron Information";
            // 
            // bpTextBox
            // 
            bpTextBox.AccessibleName = "SIP BP Field";
            bpTextBox.Location = new Point(70, 69);
            bpTextBox.Margin = new Padding(4, 3, 4, 3);
            bpTextBox.Name = "bpTextBox";
            bpTextBox.Size = new Size(46, 23);
            bpTextBox.TabIndex = 2;
            bpTextBox.Text = "1";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new Point(37, 74);
            label13.Margin = new Padding(4, 0, 4, 0);
            label13.Name = "label13";
            label13.Size = new Size(21, 15);
            label13.TabIndex = 3;
            label13.Text = "BP";
            // 
            // patronBarcodeTextBox
            // 
            patronBarcodeTextBox.AccessibleName = "Patron Barcode";
            patronBarcodeTextBox.Location = new Point(70, 20);
            patronBarcodeTextBox.Margin = new Padding(4, 3, 4, 3);
            patronBarcodeTextBox.Name = "patronBarcodeTextBox";
            patronBarcodeTextBox.Size = new Size(124, 23);
            patronBarcodeTextBox.TabIndex = 0;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new Point(117, 73);
            label12.Margin = new Padding(4, 0, 4, 0);
            label12.Name = "label12";
            label12.Size = new Size(23, 15);
            label12.TabIndex = 2;
            label12.Text = "BQ";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(7, 23);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(50, 15);
            label5.TabIndex = 3;
            label5.Text = "Barcode";
            // 
            // bqTextBox
            // 
            bqTextBox.AccessibleName = "SIP BQ Field";
            bqTextBox.Location = new Point(148, 69);
            bqTextBox.Margin = new Padding(4, 3, 4, 3);
            bqTextBox.Name = "bqTextBox";
            bqTextBox.Size = new Size(46, 23);
            bqTextBox.TabIndex = 3;
            bqTextBox.Text = "5";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(33, 47);
            label6.Margin = new Padding(4, 0, 4, 0);
            label6.Name = "label6";
            label6.Size = new Size(26, 15);
            label6.TabIndex = 3;
            label6.Text = "PIN";
            // 
            // patronPinTextBox
            // 
            patronPinTextBox.AccessibleName = "Patron PIN";
            patronPinTextBox.Location = new Point(70, 44);
            patronPinTextBox.Margin = new Padding(4, 3, 4, 3);
            patronPinTextBox.Name = "patronPinTextBox";
            patronPinTextBox.Size = new Size(124, 23);
            patronPinTextBox.TabIndex = 1;
            // 
            // checkoutButton
            // 
            checkoutButton.AccessibleName = "Item Checkout";
            checkoutButton.Location = new Point(1015, 25);
            checkoutButton.Margin = new Padding(4, 3, 4, 3);
            checkoutButton.Name = "checkoutButton";
            checkoutButton.Size = new Size(88, 39);
            checkoutButton.TabIndex = 5;
            checkoutButton.Text = "Item Checkout";
            checkoutButton.UseVisualStyleBackColor = true;
            checkoutButton.Click += checkoutButton_Click;
            // 
            // patronStatusButton
            // 
            patronStatusButton.AccessibleName = "Patron Status";
            patronStatusButton.Location = new Point(920, 69);
            patronStatusButton.Margin = new Padding(4, 3, 4, 3);
            patronStatusButton.Name = "patronStatusButton";
            patronStatusButton.Size = new Size(88, 39);
            patronStatusButton.TabIndex = 7;
            patronStatusButton.Text = "Patron Status";
            patronStatusButton.UseVisualStyleBackColor = true;
            patronStatusButton.Click += patronStatusButton_Click;
            // 
            // checkInButton
            // 
            checkInButton.AccessibleName = "Item Check-in";
            checkInButton.Location = new Point(1015, 69);
            checkInButton.Margin = new Padding(4, 3, 4, 3);
            checkInButton.Name = "checkInButton";
            checkInButton.Size = new Size(88, 39);
            checkInButton.TabIndex = 8;
            checkInButton.Text = "Item Check-in";
            checkInButton.UseVisualStyleBackColor = true;
            checkInButton.Click += checkInButton_Click;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(label8);
            groupBox3.Controls.Add(itemBarcodeTextBox);
            groupBox3.Location = new Point(486, 120);
            groupBox3.Margin = new Padding(4, 3, 4, 3);
            groupBox3.Name = "groupBox3";
            groupBox3.Padding = new Padding(4, 3, 4, 3);
            groupBox3.Size = new Size(208, 58);
            groupBox3.TabIndex = 3;
            groupBox3.TabStop = false;
            groupBox3.Text = "Item Information";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(13, 25);
            label8.Margin = new Padding(4, 0, 4, 0);
            label8.Name = "label8";
            label8.Size = new Size(50, 15);
            label8.TabIndex = 4;
            label8.Text = "Barcode";
            // 
            // itemBarcodeTextBox
            // 
            itemBarcodeTextBox.AccessibleName = "Item Barcode";
            itemBarcodeTextBox.Location = new Point(70, 22);
            itemBarcodeTextBox.Margin = new Padding(4, 3, 4, 3);
            itemBarcodeTextBox.Name = "itemBarcodeTextBox";
            itemBarcodeTextBox.Size = new Size(124, 23);
            itemBarcodeTextBox.TabIndex = 0;
            // 
            // endSessionButton
            // 
            endSessionButton.AccessibleName = "End Session";
            endSessionButton.Location = new Point(920, 113);
            endSessionButton.Margin = new Padding(4, 3, 4, 3);
            endSessionButton.Name = "endSessionButton";
            endSessionButton.Size = new Size(88, 39);
            endSessionButton.TabIndex = 10;
            endSessionButton.Text = "End  Session";
            endSessionButton.UseVisualStyleBackColor = true;
            endSessionButton.Click += endSessionButton_Click;
            // 
            // itemInformationButton
            // 
            itemInformationButton.AccessibleName = "Item Information";
            itemInformationButton.Location = new Point(1110, 113);
            itemInformationButton.Margin = new Padding(4, 3, 4, 3);
            itemInformationButton.Name = "itemInformationButton";
            itemInformationButton.Size = new Size(88, 39);
            itemInformationButton.TabIndex = 12;
            itemInformationButton.Text = "Item Information";
            itemInformationButton.UseVisualStyleBackColor = true;
            itemInformationButton.Click += itemInformationButton_Click;
            // 
            // patronInformationButton
            // 
            patronInformationButton.AccessibleName = "Patron Information";
            patronInformationButton.Location = new Point(920, 25);
            patronInformationButton.Margin = new Padding(4, 3, 4, 3);
            patronInformationButton.Name = "patronInformationButton";
            patronInformationButton.Size = new Size(88, 39);
            patronInformationButton.TabIndex = 4;
            patronInformationButton.Text = "Patron Information";
            patronInformationButton.UseVisualStyleBackColor = true;
            patronInformationButton.Click += patronInformationButton_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1 });
            statusStrip1.Location = new Point(0, 628);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Padding = new Padding(1, 0, 16, 0);
            statusStrip1.Size = new Size(1224, 22);
            statusStrip1.TabIndex = 28;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(88, 17);
            toolStripStatusLabel1.Text = "Not Connected";
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.FixedPanel = FixedPanel.Panel1;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Margin = new Padding(4, 3, 4, 3);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(btnAcsStatus);
            splitContainer1.Panel1.Controls.Add(noticeUpdateButton);
            splitContainer1.Panel1.Controls.Add(feePaidButton);
            splitContainer1.Panel1.Controls.Add(groupBox4);
            splitContainer1.Panel1.Controls.Add(label14);
            splitContainer1.Panel1.Controls.Add(label9);
            splitContainer1.Panel1.Controls.Add(importSettingsButton);
            splitContainer1.Panel1.Controls.Add(exportSettingsButton);
            splitContainer1.Panel1.Controls.Add(exportLogButton);
            splitContainer1.Panel1.Controls.Add(clearLogButton);
            splitContainer1.Panel1.Controls.Add(reloadDefaultSettingsButton);
            splitContainer1.Panel1.Controls.Add(changeDefaultSettingsButton);
            splitContainer1.Panel1.Controls.Add(groupBox1);
            splitContainer1.Panel1.Controls.Add(groupBox5);
            splitContainer1.Panel1.Controls.Add(renewAllButton);
            splitContainer1.Panel1.Controls.Add(patronInformationButton);
            splitContainer1.Panel1.Controls.Add(renewButton);
            splitContainer1.Panel1.Controls.Add(itemInformationButton);
            splitContainer1.Panel1.Controls.Add(groupBox2);
            splitContainer1.Panel1.Controls.Add(checkoutButton);
            splitContainer1.Panel1.Controls.Add(endSessionButton);
            splitContainer1.Panel1.Controls.Add(patronStatusButton);
            splitContainer1.Panel1.Controls.Add(groupBox3);
            splitContainer1.Panel1.Controls.Add(checkInButton);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(logRichTextBox);
            splitContainer1.Size = new Size(1224, 628);
            splitContainer1.SplitterDistance = 213;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 1;
            splitContainer1.TabStop = false;
            // 
            // noticeUpdateButton
            // 
            noticeUpdateButton.AccessibleName = "Update Notice";
            noticeUpdateButton.Location = new Point(1015, 159);
            noticeUpdateButton.Margin = new Padding(4, 3, 4, 3);
            noticeUpdateButton.Name = "noticeUpdateButton";
            noticeUpdateButton.Size = new Size(88, 40);
            noticeUpdateButton.TabIndex = 13;
            noticeUpdateButton.Text = "Update Notice";
            noticeUpdateButton.UseVisualStyleBackColor = true;
            noticeUpdateButton.Click += noticeUpdateButton_Click;
            // 
            // feePaidButton
            // 
            feePaidButton.AccessibleName = "Fee Paid";
            feePaidButton.Location = new Point(1110, 25);
            feePaidButton.Margin = new Padding(4, 3, 4, 3);
            feePaidButton.Name = "feePaidButton";
            feePaidButton.Size = new Size(88, 39);
            feePaidButton.TabIndex = 6;
            feePaidButton.Text = "Fee Paid";
            feePaidButton.UseVisualStyleBackColor = true;
            feePaidButton.Click += feePaidButton_Click;
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(amountTextBox);
            groupBox4.Controls.Add(transactionIdTextBox);
            groupBox4.Controls.Add(label17);
            groupBox4.Controls.Add(feeIdentifierTextBox);
            groupBox4.Controls.Add(label19);
            groupBox4.Controls.Add(paymentTypeTextBox);
            groupBox4.Controls.Add(label18);
            groupBox4.Controls.Add(label16);
            groupBox4.Controls.Add(feeTypeTextBox);
            groupBox4.Controls.Add(label15);
            groupBox4.Location = new Point(702, 10);
            groupBox4.Margin = new Padding(4, 3, 4, 3);
            groupBox4.Name = "groupBox4";
            groupBox4.Padding = new Padding(4, 3, 4, 3);
            groupBox4.Size = new Size(201, 168);
            groupBox4.TabIndex = 4;
            groupBox4.TabStop = false;
            groupBox4.Text = "Fee Information";
            // 
            // amountTextBox
            // 
            amountTextBox.AccessibleName = "Payment Amount";
            amountTextBox.Location = new Point(99, 129);
            amountTextBox.Margin = new Padding(4, 3, 4, 3);
            amountTextBox.Name = "amountTextBox";
            amountTextBox.Size = new Size(94, 23);
            amountTextBox.TabIndex = 4;
            // 
            // transactionIdTextBox
            // 
            transactionIdTextBox.AccessibleName = "Transaction ID";
            transactionIdTextBox.Location = new Point(99, 103);
            transactionIdTextBox.Margin = new Padding(4, 3, 4, 3);
            transactionIdTextBox.Name = "transactionIdTextBox";
            transactionIdTextBox.Size = new Size(94, 23);
            transactionIdTextBox.TabIndex = 3;
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.Location = new Point(14, 133);
            label17.Margin = new Padding(4, 0, 4, 0);
            label17.Name = "label17";
            label17.Size = new Size(80, 15);
            label17.TabIndex = 5;
            label17.Text = "Payment Amt";
            // 
            // feeIdentifierTextBox
            // 
            feeIdentifierTextBox.AccessibleName = "Fee Identifier";
            feeIdentifierTextBox.Location = new Point(99, 76);
            feeIdentifierTextBox.Margin = new Padding(4, 3, 4, 3);
            feeIdentifierTextBox.Name = "feeIdentifierTextBox";
            feeIdentifierTextBox.Size = new Size(94, 23);
            feeIdentifierTextBox.TabIndex = 2;
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.Location = new Point(5, 106);
            label19.Margin = new Padding(4, 0, 4, 0);
            label19.Name = "label19";
            label19.Size = new Size(81, 15);
            label19.TabIndex = 5;
            label19.Text = "Transaction ID";
            // 
            // paymentTypeTextBox
            // 
            paymentTypeTextBox.AccessibleName = "Payment Type";
            paymentTypeTextBox.Location = new Point(99, 50);
            paymentTypeTextBox.Margin = new Padding(4, 3, 4, 3);
            paymentTypeTextBox.Name = "paymentTypeTextBox";
            paymentTypeTextBox.Size = new Size(94, 23);
            paymentTypeTextBox.TabIndex = 1;
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.Location = new Point(15, 80);
            label18.Margin = new Padding(4, 0, 4, 0);
            label18.Name = "label18";
            label18.Size = new Size(75, 15);
            label18.TabIndex = 5;
            label18.Text = "Fee Identifier";
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Location = new Point(7, 53);
            label16.Margin = new Padding(4, 0, 4, 0);
            label16.Name = "label16";
            label16.Size = new Size(81, 15);
            label16.TabIndex = 5;
            label16.Text = "Payment Type";
            // 
            // feeTypeTextBox
            // 
            feeTypeTextBox.AccessibleName = "Fee Type";
            feeTypeTextBox.Location = new Point(99, 22);
            feeTypeTextBox.Margin = new Padding(4, 3, 4, 3);
            feeTypeTextBox.Name = "feeTypeTextBox";
            feeTypeTextBox.Size = new Size(94, 23);
            feeTypeTextBox.TabIndex = 0;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new Point(34, 25);
            label15.Margin = new Padding(4, 0, 4, 0);
            label15.Name = "label15";
            label15.Size = new Size(52, 15);
            label15.TabIndex = 5;
            label15.Text = "Fee Type";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            label14.Location = new Point(598, 181);
            label14.Margin = new Padding(4, 0, 4, 0);
            label14.Name = "label14";
            label14.Size = new Size(15, 20);
            label14.TabIndex = 37;
            label14.Text = "|";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            label9.Location = new Point(226, 181);
            label9.Margin = new Padding(4, 0, 4, 0);
            label9.Name = "label9";
            label9.Size = new Size(15, 20);
            label9.TabIndex = 36;
            label9.Text = "|";
            // 
            // importSettingsButton
            // 
            importSettingsButton.AccessibleName = "Import Settings";
            importSettingsButton.Location = new Point(737, 180);
            importSettingsButton.Margin = new Padding(4, 3, 4, 3);
            importSettingsButton.Name = "importSettingsButton";
            importSettingsButton.Size = new Size(112, 27);
            importSettingsButton.TabIndex = 19;
            importSettingsButton.Text = "Import Settings";
            importSettingsButton.UseVisualStyleBackColor = true;
            importSettingsButton.Click += importSettingsButton_Click;
            // 
            // exportSettingsButton
            // 
            exportSettingsButton.AccessibleName = "Export Settings";
            exportSettingsButton.Location = new Point(618, 180);
            exportSettingsButton.Margin = new Padding(4, 3, 4, 3);
            exportSettingsButton.Name = "exportSettingsButton";
            exportSettingsButton.Size = new Size(112, 27);
            exportSettingsButton.TabIndex = 18;
            exportSettingsButton.Text = "Export Settings";
            exportSettingsButton.UseVisualStyleBackColor = true;
            exportSettingsButton.Click += exportSettingsButton_Click;
            // 
            // exportLogButton
            // 
            exportLogButton.AccessibleName = "Export Log";
            exportLogButton.Location = new Point(125, 180);
            exportLogButton.Margin = new Padding(4, 3, 4, 3);
            exportLogButton.Name = "exportLogButton";
            exportLogButton.Size = new Size(99, 27);
            exportLogButton.TabIndex = 15;
            exportLogButton.Text = "Export Log";
            exportLogButton.UseVisualStyleBackColor = true;
            exportLogButton.Click += exportLogButton_Click;
            // 
            // clearLogButton
            // 
            clearLogButton.AccessibleName = "Clear Log";
            clearLogButton.Location = new Point(14, 180);
            clearLogButton.Margin = new Padding(4, 3, 4, 3);
            clearLogButton.Name = "clearLogButton";
            clearLogButton.Size = new Size(99, 27);
            clearLogButton.TabIndex = 14;
            clearLogButton.Text = "Clear Log";
            clearLogButton.UseVisualStyleBackColor = true;
            clearLogButton.Click += clearLogButton_Click;
            // 
            // reloadDefaultSettingsButton
            // 
            reloadDefaultSettingsButton.AccessibleName = "Reload Default Values";
            reloadDefaultSettingsButton.Location = new Point(425, 180);
            reloadDefaultSettingsButton.Margin = new Padding(4, 3, 4, 3);
            reloadDefaultSettingsButton.Name = "reloadDefaultSettingsButton";
            reloadDefaultSettingsButton.Size = new Size(172, 27);
            reloadDefaultSettingsButton.TabIndex = 17;
            reloadDefaultSettingsButton.Text = "Reload Default Values";
            reloadDefaultSettingsButton.UseVisualStyleBackColor = true;
            reloadDefaultSettingsButton.Click += reloadDefaultSettingsButton_Click;
            // 
            // changeDefaultSettingsButton
            // 
            changeDefaultSettingsButton.AccessibleName = "Change Default Settings";
            changeDefaultSettingsButton.Location = new Point(246, 180);
            changeDefaultSettingsButton.Margin = new Padding(4, 3, 4, 3);
            changeDefaultSettingsButton.Name = "changeDefaultSettingsButton";
            changeDefaultSettingsButton.Size = new Size(172, 27);
            changeDefaultSettingsButton.TabIndex = 16;
            changeDefaultSettingsButton.Text = "Change Default Settings";
            changeDefaultSettingsButton.UseVisualStyleBackColor = true;
            changeDefaultSettingsButton.Click += changeDefaultSettingsButton_Click;
            // 
            // logRichTextBox
            // 
            logRichTextBox.BackColor = Color.White;
            logRichTextBox.DetectUrls = false;
            logRichTextBox.Dock = DockStyle.Fill;
            logRichTextBox.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold);
            logRichTextBox.Location = new Point(0, 0);
            logRichTextBox.Margin = new Padding(4, 3, 4, 3);
            logRichTextBox.Name = "logRichTextBox";
            logRichTextBox.ReadOnly = true;
            logRichTextBox.Size = new Size(1224, 410);
            logRichTextBox.TabIndex = 1;
            logRichTextBox.TabStop = false;
            logRichTextBox.Text = "";
            //
            // btnAcsStatus
            //
            btnAcsStatus.AccessibleName = "ACS Status";
            btnAcsStatus.Location = new Point(915, 159);
            btnAcsStatus.Margin = new Padding(4, 3, 4, 3);
            btnAcsStatus.Name = "btnAcsStatus";
            btnAcsStatus.Size = new Size(88, 40);
            btnAcsStatus.TabIndex = 18;
            btnAcsStatus.Text = "ACS Status";
            btnAcsStatus.UseVisualStyleBackColor = true;
            btnAcsStatus.Click += btnAcsStatus_Click;
            //
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1224, 650);
            Controls.Add(splitContainer1);
            Controls.Add(statusStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4, 3, 4, 3);
            Name = "MainForm";
            Text = "SIP Testing Tool";
            Load += MainForm_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            patronInformationContextMenu.ResumeLayout(false);
            groupBox5.ResumeLayout(false);
            groupBox5.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox usernameTextBox;
        private System.Windows.Forms.TextBox passwordTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button loginButton;
        private System.Windows.Forms.TextBox serverTextBox;
        private System.Windows.Forms.TextBox portTextBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ContextMenuStrip patronInformationContextMenu;
        private System.Windows.Forms.ToolStripMenuItem noneMenuItem;
        private System.Windows.Forms.ToolStripMenuItem holdItemsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem overdueItemsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chargedItemsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fineItemsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recallItemsMenuItem;
        private System.Windows.Forms.ToolStripMenuItem unavailableHoldsMenuItem;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button renewAllButton;
        private System.Windows.Forms.Button renewButton;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox patronBarcodeTextBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox patronPinTextBox;
        private System.Windows.Forms.Button checkoutButton;
        private System.Windows.Forms.Button patronStatusButton;
        private System.Windows.Forms.Button checkInButton;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox itemBarcodeTextBox;
        private System.Windows.Forms.Button endSessionButton;
        private System.Windows.Forms.Button itemInformationButton;
        private System.Windows.Forms.Button patronInformationButton;
        private System.Windows.Forms.Button connectButton;
        private System.Windows.Forms.TextBox aoTextBox;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox apTextBox;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ComboBox vendorProfileComboBox;
        private System.Windows.Forms.Button changeDefaultSettingsButton;
        private System.Windows.Forms.Button reloadDefaultSettingsButton;
        private System.Windows.Forms.RichTextBox logRichTextBox;
        private System.Windows.Forms.Button clearLogButton;
        private System.Windows.Forms.TextBox bpTextBox;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox bqTextBox;
        private System.Windows.Forms.Button exportLogButton;
        private System.Windows.Forms.Button exportSettingsButton;
        private System.Windows.Forms.Button importSettingsButton;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Button feePaidButton;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.TextBox transactionIdTextBox;
        private System.Windows.Forms.TextBox feeIdentifierTextBox;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.TextBox paymentTypeTextBox;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox feeTypeTextBox;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox amountTextBox;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.TextBox locationCodeTextBox;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Button noticeUpdateButton;
        private System.Windows.Forms.Button btnAcsStatus;
    }
}

