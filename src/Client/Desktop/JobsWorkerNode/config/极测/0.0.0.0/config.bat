[
  {
    "jobName": "JobsWorkerDaemonService.Jobs.ExecuteScriptJob",
    "exePath": "",
    "processName": "",
    "cronExpressions": [
      "30 * * * * ?"
    ],
    "isEnabled": false,
    "arguments": {
      "scripts": "D:\\xwd\\echo.bat"
    }
  },
  {
    "jobName": "JobsWorkerDaemonService.Jobs.DetectProcessJob",
    "exePath": "D:/NWReport_DBWB_V1.0.0_20230915_64/NWReport_DBWB.exe",
    "processName": "NWReport_DBWB",
    "cronExpressions": [
      "0 30 0 * * ? *"
    ],
    "isEnabled": false
  },
  {
    "jobName": "JobsWorkerDaemonService.Jobs.DetectProcessAndClickButtonJob",
    "exePath": "D:/NWReport_DBWB_V1.0.0_20230915_64/NWReport_DBWB.exe",
    "processName": "NWReport_DBWB",
    "cronExpressions": [
      "0 30 23 * * ? *",
      "0 10 0 * * ? *"
    ],
    "arguments": {
      "ButtonText": "启动"
    },
    "isEnabled": false
  },
  {
    "jobName": "JobsWorkerDaemonService.Jobs.DetectProcessAndShowMessageBoxJob",
    "exePath": "",
    "processName": "",
    "cronExpressions": [
      "0 30 0 * * ? *"
    ],
    "arguments": {
      "swj_software_regex": "NWReport_DBWB",
      "check_window_warning_time": "5"
    },
    "isEnabled": false
  },
  {
    "jobName": "JobsWorkerDaemonService.Jobs.ShouHuUploadJob",
    "exePath": "",
    "processName": "",
    "cronExpressions": [
      "0 30 0 * * ? *"
    ],
    "arguments": {
      "Kafka-BootstrapServers": "10.201.71.249:9092",
      "swj_flag": "",
      "swj_vendor": "极测",
      "swj_other_info": "",
      "swj_ips_regex": "\\b(?:10|172)\\.\\d+\\.\\d+\\.\\d+\\b",
      "swj_software_regex": "",
      "windows_service_name": "JobsWorkerDaemonServiceWindowsService",
      "check_window_warning_time": ""

    },
    "isEnabled": true
  }
]