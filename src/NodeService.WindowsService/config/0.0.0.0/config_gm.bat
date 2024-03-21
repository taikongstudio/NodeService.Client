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
      "broker_list": "172.27.242.225:9092,172.27.242.226:9092,172.27.242.227:9092",
      "header_topic_name": "marcor_header",
      "time_data_topic_name": "marcor_time_data",
      "maxDegreeOfParallelism": "1",
      "mysql_host": "172.27.242.221",
      "mysql_database": "data_count",
      "mysql_userid": "root",
      "mysql_password": "KNk3DRxA#=b3",
      "dbName": "working_file_records.db"
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
      "server": "172.27.242.224",
      "username": "xwdgmuser",
      "password": "xwdgm@2023",
      "mysql_host": "172.27.242.221",
      "mysql_database": "data_count",
      "mysql_userid": "root",
      "mysql_password": "KNk3DRxA#=b3",
      "factory_name": "光明",
      "lockSeconds": "1200",
      "configExpireMinutes": "60"
    }
  },
  {
    "jobName": "JobsWorker.Jobs.ExecuteScriptJob",
    "cronExpressions": [
      "0 10 23 * * ? *"
    ],
    "isEnabled": false,
    "hostNameFilterType": "include",
    "hostNameFilters": [],
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
      "ctrc-032"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/kpr_gm.bat",
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
      "ctrc-129",
      "ctrc-103",
      "ctrc-055",
      "ctrc-044",
      "ctrc-033",
      "ctrc-057",
      "ctrc-037",
      "ctrc-108",
      "ctrc-043",
      "ctrc-051",
      "ctrc-317",
      "ctrc-003",
      "ctrc-056",
      "ctrc-017",
      "ctrc-021",
      "ctrc-106",
      "ctrc-004",
      "ctrc-048",
      "D6C06"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/ret_gm.bat",
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
      "ctrc-305",
      "ctrc-037"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/xjc_gm.bat",
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
      "ctrc-005",
      "ctrc-129",
      "ctrc-103"
    ],
    "arguments": {
      "scripts": "ftptool.exe --rsp $(WorkingDirectory)/plugins/ftptool/0.0.0.0/config/fzwc_gm.bat",
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
      "D8L86",
      "ctrc-110",
      "ctrc-019",
      "D6C16",
      "CTRC-016",
      "CTRC-307",
      "CTRC-167",
      "ctrc-208",
      "CTRC-020",
      "ctrc-186",
      "ctrc-228",
      "CTRC-232",
      "CTRC-010",
      "CTRC-008",
      "ctrc-017",
      "ctrc-009",
      "ctrc-091",
      "ctrc-030",
      "CTRC-035",
      "CTRC-033",
      "ctrc-007",
      "ctrc-005",
      "CTRC-043",
      "ctrc-034",
      "ctrc-h02",
      "ctrc-144",
      "CTRC-054",
      "CTRC-063",
      "D22244",
      "L2n514",
      "CTRC-426",
      "D6C20",
      "CTRC-045",
      "CTRC-228",
      "CTRC-h02",
      "CTRC-151",
      "CTRC-053",
      "CTRC-122",
      "D7689",
      "P5C03",
      "ctrc-332",
      "CTRC-013",
      "ctrc-993",
      "ctrc-039",
      "CTRC-028"
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
      "CTRC-035"
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
      "Kafka-BootstrapServers": "172.27.242.225:9092,172.27.242.226:9092,172.27.242.227:9092",
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
    "isEnabled": true
  },
  {
    "jobName": "JobsWorker.Jobs.UploadAppLogsToFtpServerJob",
    "cronExpressions": [
      "0 30 * * * ? *"
    ],
    "hostNameFilterType": "exclude",
    "hostNameFilters": [],
    "arguments": {
      "server": "172.27.242.224",
      "username": "xwdgmuser",
      "password": "xwdgm@2023",
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