namespace NodeService.Installer
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            groupConfig = new GroupBox();
            txtUri = new TextBox();
            label9 = new Label();
            txtHost = new TextBox();
            cmbConfigs = new ComboBox();
            label8 = new Label();
            cmbEntryPoint = new ComboBox();
            label7 = new Label();
            txtServiceName = new TextBox();
            lblServiceName = new Label();
            BtnBrowseInstallPath = new Button();
            txtInstallPath = new TextBox();
            label6 = new Label();
            ProgressBar = new ProgressBar();
            NumericUpDownPort = new NumericUpDown();
            label5 = new Label();
            txtUserName = new TextBox();
            txtPassword = new TextBox();
            txtPath = new TextBox();
            label4 = new Label();
            label3 = new Label();
            label2 = new Label();
            label1 = new Label();
            txtInfo = new TextBox();
            btnImport = new Button();
            BtnUninstall = new Button();
            BtnInstall = new Button();
            lblServiceStatusWindowsService = new Label();
            lblServiceStatusUpdateService = new Label();
            lblServiceStatusWorkerService = new Label();
            chkAutoExit = new CheckBox();
            groupConfig.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)NumericUpDownPort).BeginInit();
            SuspendLayout();
            // 
            // groupConfig
            // 
            groupConfig.Controls.Add(txtUri);
            groupConfig.Controls.Add(label9);
            groupConfig.Controls.Add(txtHost);
            groupConfig.Controls.Add(cmbConfigs);
            groupConfig.Controls.Add(label8);
            groupConfig.Controls.Add(cmbEntryPoint);
            groupConfig.Controls.Add(label7);
            groupConfig.Controls.Add(txtServiceName);
            groupConfig.Controls.Add(lblServiceName);
            groupConfig.Controls.Add(BtnBrowseInstallPath);
            groupConfig.Controls.Add(txtInstallPath);
            groupConfig.Controls.Add(label6);
            groupConfig.Controls.Add(ProgressBar);
            groupConfig.Controls.Add(NumericUpDownPort);
            groupConfig.Controls.Add(label5);
            groupConfig.Controls.Add(txtUserName);
            groupConfig.Controls.Add(txtPassword);
            groupConfig.Controls.Add(txtPath);
            groupConfig.Controls.Add(label4);
            groupConfig.Controls.Add(label3);
            groupConfig.Controls.Add(label2);
            groupConfig.Controls.Add(label1);
            groupConfig.Location = new Point(12, 12);
            groupConfig.Name = "groupConfig";
            groupConfig.Size = new Size(1010, 418);
            groupConfig.TabIndex = 27;
            groupConfig.TabStop = false;
            groupConfig.Text = "配置信息";
            // 
            // txtUri
            // 
            txtUri.Location = new Point(166, 337);
            txtUri.Name = "txtUri";
            txtUri.Size = new Size(659, 30);
            txtUri.TabIndex = 53;
            txtUri.Text = "http://172.27.242.223:50060/";
            // 
            // label9
            // 
            label9.Location = new Point(38, 334);
            label9.Name = "label9";
            label9.Size = new Size(113, 36);
            label9.TabIndex = 52;
            label9.Text = "上报地址";
            label9.TextAlign = ContentAlignment.MiddleRight;
            // 
            // txtHost
            // 
            txtHost.Location = new Point(166, 64);
            txtHost.Name = "txtHost";
            txtHost.Size = new Size(383, 30);
            txtHost.TabIndex = 51;
            // 
            // cmbConfigs
            // 
            cmbConfigs.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbConfigs.FormattingEnabled = true;
            cmbConfigs.Location = new Point(166, 26);
            cmbConfigs.Name = "cmbConfigs";
            cmbConfigs.Size = new Size(383, 32);
            cmbConfigs.TabIndex = 50;
            cmbConfigs.SelectedIndexChanged += cmbConfigs_SelectedIndexChanged;
            // 
            // label8
            // 
            label8.Location = new Point(38, 23);
            label8.Name = "label8";
            label8.Size = new Size(113, 36);
            label8.TabIndex = 49;
            label8.Text = "配置";
            label8.TextAlign = ContentAlignment.MiddleRight;
            // 
            // cmbEntryPoint
            // 
            cmbEntryPoint.FormattingEnabled = true;
            cmbEntryPoint.Location = new Point(166, 299);
            cmbEntryPoint.Name = "cmbEntryPoint";
            cmbEntryPoint.Size = new Size(659, 32);
            cmbEntryPoint.TabIndex = 48;
            // 
            // label7
            // 
            label7.Location = new Point(38, 296);
            label7.Name = "label7";
            label7.Size = new Size(113, 36);
            label7.TabIndex = 47;
            label7.Text = "启动程序";
            label7.TextAlign = ContentAlignment.MiddleRight;
            // 
            // txtServiceName
            // 
            txtServiceName.Location = new Point(166, 263);
            txtServiceName.Name = "txtServiceName";
            txtServiceName.Size = new Size(659, 30);
            txtServiceName.TabIndex = 46;
            // 
            // lblServiceName
            // 
            lblServiceName.Location = new Point(38, 260);
            lblServiceName.Name = "lblServiceName";
            lblServiceName.Size = new Size(113, 36);
            lblServiceName.TabIndex = 45;
            lblServiceName.Text = "服务名称";
            lblServiceName.TextAlign = ContentAlignment.MiddleRight;
            // 
            // BtnBrowseInstallPath
            // 
            BtnBrowseInstallPath.Location = new Point(716, 214);
            BtnBrowseInstallPath.Name = "BtnBrowseInstallPath";
            BtnBrowseInstallPath.Size = new Size(109, 43);
            BtnBrowseInstallPath.TabIndex = 44;
            BtnBrowseInstallPath.Text = "浏览";
            BtnBrowseInstallPath.UseVisualStyleBackColor = true;
            // 
            // txtInstallPath
            // 
            txtInstallPath.Location = new Point(166, 220);
            txtInstallPath.Name = "txtInstallPath";
            txtInstallPath.Size = new Size(526, 30);
            txtInstallPath.TabIndex = 43;
            // 
            // label6
            // 
            label6.Location = new Point(38, 217);
            label6.Name = "label6";
            label6.Size = new Size(113, 36);
            label6.TabIndex = 42;
            label6.Text = "安装路径";
            label6.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ProgressBar
            // 
            ProgressBar.Location = new Point(38, 383);
            ProgressBar.Name = "ProgressBar";
            ProgressBar.Size = new Size(787, 22);
            ProgressBar.TabIndex = 39;
            // 
            // NumericUpDownPort
            // 
            NumericUpDownPort.Location = new Point(716, 61);
            NumericUpDownPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            NumericUpDownPort.Name = "NumericUpDownPort";
            NumericUpDownPort.Size = new Size(111, 30);
            NumericUpDownPort.TabIndex = 28;
            NumericUpDownPort.Value = new decimal(new int[] { 21, 0, 0, 0 });
            // 
            // label5
            // 
            label5.Location = new Point(641, 58);
            label5.Name = "label5";
            label5.Size = new Size(69, 36);
            label5.TabIndex = 35;
            label5.Text = "端口";
            label5.TextAlign = ContentAlignment.MiddleRight;
            // 
            // txtUserName
            // 
            txtUserName.Location = new Point(166, 100);
            txtUserName.Name = "txtUserName";
            txtUserName.Size = new Size(659, 30);
            txtUserName.TabIndex = 31;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(166, 136);
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '*';
            txtPassword.Size = new Size(659, 30);
            txtPassword.TabIndex = 32;
            // 
            // txtPath
            // 
            txtPath.Location = new Point(166, 172);
            txtPath.Name = "txtPath";
            txtPath.Size = new Size(659, 30);
            txtPath.TabIndex = 34;
            // 
            // label4
            // 
            label4.Location = new Point(38, 169);
            label4.Name = "label4";
            label4.Size = new Size(113, 36);
            label4.TabIndex = 33;
            label4.Text = "文件路径";
            label4.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label3
            // 
            label3.Location = new Point(38, 133);
            label3.Name = "label3";
            label3.Size = new Size(113, 36);
            label3.TabIndex = 30;
            label3.Text = "密码";
            label3.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label2
            // 
            label2.Location = new Point(38, 97);
            label2.Name = "label2";
            label2.Size = new Size(113, 36);
            label2.TabIndex = 29;
            label2.Text = "用户名";
            label2.TextAlign = ContentAlignment.MiddleRight;
            // 
            // label1
            // 
            label1.Location = new Point(38, 61);
            label1.Name = "label1";
            label1.Size = new Size(113, 36);
            label1.TabIndex = 27;
            label1.Text = "Ftp服务器";
            label1.TextAlign = ContentAlignment.MiddleRight;
            // 
            // txtInfo
            // 
            txtInfo.Location = new Point(12, 436);
            txtInfo.Multiline = true;
            txtInfo.Name = "txtInfo";
            txtInfo.ReadOnly = true;
            txtInfo.ScrollBars = ScrollBars.Vertical;
            txtInfo.Size = new Size(1010, 191);
            txtInfo.TabIndex = 41;
            // 
            // btnImport
            // 
            btnImport.Location = new Point(12, 786);
            btnImport.Name = "btnImport";
            btnImport.Size = new Size(146, 56);
            btnImport.TabIndex = 40;
            btnImport.Text = "导入配置";
            btnImport.UseVisualStyleBackColor = true;
            btnImport.Click += btnImport_Click;
            // 
            // BtnUninstall
            // 
            BtnUninstall.Location = new Point(876, 786);
            BtnUninstall.Name = "BtnUninstall";
            BtnUninstall.Size = new Size(146, 56);
            BtnUninstall.TabIndex = 38;
            BtnUninstall.Text = "卸载全部服务";
            BtnUninstall.UseVisualStyleBackColor = true;
            BtnUninstall.Click += BtnUninstall_Click;
            // 
            // BtnInstall
            // 
            BtnInstall.Location = new Point(462, 786);
            BtnInstall.Name = "BtnInstall";
            BtnInstall.Size = new Size(146, 56);
            BtnInstall.TabIndex = 36;
            BtnInstall.Text = "下载安装";
            BtnInstall.UseVisualStyleBackColor = true;
            BtnInstall.Click += BtnInstall_Click;
            // 
            // lblServiceStatusWindowsService
            // 
            lblServiceStatusWindowsService.AutoSize = true;
            lblServiceStatusWindowsService.Location = new Point(58, 643);
            lblServiceStatusWindowsService.Name = "lblServiceStatusWindowsService";
            lblServiceStatusWindowsService.Size = new Size(0, 24);
            lblServiceStatusWindowsService.TabIndex = 42;
            // 
            // lblServiceStatusUpdateService
            // 
            lblServiceStatusUpdateService.AutoSize = true;
            lblServiceStatusUpdateService.Location = new Point(58, 694);
            lblServiceStatusUpdateService.Name = "lblServiceStatusUpdateService";
            lblServiceStatusUpdateService.Size = new Size(0, 24);
            lblServiceStatusUpdateService.TabIndex = 43;
            // 
            // lblServiceStatusWorkerService
            // 
            lblServiceStatusWorkerService.AutoSize = true;
            lblServiceStatusWorkerService.Location = new Point(58, 742);
            lblServiceStatusWorkerService.Name = "lblServiceStatusWorkerService";
            lblServiceStatusWorkerService.Size = new Size(0, 24);
            lblServiceStatusWorkerService.TabIndex = 44;
            // 
            // chkAutoExit
            // 
            chkAutoExit.AutoSize = true;
            chkAutoExit.Checked = true;
            chkAutoExit.CheckState = CheckState.Checked;
            chkAutoExit.Location = new Point(200, 801);
            chkAutoExit.Name = "chkAutoExit";
            chkAutoExit.Size = new Size(198, 28);
            chkAutoExit.TabIndex = 54;
            chkAutoExit.Text = "安装完成后自动退出";
            chkAutoExit.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1050, 854);
            Controls.Add(chkAutoExit);
            Controls.Add(lblServiceStatusWorkerService);
            Controls.Add(lblServiceStatusUpdateService);
            Controls.Add(lblServiceStatusWindowsService);
            Controls.Add(groupConfig);
            Controls.Add(txtInfo);
            Controls.Add(BtnInstall);
            Controls.Add(BtnUninstall);
            Controls.Add(btnImport);
            FormBorderStyle = FormBorderStyle.Fixed3D;
            MaximizeBox = false;
            Name = "MainForm";
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "守护进程安装程序";
            Load += MainForm_Load;
            groupConfig.ResumeLayout(false);
            groupConfig.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)NumericUpDownPort).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private GroupBox groupConfig;
        private TextBox txtHost;
        private ComboBox cmbConfigs;
        private Label label8;
        private ComboBox cmbEntryPoint;
        private Label label7;
        private TextBox txtServiceName;
        private Label lblServiceName;
        private Button BtnBrowseInstallPath;
        private TextBox txtInstallPath;
        private Label label6;
        private TextBox txtInfo;
        private Button btnImport;
        private ProgressBar ProgressBar;
        private Button BtnUninstall;
        private Button BtnInstall;
        private NumericUpDown NumericUpDownPort;
        private Label label5;
        private TextBox txtUserName;
        private TextBox txtPassword;
        private TextBox txtPath;
        private Label label4;
        private Label label3;
        private Label label2;
        private Label label1;
        private TextBox txtUri;
        private Label label9;
        private Label lblServiceStatusWindowsService;
        private Label lblServiceStatusUpdateService;
        private Label lblServiceStatusWorkerService;
        private CheckBox chkAutoExit;
    }
}
