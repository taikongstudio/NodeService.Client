using FluentFTP;
using NodeService.Infrastructure;
using NodeService.ServiceProcess;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        private const string DefaultInstallConfigPath = "InstallConfig.json";

        private PackageConfig[] _installConfigs = [];
        private PackageConfig? _selectedInstallConfig;

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

        protected override void OnClosed(EventArgs e)
        {
            Environment.Exit(0);
            base.OnClosed(e);
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
                this._installConfigs = JsonSerializer.Deserialize<PackageConfig[]>(jsonText);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"从\"{path}\"加载配置时发生了错误:{ex.Message}");
            }

        }

        private void ReadConfigFromStream(Stream stream)
        {
            try
            {
                this._installConfigs = JsonSerializer.Deserialize<PackageConfig[]>(stream);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"从\"流\"加载配置时发生了错误:{ex.Message}");
            }

        }

        private async void BtnInstall_Click(object sender, EventArgs e)
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

            await UninstallServicesAsync();

            _selectedInstallConfig = this._installConfigs.ElementAtOrDefault(cmbConfigs.SelectedIndex);
            if (_selectedInstallConfig == null)
            {
                _selectedInstallConfig = new PackageConfig()
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
                using var installer = CommonServiceProcessInstaller.Create(
                    _selectedInstallConfig.ServiceName,
                    _selectedInstallConfig.ServiceName,
                    string.Empty,
                    _selectedInstallConfig.EntryPoint,
                    Debugger.IsAttached ? "--mode WindowsService --env Development" : null
                    );
                installer.SetParameters(BuildPackageProvider(), BuildIntallContext());
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

        private PackageProvider BuildPackageProvider()
        {
            return new FtpPackageProvider(_selectedInstallConfig.Host,
                _selectedInstallConfig.Username,
                _selectedInstallConfig.Password,
                _selectedInstallConfig.PackagePath,
                this,
                _selectedInstallConfig.Port);
        }

        private ServiceProcessInstallContext BuildIntallContext()
        {
            var context = new ServiceProcessInstallContext(_selectedInstallConfig.ServiceName,
                _selectedInstallConfig.ServiceName,
                string.Empty,
                _selectedInstallConfig.InstallPath);
            return context;
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
                AppendMessage($"上传安装统计到服务器时发生了异常:{ex.Message}");
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
            this.BtnInstall.Enabled = false;
            this.BtnUninstall.Enabled = false;
            this.btnImport.Enabled = false;
            this.txtInfo.Enabled = true;
            this.BtnInstall.Enabled = false;
            this.BtnUninstall.Enabled = false;
            this.txtUri.Enabled = false;

        }

        private void EnableControls()
        {
            this.groupConfig.Enabled = true;
            this.BtnInstall.Enabled = true;
            this.BtnUninstall.Enabled = true;
            this.btnImport.Enabled = true;
            this.ProgressBar.Style = ProgressBarStyle.Continuous;
            this.BtnInstall.Enabled = true;
            this.BtnUninstall.Enabled = true;
            this.txtUri.Enabled = true;
        }

        private async void BtnUninstall_Click(object sender, EventArgs e)
        {
            ClearStartup();
            this.txtInfo.Clear();
            DisableControls();
            await UninstallServicesAsync();
            EnableControls();
        }

        private Task UninstallServicesAsync()
        {
            return Task.Run(async () =>
            {
                const string DaemonServiceName = "NodeService.DaemonService";
                const string UpdateServiceName = "NodeService.UpdateService";
                const string WindowsServiceName = "NodeService.WindowsService";
                const string WorkerServiceName = "NodeService.WorkerService";
                const string JobsWorkerDaemonServiceName = "JobsWorkerDaemonService";
                await foreach (var progress in ServiceProcessInstallerHelper.UninstallAllService(
                [
                    DaemonServiceName,
                    WorkerServiceName,
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
            });
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

        private void CheckServiceStatus()
        {
            Task.Run(CheckSericeStatusImpl);
        }

        const string WorkerServiceName = "NodeService.WorkerService";
        const string UpdateServiceName = "NodeService.UpdateService";
        const string WindowsServiceName = "NodeService.WindowsService";

        private async void CheckSericeStatusImpl()
        {
            ConcurrentDictionary<string, ServiceController> controllers = new ConcurrentDictionary<string, ServiceController>();
            while (true)
            {
                try
                {
                    string[] serviceNames = [WindowsServiceName, UpdateServiceName, WorkerServiceName];
                    foreach (var serviceName in serviceNames)
                    {
                        try
                        {
                            var serviceController = controllers.GetOrAdd(serviceName, new ServiceController(serviceName));
                            serviceController.Refresh();
                            UpdateServiceStatus(serviceName, serviceController.Status.ToString());
                        }
                        catch (Exception ex)
                        {
                            UpdateServiceStatus(serviceName, ex.Message.ToString());
                        }

                    }
                }
                catch (Exception ex)
                {

                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

        }

        private void UpdateServiceStatus(string serviceName, string status)
        {
            this.Invoke(() =>
            {
                switch (serviceName)
                {
                    case UpdateServiceName:
                        this.lblServiceStatusUpdateService.Text = $"{serviceName}:{status}";
                        if (status == "Running")
                        {
                            this.lblServiceStatusUpdateService.ForeColor = Color.Green;
                        }
                        else
                        {
                            this.lblServiceStatusUpdateService.ForeColor = Color.Red;
                        }
                        break;
                    case WindowsServiceName:
                        this.lblServiceStatusWindowsService.Text = $"{serviceName}:{status}";
                        if (status == "Running")
                        {
                            this.lblServiceStatusWindowsService.ForeColor = Color.Green;
                        }
                        else
                        {
                            this.lblServiceStatusWindowsService.ForeColor = Color.Red;
                        }
                        break;
                    case WorkerServiceName:
                        this.lblServiceStatusWorkerService.Text = $"{serviceName}:{status}";
                        this.lblServiceStatusWorkerService.Text = $"{serviceName}:{status}";
                        if (status == "Running")
                        {
                            this.lblServiceStatusWorkerService.ForeColor = Color.Green;
                        }
                        else
                        {
                            this.lblServiceStatusWorkerService.ForeColor = Color.Red;
                        }
                        break;
                    default:
                        break;
                }

            });
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                CheckServiceStatus();
                const string PackagesFileName = "/NodeService.WebServer/packages/packages.json";
                var form = new AuthForm();
                form.Owner = this;
                if (form.ShowDialog() != DialogResult.OK)
                {
                    Environment.Exit(0);
                    return;
                }
                using var ftpClient = new AsyncFtpClient(
                    "172.27.242.223",
                    "xwdgmuser",
                    "xwdgm@2023",
                    21
                    );
                await ftpClient.AutoConnect();
                if (!await ftpClient.FileExists(PackagesFileName))
                {
                    AppendMessage("服务器不存在包配置文件");
                }
                using var stream = new MemoryStream();
                if (!await ftpClient.DownloadStream(stream, PackagesFileName,progress:this))
                {
                    AppendMessage("下载包配置失败");
                }
                if (stream != null)
                {
                    AppendMessage("下载包配置成功");
                    stream.Position = 0;
                    ReadConfigFromStream(stream);
                }
                else
                {
                    this._installConfigs = [new PackageConfig {
                         ConfigName="临时配置",
                         Port=21
                    }];
                }
                this.cmbConfigs.DataSource = this._installConfigs;
                this.cmbConfigs.DisplayMember = "ConfigName";
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"加载默认配置时发生了错误:{ex.Message}");
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
            this._selectedInstallConfig = (PackageConfig)cmbConfigs.SelectedItem;
            SelectConfig(this._selectedInstallConfig);
        }

        private void SelectConfig(PackageConfig installConfig)
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
