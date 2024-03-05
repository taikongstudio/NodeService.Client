using JobsWorker.Shared.DataModels;
using Microsoft.EntityFrameworkCore;
// add a reference to System.ComponentModel.DataAnnotations DLL
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Hosting;
using Namotion.Reflection;
using Microsoft.Extensions.Logging.Debug;


namespace JobsWorkerWebService.Data
{
    public class ApplicationDbContext : DbContext
    {
        public static readonly LoggerFactory DebugLoggerFactory = new LoggerFactory(new[] {
            new DebugLoggerProvider()
        });

        public DbSet<NodeConfigTemplateModel> NodeConfigTemplateDbSet { get; set; }

        public DbSet<NodeInfoModel> NodeInfoDbSet { get; set; }

        public DbSet<PluginConfigModel> PluginConfigDbSet { get; set; }

        public DbSet<NodePropertySnapshotModel> NodePropsDbSet { get; set; }

        public DbSet<NodeProfileModel> NodeProfilesDbSet { get; set; }

        public DbSet<LocalDirectoryMappingConfigModel> LocalDirectoryMappingConfigsDbSet { get; set; }

        public DbSet<JobExecutionInstanceModel> JobExecutionInstancesDbSet { get; set; }

        public DbSet<FtpConfigModel> FtpConfigsDbSet { get; set; }

        public DbSet<MysqlConfigModel> MysqlConfigsDbSet { get; set; }

        public DbSet<KafkaConfigModel> KafkaConfigsDbSet { get; set; }

        public DbSet<LogUploadConfigModel> LogUploadConfigsDbSet { get; set; }

        public DbSet<FtpUploadConfigModel> FtpUploadConfigsDbSet { get; set; }

        public DbSet<JobScheduleConfigModel> JobScheduleConfigsDbSet { get; set; }

        public DbSet<NodePropertySnapshotModel> NodePropertySnapshotsDbSet { get; set; }

        public DbSet<RestApiConfigModel> RestApiConfigsDbSet { get; set; }

        public DbSet<JobTypeDescConfigModel> JobTypeDescConfigsDbSet { get; set; }

        public DbSet<NodePropertySnapshotGroupModel>  NodePropertySnapshotGroupsDbSet { get; set; }

        public DbSet<NodeInfoJobScheduleConfigBindingModel>  NodeInfoJobScheduleConfigBindingsDbSet { get; set; }


        public DbSet<JobScheduleConfigTemplateBindingModel> JobScheduleConfigTemplateBindingsDbSet { get; set; }

        public DbSet<Dictionary<string, object>> PropertyBagDbSet => Set<Dictionary<string, object>>("PropertyBag");

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> contextOptions)
            : base(contextOptions)
        {

        }

        protected ApplicationDbContext(DbContextOptions contextOptions)
            : base(contextOptions)
        {

        }

        private static string Serialize<T>(T? value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return JsonSerializer.Serialize<T>(value, default(JsonSerializerOptions));
        }

        private static T Deserialize<T>(string value) where T : class, new()
        {
            if (string.IsNullOrEmpty(value))
            {
                return new T();
            }
            return JsonSerializer.Deserialize<T>(value, default(JsonSerializerOptions));
        }

        private ValueComparer<IEnumerable<T>> GetEnumerableComparer<T>()
        {
            var comparer = new ValueComparer<IEnumerable<T>>(
                 (r, l) => r.SequenceEqual(l),
                 x => x.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                 x => x
                 );
            return comparer;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                if (Debugger.IsAttached)
                {
                    optionsBuilder.UseLoggerFactory(DebugLoggerFactory).UseSqlServer();
                }
            }
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.SharedTypeEntity<Dictionary<string, object>>("PropertyBag", c =>
            {
                c.IndexerProperty(typeof(string), "Id");
                c.IndexerProperty(typeof(string), "Key").IsRequired();
                c.IndexerProperty(typeof(DateTime), "CreatedDate");
                c.IndexerProperty(typeof(string), "Value");

            });

            BuildFtpConfigModel(modelBuilder);
            BuildKafkaConfigModel(modelBuilder);
            BuildFtpUploadConfigModel(modelBuilder);
            BuildLogUploadConfigModel(modelBuilder);
            BuildLocalDirectoryMappingConfigModel(modelBuilder);
            BuildJobScheduleConfigModel(modelBuilder);
            BuildNodeConfigTemplateModel(modelBuilder);
            BuildNodeInfoModel(modelBuilder);
            BuildNodeProfileModel(modelBuilder);
            BuildNodePropertySnapshotModel(modelBuilder);
            BuildPluginConfigModel(modelBuilder);
            BuildRestApiConfigModel(modelBuilder);
            BuildJobExecutionInstanceModel(modelBuilder);
            BuildJobTypeDescConfigModel(modelBuilder);

