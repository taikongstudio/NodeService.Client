{
  "machineDirectoryMappingConfigPath": "/ftptoolconfig/machineDirectoryMapping.json",
  "defaultTaskName": "xjc task",
  "ftpTasks": [
    {
      "taskName": "xjc task",
      "directoryConfigName": "xjc",
      "host": "172.27.242.222",
      "port": 21,
      "username": "xwdgmuser",
      "password": "xwdgm@2023",
      "localDirectory": "",
      "isLocalDirectoryUseMapping": true,
      "remoteDirectory": "/sc_yl/xjc/{0}",
      "searchPattern": "*.xlsx",
      "includeSubDirectories": true,
      "nextTaskName": "",
      "filters": [ "原始数据" ]
    }
  ]
}