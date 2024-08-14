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
            components = new System.ComponentModel.Container();
            label9 = new Label();
            txtKey = new TextBox();
            groupBox2 = new GroupBox();
            BtnImport = new Button();
            BtnVerify = new Button();
            btnRefresh = new Button();
            progressBar1 = new ProgressBar();
            dataGridView1 = new DataGridView();
            isSelectedDataGridViewCheckBoxColumn = new DataGridViewCheckBoxColumn();
            nameDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            idDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            Usages = new DataGridViewTextBoxColumn();
            lastOnlineTimeDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            nodeInfoBindingSource = new BindingSource(components);
            label1 = new Label();
            btnNewNode = new Button();
            lblStatus = new Label();
            txtKeyword = new TextBox();
            label2 = new Label();
            btnSearch = new Button();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nodeInfoBindingSource).BeginInit();
            SuspendLayout();
            // 
            // label9
            // 
            label9.Location = new Point(0, 28);
            label9.Name = "label9";
            label9.Size = new Size(142, 36);
            label9.TabIndex = 28;
            label9.Text = "请输入密钥";
            label9.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtKey
            // 
            txtKey.Location = new Point(6, 78);
            txtKey.Multiline = true;
            txtKey.Name = "txtKey";
            txtKey.PasswordChar = '*';
            txtKey.Size = new Size(1072, 157);
            txtKey.TabIndex = 0;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(BtnImport);
            groupBox2.Controls.Add(BtnVerify);
            groupBox2.Controls.Add(label9);
            groupBox2.Controls.Add(txtKey);
            groupBox2.Location = new Point(12, 406);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(1090, 302);
            groupBox2.TabIndex = 29;
            groupBox2.TabStop = false;
            groupBox2.Text = "授权验证";
            // 
            // BtnImport
            // 
            BtnImport.Location = new Point(920, 20);
            BtnImport.Name = "BtnImport";
            BtnImport.Size = new Size(158, 52);
            BtnImport.TabIndex = 30;
            BtnImport.Text = "导入密钥文件";
            BtnImport.UseVisualStyleBackColor = true;
            BtnImport.Click += BtnImport_Click;
            // 
            // BtnVerify
            // 
            BtnVerify.Location = new Point(465, 241);
            BtnVerify.Name = "BtnVerify";
            BtnVerify.Size = new Size(164, 52);
            BtnVerify.TabIndex = 29;
            BtnVerify.Text = "验证";
            BtnVerify.UseVisualStyleBackColor = true;
            BtnVerify.Click += BtnVerify_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(650, 12);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(186, 50);
            btnRefresh.TabIndex = 31;
            btnRefresh.Text = "刷新节点匹配信息";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(18, 181);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(1072, 34);
            progressBar1.TabIndex = 32;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.BackgroundColor = SystemColors.Control;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { isSelectedDataGridViewCheckBoxColumn, nameDataGridViewTextBoxColumn, idDataGridViewTextBoxColumn, Usages, lastOnlineTimeDataGridViewTextBoxColumn });
            dataGridView1.DataSource = nodeInfoBindingSource;
            dataGridView1.GridColor = SystemColors.ControlLight;
            dataGridView1.Location = new Point(12, 119);
            dataGridView1.MultiSelect = false;
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersWidth = 62;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new Size(1090, 281);
            dataGridView1.TabIndex = 33;
            dataGridView1.SelectionChanged += dataGridView1_SelectionChanged;
            // 
            // isSelectedDataGridViewCheckBoxColumn
            // 
            isSelectedDataGridViewCheckBoxColumn.DataPropertyName = "IsSelected";
            isSelectedDataGridViewCheckBoxColumn.HeaderText = "使用此纪录";
            isSelectedDataGridViewCheckBoxColumn.MinimumWidth = 8;
            isSelectedDataGridViewCheckBoxColumn.Name = "isSelectedDataGridViewCheckBoxColumn";
            isSelectedDataGridViewCheckBoxColumn.ReadOnly = true;
            isSelectedDataGridViewCheckBoxColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            isSelectedDataGridViewCheckBoxColumn.Width = 150;
            // 
            // nameDataGridViewTextBoxColumn
            // 
            nameDataGridViewTextBoxColumn.DataPropertyName = "Name";
            nameDataGridViewTextBoxColumn.HeaderText = "名称";
            nameDataGridViewTextBoxColumn.MinimumWidth = 8;
            nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
            nameDataGridViewTextBoxColumn.ReadOnly = true;
            nameDataGridViewTextBoxColumn.Width = 200;
            // 
            // idDataGridViewTextBoxColumn
            // 
            idDataGridViewTextBoxColumn.DataPropertyName = "Id";
            idDataGridViewTextBoxColumn.HeaderText = "编号";
            idDataGridViewTextBoxColumn.MinimumWidth = 8;
            idDataGridViewTextBoxColumn.Name = "idDataGridViewTextBoxColumn";
            idDataGridViewTextBoxColumn.ReadOnly = true;
            idDataGridViewTextBoxColumn.Width = 200;
            // 
            // Usages
            // 
            Usages.DataPropertyName = "Usages";
            Usages.HeaderText = "用途";
            Usages.MinimumWidth = 8;
            Usages.Name = "Usages";
            Usages.ReadOnly = true;
            Usages.Width = 200;
            // 
            // lastOnlineTimeDataGridViewTextBoxColumn
            // 
            lastOnlineTimeDataGridViewTextBoxColumn.DataPropertyName = "LastOnlineTime";
            lastOnlineTimeDataGridViewTextBoxColumn.HeaderText = "最后在线日期";
            lastOnlineTimeDataGridViewTextBoxColumn.MinimumWidth = 8;
            lastOnlineTimeDataGridViewTextBoxColumn.Name = "lastOnlineTimeDataGridViewTextBoxColumn";
            lastOnlineTimeDataGridViewTextBoxColumn.ReadOnly = true;
            lastOnlineTimeDataGridViewTextBoxColumn.Width = 200;
            // 
            // nodeInfoBindingSource
            // 
            nodeInfoBindingSource.DataSource = typeof(NodeInfo);
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 25);
            label1.Name = "label1";
            label1.Size = new Size(280, 24);
            label1.TabIndex = 34;
            label1.Text = "选择原有节点信息或新建节点信息";
            // 
            // btnNewNode
            // 
            btnNewNode.Location = new Point(860, 12);
            btnNewNode.Name = "btnNewNode";
            btnNewNode.Size = new Size(239, 50);
            btnNewNode.TabIndex = 35;
            btnNewNode.Text = "使用新的节点信息";
            btnNewNode.UseVisualStyleBackColor = true;
            btnNewNode.Click += btnNewNode_Click;
            // 
            // lblStatus
            // 
            lblStatus.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblStatus.ForeColor = Color.DodgerBlue;
            lblStatus.Location = new Point(18, 236);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(1072, 82);
            lblStatus.TabIndex = 36;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // txtKeyword
            // 
            txtKeyword.Location = new Point(220, 81);
            txtKeyword.Name = "txtKeyword";
            txtKeyword.Size = new Size(303, 30);
            txtKeyword.TabIndex = 37;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(16, 84);
            label2.Name = "label2";
            label2.Size = new Size(172, 24);
            label2.TabIndex = 38;
            label2.Text = "输入上位机名称查询";
            // 
            // btnSearch
            // 
            btnSearch.Location = new Point(552, 77);
            btnSearch.Name = "btnSearch";
            btnSearch.Size = new Size(112, 34);
            btnSearch.TabIndex = 39;
            btnSearch.Text = "查询";
            btnSearch.UseVisualStyleBackColor = true;
            btnSearch.Click += btnSearch_Click;
            // 
            // AuthForm
            // 
            AcceptButton = BtnVerify;
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1111, 720);
            Controls.Add(btnSearch);
            Controls.Add(label2);
            Controls.Add(txtKeyword);
            Controls.Add(lblStatus);
            Controls.Add(btnNewNode);
            Controls.Add(label1);
            Controls.Add(progressBar1);
            Controls.Add(btnRefresh);
            Controls.Add(dataGridView1);
            Controls.Add(groupBox2);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Name = "AuthForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "授权";
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)nodeInfoBindingSource).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label9;
        private TextBox txtKey;
        private GroupBox groupBox2;
        private Button BtnVerify;
        private Button BtnImport;
        private Button btnRefresh;
        private ProgressBar progressBar1;
        private DataGridView dataGridView1;
        private BindingSource nodeInfoBindingSource;
        private Label label1;
        private DataGridViewCheckBoxColumn isSelectedDataGridViewCheckBoxColumn;
        private DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn idDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn Usages;
        private DataGridViewTextBoxColumn lastOnlineTimeDataGridViewTextBoxColumn;
        private Button btnNewNode;
        private Label lblStatus;
        private TextBox txtKeyword;
        private Label label2;
        private Button btnSearch;
    }
}