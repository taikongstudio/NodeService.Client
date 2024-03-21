using FluentFTP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NodeService.Installer
{
    public partial class AuthForm : Form
    {
        public AuthForm()
        {
            InitializeComponent();
        }

        private async void BtnVerify_Click(object sender, EventArgs e)
        {
            const string KeyFileName = "NodeService.WebServer/keys/install.key";
            try
            {
                using var ftpClient = new AsyncFtpClient(
                                    "172.27.242.223",
                                    "xwdgmuser",
                                    "xwdgm@2023",
                                    21
                                    );
                if (!await ftpClient.FileExists(KeyFileName))
                {
                    MessageBox.Show("从服务器获取密钥失败");
                    return;
                }
                var bytes = await ftpClient.DownloadBytes(KeyFileName, default);
                var str = Encoding.UTF8.GetString(bytes);
                if (str == this.txtKey.Text)
                {
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show("密钥错误");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = AppContext.BaseDirectory;
            openFileDialog.Filter = "key|*.key";
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.txtKey.Text = File.ReadAllText(openFileDialog.FileName);
            }
        }
    }
}
