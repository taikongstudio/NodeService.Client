{
  "machineDirectoryMappingConfigPath": "/ftptoolconfig/machineDirectoryMapping.json",
  "defaultTaskName": "ret task",
  "ftpTasks": [
    {
      "taskName": "ret task",
      "directoryConfigName": "ret",
      "host": "172.27.242.222",
      "port": 21,
      "username": "xwdgmuser",
      "password": "xwdgm@2023",
      "localDirectory": "",
      "isLocalDirectoryUseMapping": true,
      "remoteDirectory": "/sc_yl/ret/{0}",
      "searchPattern": "*.csv",
      "includeSubDirectories": true,
      "nextTaskName": "",
      "filters": [ "" ]
    }
  ]
}