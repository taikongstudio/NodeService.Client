using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Pages.CommonConfig
{


    public class OptionEditors
    {
        static OptionEditors()
        {
            Types = System.Enum.GetNames(typeof(OptionValueType));
        }


        public static string[] Types { get; set; }

    }

    public abstract class EditorBase
    {
        public string Name { get; set; }
        public bool IsImplement { get; set; }

        public OptionValueType EditorType { get; set; }
    }

    public class TypedEditor<T> : EditorBase
    {
        public T? Value { get; set; }

    }

    public abstract class TypedCollectionEditor<T> : TypedEditor<List<T>>
    {
        public IEnumerable<T> SelectedItems { get; set; } = new List<T>();

        public abstract void AddNewItem();

        public abstract void RemoveSelectedItems();
    }

    public class TextArrayValueEditor : TypedCollectionEditor<StringEntry>
    {
        public TextArrayValueEditor(List<StringEntry>? stringEntries = null)
        {
            this.Value = stringEntries ?? new List<StringEntry?>();
            this.EditorType = OptionValueType.TextArrayValue;
        }




        public override void AddNewItem()
        {
            var entry = new StringEntry()
            {
                Id = Guid.NewGuid().ToString(),
            };
            this.Value.Add(entry);
            entry.BeginEdit();
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
   
        }

    }

    public class BooleanValueEditor : TypedEditor<bool>
    {
        public BooleanValueEditor(bool value=false)
        {
            this.Value = value;
            this.EditorType = OptionValueType.BooleanValue;
        }
    }

    public class NumberValueEditor : TypedEditor<double>
    {
        public NumberValueEditor(double value = 0)
        {
            this.Value = value;
            this.EditorType = OptionValueType.NumberValue;
        }
    }

    public class TextValueEditor : TypedEditor<string>
    {
        public TextValueEditor(string? value=null)
        {
            this.Value = value;
            this.EditorType = OptionValueType.TextValue;
        }
    }

    public class ScriptCodeEditor : TypedEditor<string>
    {
        public ScriptCodeEditor(string? scriptCode = null)
        {
            this.Value = scriptCode;
            this.EditorType = OptionValueType.ScriptCodeValue;
        }
    }

    public class FtpConfigEditor : TypedEditor<FtpConfigModel>
    {
        public FtpConfigEditor(FtpConfigModel? ftpConfig=null)
        {
            this.Value = ftpConfig;
            this.EditorType = OptionValueType.FtpConfigValue;
        }
    }

    public class MysqlConfigEditor : TypedEditor<MysqlConfigModel>
    {
        public MysqlConfigEditor(MysqlConfigModel? mysqlConfig = null)
        {
            this.Value = mysqlConfig;
            this.EditorType = OptionValueType.MysqlConfigValue;
        }
    }

    public class KafkaConfigEditor : TypedEditor<KafkaConfigModel>
    {
        public KafkaConfigEditor(KafkaConfigModel? kafkaConfig = null)
        {
            this.Value = kafkaConfig;
            this.EditorType = OptionValueType.KafkaConfigValue;
        }
    }
    public class FtpUploadConfigEditor : TypedEditor<FtpUploadConfigModel>
    {
        public FtpUploadConfigEditor(FtpUploadConfigModel? ftpUploadConfig = null)
        {
            this.Value = ftpUploadConfig;
            this.EditorType = OptionValueType.FtpUploadConfigValue;
        }
    }

    public class LocalDirectoryMappingConfigEditor : TypedEditor<LocalDirectoryMappingConfigModel>
    {
        public LocalDirectoryMappingConfigEditor(LocalDirectoryMappingConfigModel?  localDirectoryMappingConfig = null)
        {
            this.Value = localDirectoryMappingConfig;
            this.EditorType = OptionValueType.LocalDirectoryMappingConfigValue;
        }
    }

    public class LogUploadConfigEditor : TypedEditor<LogUploadConfigModel>
    {
        public LogUploadConfigEditor(LogUploadConfigModel?  logUploadConfig = null)
        {
            this.Value = logUploadConfig;
            this.EditorType = OptionValueType.LogUploadConfigValue;
        }
    }

    public class PluginConfigEditor : TypedEditor<PluginConfigModel>
    {
        public PluginConfigEditor(PluginConfigModel? pluginConfig = null)
        {
            this.Value = pluginConfig;
            this.EditorType = OptionValueType.PluginConfigValue;
        }
    }

    public class PluginConfigListEditor : TypedCollectionEditor<PluginConfigModel>
    {
        public PluginConfigListEditor(List<PluginConfigModel>? values = null)
        {
            this.Value = values ?? [];
            this.EditorType = OptionValueType.PluginConfigListValue;
        }

        public IEnumerable<PluginConfigModel> Configs { get; set; } = [];

        public PluginConfigModel? SelectedConfig { get; set; }

        public override void AddNewItem()
        {
            if (this.SelectedConfig == null)
            {
                return;
            }
            this.Value.Add(this.SelectedConfig);
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
        }
    }


    public class FtpConfigListEditor : TypedCollectionEditor<FtpConfigModel>
    {
        public FtpConfigListEditor(List<FtpConfigModel>? values = null)
        {
            this.Value = values ?? [];
            this.EditorType = OptionValueType.FtpConfigListValue;
        }

        public IEnumerable<FtpConfigModel> Configs { get; set; } = [];

        public FtpConfigModel? SelectedConfig { get; set; }

        public override void AddNewItem()
        {
            if (this.SelectedConfig == null)
            {
                return;
            }
            this.Value.Add(this.SelectedConfig);
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
        }
    }

    public class MysqlConfigListEditor : TypedCollectionEditor<MysqlConfigModel>
    {
        public MysqlConfigListEditor(List<MysqlConfigModel>? values = null)
        {
            this.Value = values ?? [];
            this.EditorType = OptionValueType.MysqlConfigListValue;
        }

        public IEnumerable<MysqlConfigModel> Configs { get; set; } = [];

        public MysqlConfigModel? SelectedConfig { get; set; }


        public override void AddNewItem()
        {
            if (this.SelectedConfig == null)
            {
                return;
            }
            this.Value.Add(this.SelectedConfig);
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
        }
    }

    public class KafkaConfigListEditor : TypedCollectionEditor<KafkaConfigModel>
    {
        public KafkaConfigListEditor(List<KafkaConfigModel>? values = null)
        {
            this.Value = values ?? [];
            this.EditorType = OptionValueType.KafkaConfigListValue;
        }

        public IEnumerable<KafkaConfigModel> Configs { get; set; } = [];

        public KafkaConfigModel? SelectedConfig { get; set; }


        public override void AddNewItem()
        {
            if (this.SelectedConfig == null)
            {
                return;
            }
            this.Value.Add(this.SelectedConfig);
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
        }
    }

    public class FtpUploadConfigListEditor : TypedCollectionEditor<FtpUploadConfigModel>
    {
        public FtpUploadConfigListEditor(List<FtpUploadConfigModel>? stringEntries = null)
        {
            this.Value = stringEntries ?? [];
            this.EditorType = OptionValueType.FtpUploadConfigListValue;
        }

        public IEnumerable<FtpUploadConfigModel> Configs { get; set; } = [];

        public FtpUploadConfigModel? SelectedConfig { get; set; }


        public override void AddNewItem()
        {
            if (this.SelectedConfig == null)
            {
                return;
            }
            this.Value.Add(this.SelectedConfig);
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
        }
    }

    public class LogUploadConfigListEditor : TypedCollectionEditor<LogUploadConfigModel>
    {
        public LogUploadConfigListEditor(List<LogUploadConfigModel>? values = null)
        {
            this.Value = values ?? [];
            this.EditorType = OptionValueType.LogUploadConfigListValue;
        }

        public IEnumerable<LogUploadConfigModel> Configs { get; set; } = [];

        public LogUploadConfigModel? SelectedConfig { get; set; }


        public override void AddNewItem()
        {
            if (this.SelectedConfig == null)
            {
                return;
            }
            this.Value.Add(this.SelectedConfig);
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
        }
    }

    public class LocalDirectoryMappingConfigListEditor : TypedCollectionEditor<LocalDirectoryMappingConfigModel>
    {
        public LocalDirectoryMappingConfigListEditor(List<LocalDirectoryMappingConfigModel>? values = null)
        {
            this.Value = values ?? [];
            this.EditorType = OptionValueType.LocalDirectoryMappingConfigListValue;
        }

        public IEnumerable<LocalDirectoryMappingConfigModel> Configs { get; set; } = [];

        public LocalDirectoryMappingConfigModel? SelectedConfig { get; set; }


        public override void AddNewItem()
        {
            if (this.SelectedConfig == null)
            {
                return;
            }
            this.Value.Add(this.SelectedConfig);
        }

        public override void RemoveSelectedItems()
        {
            foreach (var item in this.SelectedItems)
            {
                this.Value.Remove(item);
            }
        }
    }


}
