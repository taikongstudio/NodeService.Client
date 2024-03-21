using FluentFTP;
using System.IO.Compression;
using System.Text.Json;

namespace NodeService.Installer
{
    public partial class MainForm : Form, IProgress<FtpProgress>
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private const string DefaultInstallConfigPath = "InstallConfig.json.config";

        private InstallConfig[] _installConfigs = [];
        private InstallConfig? _selectedInstallConfig;

        private void btnImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = AppContext.BaseDirectory;
            openFileDialog.Filter = "json|*.json";
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                ReadConfigFromFile(openFileDialog.FileName);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Application.Exit();
            base.OnFormClosed(e);
        }

        private void ReadConfigFromFile(string path)
        {
            try
            {
                var jsonText = File.ReadAllText(path);
                this._installConfigs = JsonSerializer.Deserialize<InstallConfig[]>(jsonText);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"从\"{path}\"加载配置时发生了错误:{ex.Message}");
            }

        }

        private void ReadConfigFromStream(Stream stream)
        {
            try
            {
                this._installConfigs = JsonSerializer.Deserialize<InstallConfig[]>(stream);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"从\"流\"加载配置时发生了错误:{ex.Message}");
            }

        }

        private async void BtnDownload_Click(object sender, EventArgs e)
        {
            DisableControls();
            _selectedInstallConfig = this._installConfigs.ElementAtOrDefault(cmbConfigs.SelectedIndex);
            if (_selectedInstallConfig == null)
            {
                _selectedInstallConfig = new InstallConfig()
                {
                    Host = this.txtHost.Text,
                    Password = this.txtPassword.Text,
                    Port = (int)this.NumericUpDownPort.Value,
                    Username = this.txtUserName.Text,
                    PackagePath = this.txtPath.Text,
                    InstallPath = this.txtInstallPath.Text,
                    EntryPoint = this.cmbEntryPoint.Text,
                    ServiceName = this.txtServiceName.Text,

                };
            }
            else
            {
                _selectedInstallConfig.Host = this.txtHost.Text;
                _selectedInstallConfig.Password = this.txtPassword.Text;
                _selectedInstallConfig.Port = (int)this.NumericUpDownPort.Value;
                _selectedInstallConfig.Username = this.txtUserName.Text;
                _selectedInstallConfig.PackagePath = this.txtPath.Text;
                _selectedInstallConfig.InstallPath = this.txtInstallPath.Text;
                _selectedInstallConfig.EntryPoint = this.cmbEntryPoint.Text;
                _selectedInstallConfig.ServiceName = this.txtServiceName.Text;
            }
            var tempFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                using var ftpClient = new AsyncFtpClient(
                    _selectedInstallConfig.Host,
                    _selectedInstallConfig.Username,
                    _selectedInstallConfig.Password,
                    _selectedInstallConfig.Port
                    );
                if ((await ftpClient.FileExists(_selectedInstallConfig.PackagePath)) == false)
                {
                    MessageBox.Show("服务器不存在此文件");
                    return;
                }
                using var stream = File.Open(tempFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);


                if (!await ftpClient.DownloadStream(stream, _selectedInstallConfig.PackagePath, progress: this))
                {
                    MessageBox.Show("下载安装包失败");
                    return;
                }

                Uninstall();

                if (!CleanupInstallDirectory(_selectedInstallConfig))
                {
                    return;
                }

                stream.Position = 0;
                if (!Extract(_selectedInstallConfig, stream))
                {
                    MessageBox.Show($"解压到\"{_selectedInstallConfig.InstallPath}\"失败");
                    return;
                }
                Install();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                EnableControls();
                if (File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                }
            }


        }

        private void DisableControls()
        {
            this.groupConfig.Enabled = false;
            this.BtnDownload.Enabled = false;
            this.BtnInstall.Enabled = false;
            this.BtnUninstall.Enabled = false;
            this.btnImport.Enabled = false;
        }

        private void EnableControls()
        {
            this.groupConfig.Enabled = true;
            this.BtnDownload.Enabled = true;
            this.BtnInstall.Enabled = true;
            this.BtnUninstall.Enabled = true;
            this.btnImport.Enabled = true;
        }

        private static bool Extract(InstallConfig? installConfig, FileStream stream)
        {
            try
            {
                ZipFile.ExtractToDirectory(stream, installConfig.InstallPath, true);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解压文件到{installConfig.InstallPath}时发生了错误:{ex.Message}");
            }
            return false;
        }

        private bool CleanupInstallDirectory(InstallConfig installConfig)
        {
            try
            {
                var installDirectory = new DirectoryInfo(installConfig.InstallPath);
                if (!installDirectory.Exists)
                {
                    return true;
                }
                foreach (var item in installDirectory.GetFileSystemInfos())
                {
                    if (Directory.Exists(item.FullName))
                    {
                        Directory.Delete(item.FullName, true);
                    }
                    else
                    {
                        item.Delete();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清理目录\"{installConfig.PackagePath}\"时发生了错误:{ex.Message}");
            }
            return false;
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            Install();

        }

        private void Install()
        {
            try
            {
                if (this._selectedInstallConfig == null)
                {
                    return;
                }
                ServiceHelper.Install(_selectedInstallConfig.ServiceName,
                  _selectedInstallConfig.ServiceName,
                  _selectedInstallConfig.EntryPoint,
                  _selectedInstallConfig.ServiceName,
                    ServiceStartType.AutoStart
                  );
                if (!ServiceHelper.StartService(_selectedInstallConfig.ServiceName, _selectedInstallConfig.EntryPoint))
                {
                    MessageBox.Show("安装失败");
                    return;
                }
                MessageBox.Show("安装成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            UninstallAll();

        }

        private void UninstallAll()
        {
            Uninstall();
            if (this._selectedInstallConfig == null)
            {
                return;
            }
            const string ServiceName = "NodeService.WindowsService";
            if (!ServiceHelper.Uninstall(ServiceName))
            {
                MessageBox.Show($"卸载服务\"{ServiceName}\"失败");
                return;
            }
            MessageBox.Show("卸载成功");
        }

        private void Uninstall()
        {
            if (this._selectedInstallConfig == null)
            {
                return;
            }
            if (!ServiceHelper.Uninstall(_selectedInstallConfig.ServiceName))
            {
                MessageBox.Show($"卸载服务\"{_selectedInstallConfig.ServiceName}\"失败");
                return;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                var form = new AuthForm();
                form.Owner = this;
                if (form.ShowDialog() != DialogResult.OK)
                {
                    Environment.Exit(0);
                    return;
                }
                var resourceStream = typeof(MainForm).Assembly.GetManifestResourceStream("NodeService.Installer.InstallConfig.json.config");
                if (resourceStream != null)
                {
                    ReadConfigFromStream(resourceStream);
                }
                else
                {
                    this._installConfigs = [new InstallConfig {
                         ConfigName="临时配置",
                         Port=21
                    }];
                }
                this.cmbConfigs.DataSource = this._installConfigs;
                this.cmbConfigs.DisplayMember = "ConfigName";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载默认配置时发生了错误:{ex.Message}");
            }

        }

        public void Report(FtpProgress value)
        {
            this.ProgressBar.Invoke(() =>
            {
                this.ProgressBar.Value = (int)value.Progress;
            });
        }

        private void cmbConfigs_SelectedIndexChanged(object sender, EventArgs e)
        {
            this._selectedInstallConfig = (InstallConfig)cmbConfigs.SelectedItem;
            SelectConfig(this._selectedInstallConfig);
        }

        private void SelectConfig(InstallConfig installConfig)
        {
            if (installConfig == null)
            {
                this.txtHost.Text = string.Empty;
                this.txtUserName.Text = string.Empty;
                this.txtPassword.Text = string.Empty;
                this.NumericUpDownPort.Value = 21;
                this.txtServiceName.Text = string.Empty;
                this.txtInstallPath.Text = string.Empty;
                this.cmbEntryPoint.Text = string.Empty;
                return;
            }
            this.txtHost.Text = installConfig.Host;
            this.txtUserName.Text = installConfig.Username;
            this.txtPassword.Text = installConfig.Password;
            this.NumericUpDownPort.Value = installConfig.Port;
            this.txtPath.Text = installConfig.PackagePath;
            this.txtServiceName.Text = installConfig.ServiceName;
            this.txtInstallPath.Text = installConfig.InstallPath;
            this.cmbEntryPoint.Text = installConfig.EntryPoint;
        }

    }
}
