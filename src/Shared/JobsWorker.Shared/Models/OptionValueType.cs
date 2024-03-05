using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public enum OptionValueType
    {
        BooleanValue,
        NumberValue,
        TextValue,
        ScriptCodeValue,
        TextArrayValue,
        FtpConfigValue,
        MysqlConfigValue,
        KafkaConfigValue,
        FtpConfigListValue,
        MysqlConfigListValue,
        KafkaConfigListValue,
        FtpUploadConfigValue,
        FtpUploadConfigListValue,
        LogUploadConfigValue,
        LogUploadConfigListValue,
        PluginConfigValue,
        PluginConfigListValue,
        RestApiConfigValue,
        RestApiConfigListValue,
        LocalDirectoryMappingConfigValue,
        LocalDirectoryMappingConfigListValue,
    }
}