            BuildFtpConfigTemplateBindingModel(modelBuilder);
            BuildFtpUploadConfigTemplateBindingModel(modelBuilder);
            BuildJobScheduleConfigTemplateBindingModel(modelBuilder);
            BuildLogUploadConfigTemplateBindingModel(modelBuilder);
            BuildMysqlConfigTemplateBindingModel(modelBuilder);
            BuildRestApiConfigTemplateBindingModel(modelBuilder);
            BuildLocalDirectoryMappingConfigTemplateBindingModel(modelBuilder);
            BuildPluginConfigTemplateBindingModel(modelBuilder);
            BuildNodePropertySnapshotGroupModel(modelBuilder);
            BuildNodeInfoJobScheduleConfigBindingModel(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }

        private void BuildNodeInfoJobScheduleConfigBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodeInfoJobScheduleConfigBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });


            modelBuilder.Entity<NodeInfoJobScheduleConfigBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.NodeInfoJobScheduleConfigBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey)
                .IsRequired();

            modelBuilder.Entity<NodeInfoJobScheduleConfigBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.NodeInfoJobScheduleConfigBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey)
                .IsRequired();


            modelBuilder.Entity<JobScheduleConfigTemplateBindingModel>()
                .Navigation(x => x.Target)
                .AutoInclude();

            modelBuilder.Entity<JobScheduleConfigTemplateBindingModel>()
                .Navigation(x => x.Owner)
                .AutoInclude();
        }

        private void BuildNodePropertySnapshotGroupModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodePropertySnapshotGroupModel>()
                .HasKey(t => t.Id);

            modelBuilder.Entity<NodePropertySnapshotGroupModel>()
                .HasMany(x => x.Snapshots)
                .WithOne(x => x.Group)
                .HasForeignKey(x => x.GroupForeignKey)
                .IsRequired();

            modelBuilder.Entity<NodePropertySnapshotGroupModel>()
                .HasOne(x => x.NodeInfo)
                .WithMany(x => x.PropertySnapshotGroups)
                .HasForeignKey(x => x.NodeInfoForeignKey)
                .IsRequired();

        }



        private void BuildKafkaConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<KafkaConfigModel>()
                .HasKey(nameof(KafkaConfigModel.Id));

            modelBuilder.Entity<KafkaConfigModel>()
                .Property(x => x.Topics)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());
        }

        private void BuildJobTypeDescConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JobTypeDescConfigModel>()
                .HasKey(nameof(JobTypeDescConfigModel.Id));

            modelBuilder.Entity<JobTypeDescConfigModel>()
                .HasMany(x => x.JobScheduleConfigs)
                .WithOne(x => x.JobTypeDesc)
                .HasForeignKey(x => x.JobTypeDescForeignKey)
                .IsRequired(false);
            modelBuilder.Entity<JobTypeDescConfigModel>()
                .Property(x => x.OptionEditors)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());

            modelBuilder.Entity<JobTypeDescConfigModel>()
                .Navigation(x => x.JobScheduleConfigs)
                .AutoInclude();

        }

        private void BuildLogUploadConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogUploadConfigModel>()
                .HasKey(nameof(LogUploadConfigModel.Id));

            modelBuilder.Entity<LogUploadConfigModel>()
                .Property(x => x.LocalDirectories)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());

        }

        private void BuildPluginConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PluginConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<PluginConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.PluginConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<PluginConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);
        }

        private void BuildLocalDirectoryMappingConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LocalDirectoryMappingConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<LocalDirectoryMappingConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.LocalDirectoryMappingConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<LocalDirectoryMappingConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);

        }

        private void BuildRestApiConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RestApiConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<RestApiConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.RestApiConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<RestApiConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);
        }

        private void BuildMysqlConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MysqlConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<MysqlConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.MysqlConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<MysqlConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);
        }

        private void BuildLogUploadConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LogUploadConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<LogUploadConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.LogUploadConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<LogUploadConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);
        }

        private void BuildJobScheduleConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JobScheduleConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<JobScheduleConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.JobScheduleConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<JobScheduleConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);

            modelBuilder.Entity<JobScheduleConfigTemplateBindingModel>()
                .Navigation(x => x.Target)
                .AutoInclude();

            modelBuilder.Entity<JobScheduleConfigTemplateBindingModel>()
                .Navigation(x => x.Owner)
                .AutoInclude();
        }

        private void BuildFtpUploadConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FtpUploadConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<FtpUploadConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.FtpUploadConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<FtpUploadConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);


        }

        private void BuildFtpConfigTemplateBindingModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FtpConfigTemplateBindingModel>()
                .HasKey(t => new { t.OwnerForeignKey, t.TargetForeignKey });

            modelBuilder.Entity<FtpConfigTemplateBindingModel>()
                .HasOne(pt => pt.Owner)
                .WithMany(p => p.FtpConfigTemplateBindingList)
                .HasForeignKey(pt => pt.OwnerForeignKey);

            modelBuilder.Entity<FtpConfigTemplateBindingModel>()
                .HasOne(pt => pt.Target)
                .WithMany(t => t.TemplateBindingList)
                .HasForeignKey(pt => pt.TargetForeignKey);
        }

        private static void BuildRestApiConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RestApiConfigModel>()
                .HasKey(nameof(RestApiConfigModel.Id));
        }

        private static void BuildJobExecutionInstanceModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JobExecutionInstanceModel>()
                .HasKey(nameof(JobExecutionInstanceModel.Id));

          
        }

        private static void BuildPluginConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PluginConfigModel>()
                .HasKey(nameof(PluginConfigModel.Id));
        }

        private void BuildNodePropertySnapshotModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodePropertySnapshotModel>()
                .HasKey(nameof(NodePropertySnapshotModel.Id));

            modelBuilder.Entity<NodePropertySnapshotModel>()
                .Property(x => x.NodeProperties)
                .HasConversion(x => Serialize(x), x => Deserialize<List<NodePropertyEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<NodePropertyEntry>());



        }

        private static void BuildNodeProfileModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodeProfileModel>()
                .HasKey(nameof(NodeProfileModel.Id));

        }

        private static void BuildNodeInfoModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodeInfoModel>()
                .HasKey(nameof(NodeInfoModel.Id));

            modelBuilder.Entity<NodeInfoModel>()
                .HasMany(x => x.NodeInfoJobScheduleConfigBindingList)
                .WithOne(x => x.Owner)
                .HasForeignKey(x => x.OwnerForeignKey)
                .IsRequired();

            modelBuilder.Entity<NodeInfoModel>()
                .HasOne(x => x.Profile)
                .WithOne()
                .HasForeignKey<NodeInfoModel>(x => x.ProfileForeignKey)
                .IsRequired();

            modelBuilder.Entity<NodeInfoModel>()
                .HasMany(e => e.PropertySnapshotGroups)
                .WithOne(x => x.NodeInfo)
                .HasForeignKey(x => x.NodeInfoForeignKey)
                .IsRequired();


            modelBuilder.Entity<NodeInfoModel>()
                .HasMany(pt => pt.JobExecutionInstances)
                .WithOne(t => t.NodeInfo)
                .HasForeignKey(pt => pt.NodeInfoForeignKey)
                .IsRequired();

            modelBuilder.Entity<NodeInfoModel>()
                .HasMany(e => e.JobScheduleConfigs)
                .WithMany(e => e.NodeInfoList)
                .UsingEntity<NodeInfoJobScheduleConfigBindingModel>(
                    l => l.HasOne<JobScheduleConfigModel>(e => e.Target).WithMany(e => e.NodeInfoJobScheduleConfigBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeInfoModel>(e => e.Owner).WithMany(e => e.NodeInfoJobScheduleConfigBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeInfoModel>()
                  .HasMany(e => e.JobScheduleConfigs)
                .WithMany(e => e.NodeInfoList)
                .UsingEntity<NodeInfoJobScheduleConfigBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

        }

        private static void BuildNodeConfigTemplateModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasKey(nameof(NodeConfigTemplateModel.Id));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.FtpConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<FtpConfigTemplateBindingModel>(
                    l => l.HasOne<FtpConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.FtpConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.FtpConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<FtpConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.MysqlConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<MysqlConfigTemplateBindingModel>(
                    l => l.HasOne<MysqlConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.MysqlConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.MysqlConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<MysqlConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.LogUploadConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<LogUploadConfigTemplateBindingModel>(
                    l => l.HasOne<LogUploadConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.LogUploadConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.LogUploadConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<LogUploadConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.KafkaConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<KafkaConfigTemplateBindingModel>(
                    l => l.HasOne<KafkaConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.KafkaConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.KafkaConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<KafkaConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.JobScheduleConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<JobScheduleConfigTemplateBindingModel>(
                    l => l.HasOne<JobScheduleConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.JobScheduleConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.JobScheduleConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<JobScheduleConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.PluginConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<PluginConfigTemplateBindingModel>(
                    l => l.HasOne<PluginConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.PluginConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.PluginConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<PluginConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.RestApiConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<RestApiConfigTemplateBindingModel>(
                    l => l.HasOne<RestApiConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.RestApiConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.RestApiConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<RestApiConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.FtpUploadConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<FtpUploadConfigTemplateBindingModel>(
                    l => l.HasOne<FtpUploadConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.FtpUploadConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.FtpUploadConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<FtpUploadConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));


            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.LocalDirectoryMappingConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<LocalDirectoryMappingConfigTemplateBindingModel>(
                    l => l.HasOne<LocalDirectoryMappingConfigModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
                    r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.LocalDirectoryMappingConfigTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(e => e.LocalDirectoryMappingConfigs)
                .WithMany(e => e.Templates)
                .UsingEntity<LocalDirectoryMappingConfigTemplateBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            //modelBuilder.Entity<NodeConfigTemplateModel>()
            //    .HasMany(e => e.Nodes)
            //    .WithMany(e => e.NodeConfigTemplates)
            //    .UsingEntity<NodeConfigTemplateNodeInfoBindingModel>(
            //        l => l.HasOne<NodeInfoModel>(e => e.Target).WithMany(e => e.TemplateBindingList).HasForeignKey(e => e.TargetForeignKey),
            //        r => r.HasOne<NodeConfigTemplateModel>(e => e.Owner).WithMany(e => e.NodeInfoTemplateBindingList).HasForeignKey(e => e.OwnerForeignKey));

            //modelBuilder.Entity<NodeConfigTemplateModel>()
            //    .HasMany(e => e.Nodes)
            //    .WithMany(e => e.NodeConfigTemplates)
            //    .UsingEntity<NodeConfigTemplateNodeInfoBindingModel>(x => x.Property(e => e.PublicationDate).HasDefaultValueSql("CURRENT_TIMESTAMP"));

            modelBuilder.Entity<NodeConfigTemplateModel>()
                .HasMany(x => x.Nodes)
                .WithOne(x => x.ActiveNodeConfigTemplate)
                .HasForeignKey(x => x.ActiveNodeConfigTemplateForeignKey)
                .IsRequired(false);


        }

        private void BuildLocalDirectoryMappingConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LocalDirectoryMappingConfigModel>()
                .HasKey(nameof(LocalDirectoryMappingConfigModel.Id));

            modelBuilder.Entity<LocalDirectoryMappingConfigModel>()
                .Property(x => x.Entries)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());

            modelBuilder.Entity<LocalDirectoryMappingConfigModel>()
                .HasMany(x => x.FtpUploadConfigs)
                .WithOne(x => x.LocalDirectoryMappingConfig)
                .HasForeignKey(x => x.LocalDirectoryMappingConfigForeignKey)
                .IsRequired(false);

        }

        private void BuildFtpUploadConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FtpUploadConfigModel>()
                .HasKey(nameof(FtpUploadConfigModel.Id));

            modelBuilder.Entity<FtpUploadConfigModel>()
                .Property(x => x.Filters)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());
            
        }

        private static void BuildFtpConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FtpConfigModel>()
                .HasKey(nameof(FtpConfigModel.Id));

            modelBuilder.Entity<FtpConfigModel>()
                .HasMany(x => x.FtpUploadConfigBindingList)
                .WithOne(x => x.FtpConfig)
                .HasForeignKey(x => x.FtpConfigForeignKey)
                .IsRequired();

            modelBuilder.Entity<FtpConfigModel>()
                .HasMany(x => x.LogUploadConfigBindingList)
                .WithOne(x => x.FtpConfig)
                .HasForeignKey(x => x.FtpConfigForeignKey)
                .IsRequired();


        }

        private void BuildJobScheduleConfigModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JobScheduleConfigModel>()
                .HasKey(nameof(JobScheduleConfigModel.Id));

            modelBuilder.Entity<JobScheduleConfigModel>()
                .Property(x => x.CronExpressions)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());

            modelBuilder.Entity<JobScheduleConfigModel>()
                .Property(x => x.Options)
                .HasConversion(x => Serialize(x), x => Deserialize<Dictionary<string, object?>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<KeyValuePair<string, object?>>());

            modelBuilder.Entity<JobScheduleConfigModel>()
                .Property(x => x.DnsFilters)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());

            modelBuilder.Entity<JobScheduleConfigModel>()
                .Property(x => x.IpAddressFilters)
                .HasConversion(x => Serialize(x), x => Deserialize<List<StringEntry>>(x))
                .Metadata
                .SetValueComparer(GetEnumerableComparer<StringEntry>());

            modelBuilder.Entity<JobScheduleConfigModel>()
                .Navigation(x => x.JobTypeDesc)
                .AutoInclude();

            //modelBuilder.Entity<TaskScheduleConfigModel>()
            //    .Property(x => x.PropertyBag)
            //    .HasConversion(x => Serialize(x), x => Deserialize<Dictionary<string, string>>(x))
            //    .Metadata
            //    .SetValueComparer(GetEnumerableComparer<KeyValuePair<string, string>>());
        }


    }
}
