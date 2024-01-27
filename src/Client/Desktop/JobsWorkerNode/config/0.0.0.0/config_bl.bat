[
  {
    "jobName": "JobsWorker.Jobs.UploadMaccorDataJob",
    "cronExpressions": [
      "40 0/10 * * * ?"
    ],
    "isEnabled": true,
    "hostNameFilterType": "exclude",
    "hostNameFilters": [],
    "arguments": {
      "path": "",
      "broker_list": "10.201.71.249:9092",
      "header_topic_name": "marcor_header",
      "time_data_topic_name": "marcor_time_data",
      "maxDegreeOfParallelism": "1",
      "mysql_host": "172.27.241.153",
      "mysql_database": "data_count",
      "mysql_userid": "root",
      "mysql_password": "Szch@2023",
      "dbName":"testdb20240125.db"
    }
  },
    {
    "jobName": "JobsWorker.Jobs.UpdateStatusJob",
    "cronExpressions": [
      "40 0/30 * * * ?"
    ],
    "isEnabled": true,
    "hostNameFilterType": "exclude",
    "hostNameFilters": [],
    "arguments": {
      "server": "172.27.241.153",
      "username": "ftpuser",
      "password": "Szch@2023",
      "mysql_host": "172.27.241.153",
      "mysql_database": "data_count",
      "mysql_userid": "root",
      "mysql_password": "Szch@2023",
      "factory_name":"博罗",
      "lockSeconds":"1200",
      "configExpireMinutes":"60"
    }
  },
  {
    "jobName": "JobsWorker.Jobs.ExecuteScriptJob",
    "cronExpressions": [
      "0 10 23 * * ? *"
    ],
    "isEnabled": false,
    "hostNameFilterType": "include",
    "hostNameFilters": [
        "ctrc-198",
        "ctrc-419", 
        "ctrc-322", 
        "ctrc-140", 
        "ctrc-182", 
        "ctrc-184", 
        "ctrc-209", 
        "ctrc-207", 
        "ctrc-395", 
        "ctrc-400", 
        "ctrc-397", 
        "ctrc-373", 
        "ctrc-539", 
        "ctrc-h87", 
        "ctrc-h81"
    ],
    "arguments": {
      "scripts": "C:/Windows/System32/taskkill.exe /F /IM NWReport_DBWB.exe",
      "workingDirectory": "",
      "createNoWindow": "true"
    }
  },
  {
    "jobName": "JobsWorker.Jobs.ExecuteScriptJob",
    "cronExpressions": [
      "0 0 01 * * ? *"
    ],
    "isEnabled": true,
    "hostNameFilterType": "include",
    "hostNameFilters": [
      "ywx-pc",
      "DESKTOP-CS5RBC9",
      "ctrc-127",
      "ctrc-539",
      "ctrc-368",
      "ctrc-352",
      "ctrc-222",
      "ctrc-158",
      "h49"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/kpr_bl.bat",
      "workingDirectory": "$(WorkingDirectory)/plugins/ftptool/0.0.0.0/",
      "createNoWindow": "true"
    }
  },
  {
    "jobName": "JobsWorker.Jobs.ExecuteScriptJob",
    "cronExpressions": [
      "0 0 01 * * ? *"
    ],
    "isEnabled": true,
    "hostNameFilterType": "include",
    "hostNameFilters": [
      "ywx-pc",
      "DESKTOP-CS5RBC9",
      "ctrc-210"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/ret_bl.bat",
      "workingDirectory": "$(WorkingDirectory)/plugins/ftptool/0.0.0.0/",
      "createNoWindow": "true"
    }
  },
  {
    "jobName": "JobsWorker.Jobs.ExecuteScriptJob",
    "cronExpressions": [
      "0 0 01 * * ? *"
    ],
    "isEnabled": true,
    "hostNameFilterType": "include",
    "hostNameFilters": [
      "ywx-pc",
      "DESKTOP-CS5RBC9",
      "ctrc-217",
      "ctrc-178"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/xjc_bl.bat",
      "workingDirectory": "$(WorkingDirectory)/plugins/ftptool/0.0.0.0/",
      "createNoWindow": "true"
    }
  },
  {
    "jobName": "JobsWorker.Jobs.ExecuteScriptJob",
    "cronExpressions": [
      "0 15 0 * * ? *"
    ],
    "isEnabled": true,
    "hostNameFilterType": "include",
    "hostNameFilters": [
      "ywx-pc",
      "DESKTOP-CS5RBC9",
      "ctrc-539",
      "ctrc-399",
      "ctrc-398",
      "ctrc-132",
      "ctrc-131",
      "ctrc-116",
      "h38"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/fzwc_bl.bat",
      "workingDirectory": "$(WorkingDirectory)/plugins/ftptool/0.0.0.0/",
      "createNoWindow": "true"
    }
  },
  {
    "jobName": "JobsWorker.Jobs.DetectProcessAndClickButtonJob",
    "cronExpressions": [
      "0 30 23 * * ? *",
      "0 10 0 * * ? *",
      "0 10 9 * * ? *",
      "0 10 10 * * ? *",
      "0 10 11 * * ? *",
      "0 10 16 * * ? *",
      "0 10 17 * * ? *",
      "0 10 18 * * ? *",
      "0 10 19 * * ? *",
      "0 10 20 * * ? *",
      "0 10 21 * * ? *"
    ],
    "hostNameFilterType": "include",
    "hostNameFilters": [
        "ctrc-198",
        "ctrc-419", 
        "ctrc-322", 
        "ctrc-140", 
        "ctrc-182", 
        "ctrc-184", 
        "ctrc-209", 
        "ctrc-207", 
        "ctrc-395", 
        "ctrc-400", 
        "ctrc-397", 
        "ctrc-373", 
        "ctrc-539", 
        "ctrc-h87", 
        "ctrc-h81"
    ],
    "arguments": {
      "ButtonText": "启动",
      "exePath": "D:/NWReport_DBWB_V1.0.0_20230915_64/NWReport_DBWB.exe",
      "processName": "NWReport_DBWB",
      "workingDirectory": "D:/NWReport_DBWB_V1.0.0_20230915_64"
    },
    "isEnabled": true
  },
  {
    "jobName": "JobsWorker.Jobs.DetectProcessAndClickButtonJob",
    "cronExpressions": [
      "0 30 23 * * ? *",
      "0 10 0 * * ? *",
      "0 10 9 * * ? *",
      "0 10 10 * * ? *",
      "0 10 11 * * ? *",
      "0 10 16 * * ? *",
      "0 10 17 * * ? *",
      "0 10 18 * * ? *",
      "0 10 19 * * ? *",
      "0 10 20 * * ? *",
      "0 10 21 * * ? *"
    ],
    "hostNameFilterType": "include",
    "hostNameFilters": [
        "ctrc-198",
        "ctrc-419", 
        "ctrc-322", 
        "ctrc-140", 
        "ctrc-182", 
        "ctrc-184", 
        "ctrc-209", 
        "ctrc-207", 
        "ctrc-395", 
        "ctrc-400", 
        "ctrc-397", 
        "ctrc-373", 
        "ctrc-539", 
        "ctrc-h87", 
        "ctrc-h81"
    ],
    "arguments": {
      "ButtonText": "启动",
      "exePath": "E:/NWReport_DBWB_V1.0.0_20230915_64/NWReport_DBWB.exe",
      "processName": "NWReport_DBWB",
      "workingDirectory": "E:/NWReport_DBWB_V1.0.0_20230915_64"
    },
    "isEnabled": true
  },
  {
    "jobName": "JobsWorker.Jobs.DetectProcessAndShowMessageBoxJob",
    "cronExpressions": [
      "0 30 0 * * ? *"
    ],
    "hostNameFilterType": "exclude",
    "hostNameFilters": [],
    "arguments": {
      "swj_software_regex": "NWReport_DBWB",
      "check_window_warning_time": "5"
    },
    "isEnabled": false
  },
  {
    "jobName": "JobsWorker.Jobs.ShouHuUploadJob",
    "cronExpressions": [
      "0 0/5 * * * ? *"
    ],
    "hostNameFilterType": "exclude",
    "hostNameFilters": [],
    "arguments": {
      "Kafka-BootstrapServers": "10.201.71.249:9092",
      "swj_flag": "",
      "swj_vendor": "未知",
      "swj_other_info": "",
      "swj_ips_regex": "\\b(?:10|172)\\.\\d+\\.\\d+\\.\\d+\\b",
      "swj_software_regex": "",
      "windows_service_name": "JobsWorkerDaemonServiceWindowsService",
      "check_window_warning_time": ""
    },
    "isEnabled": true
  },
  {
    "jobName": "JobsWorker.Jobs.GCJob",
    "cronExpressions": [
      "0 30 * * * ? *"
    ],
    "isEnabled": true,
    "arguments":{"GCEnabled":"false"}
  },
  {
    "jobName": "JobsWorker.Jobs.UploadAppLogsToFtpServerJob",
    "cronExpressions": [
      "0 30 * * * ? *"
    ],
    "hostNameFilterType": "exclude",
    "hostNameFilters": [],
    "arguments": {
      "server": "172.27.241.153",
      "username": "ftpuser",
      "password": "Szch@2023",
      "appName": "xinwei",
      "localLogDirectory": "D:/NWReport_DBWB_V1.0.0_20230915_64/LogData",
      "searchPattern": "*.log",
      "remoteLogDirectoryFormat": "JobsWorkerAppLogs/xinwei/{0}/logs",
      "timespanhour": "72",
      "sizelimitbytes": "102400000"
    },
    "isEnabled": true
  }
]