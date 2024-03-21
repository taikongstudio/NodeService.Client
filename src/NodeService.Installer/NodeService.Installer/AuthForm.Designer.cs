namespace NodeService.Installer
{
    partial class AuthForm
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
            label9 = new Label();
            txtKey = new TextBox();
            groupBox2 = new GroupBox();
            BtnVerify = new Button();
            BtnImport = new Button();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // label9
            // 
            label9.Location = new Point(6, 33);
            label9.Name = "label9";
            label9.Size = new Size(142, 36);
            label9.TabIndex = 28;
            label9.Text = "请输入密钥";
            label9.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtKey
            // 
            txtKey.Location = new Point(6, 107);
            txtKey.Multiline = true;
            txtKey.Name = "txtKey";
            txtKey.PasswordChar = '*';
            txtKey.Size = new Size(764, 237);
            txtKey.TabIndex = 0;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(BtnImport);
            groupBox2.Controls.Add(BtnVerify);
            groupBox2.Controls.Add(label9);
            groupBox2.Controls.Add(txtKey);
            groupBox2.Location = new Point(12, 12);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(776, 437);
            groupBox2.TabIndex = 29;
            groupBox2.TabStop = false;
            groupBox2.Text = "授权验证";
            // 
            // BtnVerify
            // 
            BtnVerify.Location = new Point(295, 362);
            BtnVerify.Name = "BtnVerify";
            BtnVerify.Size = new Size(164, 52);
            BtnVerify.TabIndex = 29;
            BtnVerify.Text = "验证";
            BtnVerify.UseVisualStyleBackColor = true;
            BtnVerify.Click += BtnVerify_Click;
            // 
            // BtnImport
            // 
            BtnImport.Location = new Point(560, 25);
            BtnImport.Name = "BtnImport";
            BtnImport.Size = new Size(158, 52);
            BtnImport.TabIndex = 30;
            BtnImport.Text = "导入密钥文件";
            BtnImport.UseVisualStyleBackColor = true;
            BtnImport.Click += BtnImport_Click;
            // 
            // AuthForm
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(groupBox2);
            MaximizeBox = false;
            Name = "AuthForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "授权";
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Label label9;
        private TextBox txtKey;
        private GroupBox groupBox2;
        private Button BtnVerify;
        private Button BtnImport;
    }
}