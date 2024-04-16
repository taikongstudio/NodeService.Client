using FluentFTP;
using Microsoft.Win32;
using NodeService.Infrastructure;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO.Compression;
using System.Net;
using System.ServiceProcess;
using System.Text.Json;

namespace NodeService.Installer
{
    public partial class MainForm : Form, IProgress<FtpProgress>
    {
        ApiService apiService;
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
                System.Windows.Forms.MessageBox.Show($"��\"{path}\"��������ʱ�����˴���:{ex.Message}");
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
                System.Windows.Forms.MessageBox.Show($"��\"��\"��������ʱ�����˴���:{ex.Message}");
            }

        }

        private async void BtnDownload_Click(object sender, EventArgs e)
        {
            if (this.apiService != null && this.apiService.HttpClient.BaseAddress.ToString() != this.txtUri.Text)
            {
                this.apiService.Dispose();
                this.apiService = null;
            }
            this.apiService = new ApiService(new HttpClient()
            {
                BaseAddress = new Uri(this.txtUri.Text),
                Timeout = TimeSpan.FromSeconds(60)
            });
            ClearStartup();
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

            try
            {
                using var installer = UpdateServiceProcessInstaller.Create(
                    _selectedInstallConfig.ServiceName,
                    _selectedInstallConfig.ServiceName,
                    string.Empty,
                    _selectedInstallConfig.EntryPoint
                    );
                installer.SetInstallConfig(this._selectedInstallConfig);
                installer.SetFileDownloadProgressProvider(this);
                installer.ProgressChanged += Installer_ProgressChanged;
                installer.Failed += Installer_Failed;
                installer.Completed += Installer_Completed;
                await installer.RunAsync();
                installer.ProgressChanged -= Installer_ProgressChanged;
                installer.Failed -= Installer_Failed;
                installer.Completed -= Installer_Completed;

            }
            catch (Exception ex)
            {

            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                EnableControls();
            }


        }

        private async void Installer_Completed(object? sender, InstallerProgressEventArgs e)
        {
            AppendMessage(e.Progress.Message);
            Alert(e.Progress.Message);
            await UploadCounter(e);
        }

        private async Task UploadCounter(InstallerProgressEventArgs e)
        {
            try
            {
                await this.apiService.AddOrUpdateUpdateInstallCounterAsync(new Infrastructure.Models.AddOrUpdateCounterParameters()
                {
                    ClientUpdateConfigId = "Installer",
                    CategoryName = e.Progress.Message,
                    NodeName = Dns.GetHostName()
                });
            }
            catch (Exception ex)
            {
                AppendMessage($"�ϴ���װͳ�Ƶ�������ʱ�������쳣:{ex.Message}");
            }

        }

        private async void Installer_Failed(object? sender, InstallerProgressEventArgs e)
        {
            AppendMessage(e.Progress.Message);
            Alert(e.Progress.Message);
            await UploadCounter(e);
        }

        private async void Installer_ProgressChanged(object? sender, InstallerProgressEventArgs e)
        {
            AppendMessage(e.Progress.Message);
            await UploadCounter(e);
        }

        private void Alert(string errorMessage)
        {
            if (!this.InvokeRequired)
            {
                if (errorMessage == null)
                {
                    return;
                }
                MessageBox.Show(errorMessage);
            }
            else
            {
                this.Invoke(Alert, errorMessage);
            }
        }

        private void AppendMessage(string errorMessage)
        {
            if (!this.InvokeRequired)
            {
                if (errorMessage == null)
                {
                    return;
                }
                this.txtInfo.AppendText(errorMessage);
                this.txtInfo.AppendText(Environment.NewLine);
            }
            else
            {
                this.Invoke(AppendMessage, errorMessage);
            }
        }

        private void DisableControls()
        {
            this.txtInfo.Clear();
            this.groupConfig.Enabled = false;
            this.BtnDownload.Enabled = false;
            this.BtnUninstall.Enabled = false;
            this.btnImport.Enabled = false;
            this.txtInfo.Enabled = true;
            this.BtnDownload.Enabled = false;
            this.BtnUninstall.Enabled = false;
            this.txtUri.Enabled = false;

        }

        private void EnableControls()
        {
            this.groupConfig.Enabled = true;
            this.BtnDownload.Enabled = true;
            this.BtnUninstall.Enabled = true;
            this.btnImport.Enabled = true;
            this.ProgressBar.Style = ProgressBarStyle.Continuous;
            this.BtnDownload.Enabled = true;
            this.BtnUninstall.Enabled = true;
            this.txtUri.Enabled = true;
        }

        private async void BtnUninstall_Click(object sender, EventArgs e)
        {
            ClearStartup();
            this.txtInfo.Clear();
            DisableControls();
            const string UpdateServiceName = "NodeService.UpdateService";
            const string WindowsServiceName = "NodeService.WindowsService";
            const string JobsWorkerDaemonServiceName = "JobsWorkerDaemonService";
            await foreach (var progress in ServiceProcessInstallerHelper.UninstallAllService(
            [
                UpdateServiceName,
                WindowsServiceName,
                JobsWorkerDaemonServiceName
            ]))
            {
                AppendMessage(progress.Message);
                if (progress.Type == ServiceProcessInstallerProgressType.Error)
                {
                    Alert(progress.Message);
                }
            }
            EnableControls();
        }

        void ClearStartup()
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "JobsWorkerDaemonService.lnk");
                File.Delete(path);
            }
            catch (Exception ex)
            {

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
                         ConfigName="��ʱ����",
                         Port=21
                    }];
                }
                this.cmbConfigs.DataSource = this._installConfigs;
                this.cmbConfigs.DisplayMember = "ConfigName";
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"����Ĭ������ʱ�����˴���:{ex.Message}");
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
