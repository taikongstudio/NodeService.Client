using FluentFTP;
using Microsoft.Win32;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using System.Data;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace NodeService.Installer
{
    public partial class AuthForm : Form
    {
        private NodeInfo? _selectedNodeInfo;
        private List<NodeInfoModel> _matchedNodeInfoList;

        public AuthForm()
        {
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            await QueryNodeInfoListByExtendInfoAsync();

            base.OnLoad(e);
        }

        public string? GetIdentity()
        {
            try
            {
                const string ServiceName = "NodeService.WindowsService";
                using var softwareSubKey = Registry.LocalMachine.OpenSubKey("SOFTWARE", true);
                var subKeyNames = softwareSubKey.GetSubKeyNames();
                RegistryKey nodeRegisty = null;
                if (!subKeyNames.Any(x => x == ServiceName))
                {
                    nodeRegisty = softwareSubKey.CreateSubKey(ServiceName, true);
                }
                else
                {
                    nodeRegisty = softwareSubKey.OpenSubKey(ServiceName, true);
                }
                string nodeIdentity = null;
                nodeIdentity = nodeRegisty.GetValue(nameof(NodeClientHeaders.NodeId)) as string;
                nodeRegisty.Dispose();
                return nodeIdentity;
            }
            catch (Exception)
            {

            }
            return null;
        }

        private async Task QueryNodeInfoListByExtendInfoAsync()
        {
            try
            {

                Invoke(() =>
                {
                    BtnVerify.Enabled = false;
                    progressBar1.Visible = true;
                    progressBar1.Style = ProgressBarStyle.Marquee;
                    dataGridView1.Visible = true;
                    dataGridView1.Enabled = false;
                    lblStatus.Visible = true;
                    //nodeCheckList.SelectedIndex = -1;
                });
                using var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri("http://10.201.76.21:50060"),
                    Timeout = TimeSpan.FromMinutes(5)
                };
                using var apiService = new ApiService(httpClient);

                List<NodeInfoModel> nodeInfoList = [];

                Invoke(() =>
                {
                    this.nodeInfoBindingSource.Clear();
                });

                var pageIndex = 1;
                while (true)
                {
                    var queryNodeRsp = await apiService.QueryNodeListAsync(new QueryNodeListParameters()
                    {
                        AreaTag = AreaTags.Any,
                        DeviceType = NodeDeviceType.Computer,
                        PageIndex = pageIndex,
                        PageSize = 50,
                        Status = NodeStatus.All
                    });
                    if (queryNodeRsp.ErrorCode == 0)
                    {
                        if (!queryNodeRsp.Result.Any())
                        {
                            break;
                        }
                        Invoke(() =>
                        {
                            foreach (var item in queryNodeRsp.Result)
                            {
                                nodeInfoBindingSource.Add(new NodeInfo()
                                {
                                    Id = item.Id,
                                    Name = item.Name,
                                    LastOnlineTime = item.Profile.ServerUpdateTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                                    Usages = item.Profile.Usages,
                                    IsSelected = false
                                });
                                nodeInfoList.Add(item);
                            }
                            foreach (var item in nodeInfoBindingSource)
                            {
                                (item as NodeInfo).IsSelected = false;
                            }

                            progressBar1.Style = ProgressBarStyle.Continuous;
                            progressBar1.Value = (int)(nodeInfoList.Count / (queryNodeRsp.TotalCount + 0d) * 100);
                            lblStatus.Text = $"正在查询节点信息：{nodeInfoList.Count}/{queryNodeRsp.TotalCount}";
                        });
                    }


                    if (queryNodeRsp.Result.Count() < 50)
                    {
                        break;
                    }
                    pageIndex++;
                }

                var nodes = await GetNodeInfoListAsync(apiService);
                Invoke(() =>
                {
                    lblStatus.Text = $"正在查询匹配信息";
                });
                Invoke(() =>
                {

                    if (nodes != null && nodes.Any())
                    {
                        _matchedNodeInfoList = nodes.ToList();
                        foreach (var item in nodes)
                        {
                            var index = 0;
                            foreach (var nodeInfo in nodeInfoList)
                            {
                                if (item.Id == nodeInfo.Id)
                                {
                                    if (nodeInfoBindingSource[index] is not NodeInfo nodeInfoData)
                                    {
                                        continue;
                                    }
                                    _selectedNodeInfo = nodeInfoData;
                                    _selectedNodeInfo.IsSelected = true;
                                    dataGridView1.FirstDisplayedScrollingRowIndex = index;
                                    break;
                                }
                                index++;
                            }
                        }

                    }
                    else
                    {
                        _selectedNodeInfo = new NodeInfo()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Dns.GetHostName(),
                        };
                    }
                    this.BtnVerify.Enabled = true;
                    dataGridView1.Enabled = true;
                    lblStatus.Visible = false;
                });
            }
            catch (Exception ex)
            {
                Invoke(() =>
                {
                    MessageBox.Show(this, ex.ToString());
                    this.BtnVerify.Enabled = false;
                    lblStatus.Visible = false;
                });
            }
            finally
            {
                Invoke(() =>
                {
                    progressBar1.Visible = false;
                    lblStatus.Visible = false;
                });
            }

        }

        async Task<IEnumerable<NodeInfoModel>> GetNodeInfoListAsync(ApiService apiService)
        {
            try
            {
                var nodeExtendInfo = new NodeExtendInfo();
                nodeExtendInfo.CpuInfoList = GetCpuInfoList().ToList();
                nodeExtendInfo.BIOSInfoList = GetBIOSInfoList().ToList();
                nodeExtendInfo.PhysicalMediaInfoList = GetPhysicalMediaInfoList().ToList();
                var rsp = await apiService.QueryNodeInfoListByExtendInfoAsync(nodeExtendInfo);
                return rsp.Result;
            }
            catch (Exception ex)
            {
                Invoke(() =>
                {
                    MessageBox.Show(ex.ToString());
                });
            }

            return [];
        }

        public IEnumerable<CpuInfo> GetCpuInfoList()
        {
            using var searcher = new ManagementObjectSearcher("Select * From Win32_Processor");
            foreach (ManagementObject mo in searcher.Get().Cast<ManagementObject>())
            {
                var cpuInfo = new CpuInfo()
                {
                    SerialNumber = mo["ProcessorId"].ToString().Trim()
                };
                yield return cpuInfo;
            }
            yield break;
        }


        //获取主板序列号
        public IEnumerable<BIOSInfo> GetBIOSInfoList()
        {
            using var searcher = new ManagementObjectSearcher("Select * From Win32_BIOS");
            foreach (ManagementObject mo in searcher.Get().Cast<ManagementObject>())
            {
                var biosInfo = new BIOSInfo
                {
                    SerialNumber = mo.GetPropertyValue("SerialNumber").ToString().Trim()
                };
                yield return biosInfo;
            }
            yield break;
        }


        //获取硬盘序列号
        public IEnumerable<PhysicalMediaInfo> GetPhysicalMediaInfoList()
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMedia");
            foreach (ManagementObject mo in searcher.Get().Cast<ManagementObject>())
            {
                var physicalMediaInfo = new PhysicalMediaInfo()
                {
                    SerialNumber = mo["SerialNumber"].ToString().Trim(),
                };
                yield return physicalMediaInfo;
            }
            yield break;
        }

        private async void BtnVerify_Click(object sender, EventArgs e)
        {
            const string KeyFileName = "NodeService.WebServer/keys/install.key";
            try
            {
                if (_matchedNodeInfoList == null)
                {
                    _matchedNodeInfoList = [];
                }
                using var ftpClient = new AsyncFtpClient(
                                    "172.27.242.223",
                                    "xwdgmuser",
                                    "xwdgm@2023",
                                    21
                                    );
                if (!await ftpClient.FileExists(KeyFileName))
                {
                    MessageBox.Show(this, "从服务器获取密钥失败");
                    return;
                }
                var bytes = await ftpClient.DownloadBytes(KeyFileName, default);
                var str = Encoding.UTF8.GetString(bytes);
                if (str == this.txtKey.Text)
                {
                    if (_selectedNodeInfo != null)
                    {
                        if (!_selectedNodeInfo.IsNew && _matchedNodeInfoList.Count > 0 && !_matchedNodeInfoList.Any(x => x.Id == _selectedNodeInfo.Id))
                        {
                            if (MessageBox.Show(this, $"选择的节点信息不在历史匹配信息内，确定使用{_selectedNodeInfo.Id}作为节点ID？", "", MessageBoxButtons.OKCancel) != DialogResult.OK)
                            {
                                return;
                            }
                        }
                    }
                    UpdateNodeId();
                    this.DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show(this, "密钥错误");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString());
            }

        }

        private void UpdateNodeId()
        {
            if (_selectedNodeInfo == null)
            {
                return;
            }
            const string ServiceName = "NodeService.WindowsService";
            using var softwareSubKey = Registry.LocalMachine.OpenSubKey("SOFTWARE", true);
            var subKeyNames = softwareSubKey.GetSubKeyNames();
            RegistryKey nodeRegisty = null;
            if (!subKeyNames.Any(x => x == ServiceName))
            {
                nodeRegisty = softwareSubKey.CreateSubKey(ServiceName, true);
            }
            else
            {
                nodeRegisty = softwareSubKey.OpenSubKey(ServiceName, true);
            }
            string nodeIdentity = _selectedNodeInfo.Id;
            nodeRegisty.SetValue(nameof(NodeClientHeaders.NodeId), nodeIdentity);
            nodeIdentity = nodeRegisty.GetValue(nameof(NodeClientHeaders.NodeId)) as string;
            nodeRegisty.Dispose();
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

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await QueryNodeInfoListByExtendInfoAsync();
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            foreach (DataGridViewRow item in dataGridView1.Rows)
            {
                if (item.DataBoundItem is not NodeInfo nodeInfo)
                {
                    continue;
                }
                nodeInfo.IsSelected = false;
            }
            foreach (DataGridViewRow item in dataGridView1.SelectedRows)
            {
                if (item.DataBoundItem is not NodeInfo nodeInfo)
                {
                    continue;
                }
                nodeInfo.IsSelected = true;
                _selectedNodeInfo = nodeInfo;
                break;
            }
            dataGridView1.Invalidate();
        }

        private void btnNewNode_Click(object sender, EventArgs e)
        {
            dataGridView1.Visible = false;
            foreach (var item in nodeInfoBindingSource)
            {
                if (item is not NodeInfo nodeInfo)
                {
                    continue;
                }
                nodeInfo.IsSelected = false;
            }
            _selectedNodeInfo = new NodeInfo()
            {
                Id = Guid.NewGuid().ToString(),
                Name = Dns.GetHostName(),
                IsNew = true,
            };
            lblStatus.Visible = true;
            lblStatus.Text = $"节点ID:{_selectedNodeInfo.Id}";
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            var keyword = this.txtKeyword.Text;
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show(this, "请输入关键字");
                return;
            }
            var index = 0;
            foreach (DataGridViewRow item in dataGridView1.Rows)
            {
                if (item.DataBoundItem is not NodeInfo nodeInfo)
                {
                    continue;
                }
                var isMatched = nodeInfo.Name != null && nodeInfo.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                if (isMatched)
                {
                    dataGridView1.FirstDisplayedScrollingRowIndex = index;
                    break;
                }
                index++;
            }
        }

    }
}
