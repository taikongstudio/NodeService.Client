using AntDesign.ProLayout;
using CommandLine;
using FluentFTP;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using JobsWorker.Shared;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorkerWebService.Data;
using JobsWorkerWebService.GrpcServices;
using JobsWorkerWebService.Models.Configurations;
using JobsWorkerWebService.Services;
using JobsWorkerWebService.Services.VirtualSystem;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using NLog.Extensions.Logging;
using System.Net;
using System.Runtime.CompilerServices;
using JobsWorkerWebService;
using System.Text.Json;


public class Program
{



    public static void Main(string[] args)
    {

        Parser.Default.ParseArguments<Options>(args)
                         .WithParsed((options) =>
                         {
                             Console.WriteLine(JsonSerializer.Serialize(options));
                             if (string.IsNullOrEmpty(options.env))
                             {
                                 options.env = Environments.Development;
                             }
                             RunWithOptions(options, args);
                         });

    }

    private static void RunWithOptions(Options options, string[] args)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", options.env);

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            Configure(builder);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            Configure(app);

            app.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    static void Configure(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            // Add OpenAPI 3.0 document serving middleware
            // Available at: http://localhost:<port>/swagger/v1/swagger.json
            app.UseOpenApi();

            // Add web UIs to interact with the document
            // Available at: http://localhost:<port>/swagger
            app.UseSwaggerUi((uiSettings) =>
            {


            });
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });


        MapGrpcServices(app);

        //app.UseHttpsRedirection();
        app.UseHsts();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseCors("AllowAll");

        app.UseEndpoints(req =>
        {

        });

        app.MapRazorPages();
        app.MapControllers();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        using (var serviceScope = app.Services.GetService<IServiceScopeFactory>().CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }
    }

    private static void MapGrpcServices(WebApplication app)
    {
        app.MapGrpcService<FileSystemServiceImpl>().EnableGrpcWeb().RequireCors("AllowAll");
        app.MapGrpcService<NodeServiceImpl>().EnableGrpcWeb().RequireCors("AllowAll");
    }

    static void Configure(WebApplicationBuilder builder)
    {

        builder.Services.AddControllersWithViews();
        builder.Services.AddControllers();
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddAntDesign();
        builder.Services.AddOpenApiDocument();
        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 1024 * 1024 * 1024;
        });
     

        //builder.Services.AddGrpcClient<FileSystem.FileSystemClient>(options =>
        //    {
        //        var httpsEndpointUrl = builder.Configuration.GetValue<string>("Kestrel:Endpoints:MyHttpsEndpoint:Url");
        //        options.Address = new Uri(httpsEndpointUrl);
        //    })
        //    .ConfigurePrimaryHttpMessageHandler(
        //        () => new GrpcWebHandler(new HttpClientHandler() { }));

        builder.Services.AddLogging(logger =>
        {
            logger.ClearProviders();
            logger.AddConsole();
            logger.AddNLog();
        });

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(
                builder.Configuration.GetConnectionString("Sqlite"), (optionsBuilder) =>
                {

                }));

        }
        else
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(builder.Configuration.GetConnectionString("MySQL"),
            MySqlServerVersion.LatestSupportedServerVersion, mySqlOptionBuilder =>
            {
                mySqlOptionBuilder.EnableStringComparisonTranslations();
            }));
        }

        builder.Services.AddDbContext<ApplicationProfileDbContext>(options => {
            options.UseMySql(builder.Configuration.GetConnectionString("MyProfileSQL"),
            MySqlServerVersion.LatestSupportedServerVersion, mySqlOptionBuilder =>
            {
                mySqlOptionBuilder.EnableStringComparisonTranslations();
            });
            options.EnableThreadSafetyChecks(true);
        });



        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri(sp.GetService<NavigationManager>()!.BaseUri)
        });

        builder.Services.Configure<ProSettings>(builder.Configuration.GetSection("ProSettings"));
        builder.Services.AddScoped<IChartService, ChartService>();
        builder.Services.AddScoped<IProjectService, ProjectService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IAccountService, AccountService>();
        builder.Services.AddScoped<IProfileService, ProfileService>();
        builder.Services.AddScoped<IHeartBeatResponseHandler, HeartBeatResponseHandler>();

        builder.Services.AddScoped<AsyncFtpClient>(serviceProvider =>
        {
            var ftpConnectConfig = serviceProvider.GetRequiredService<FtpServerConfig>();
            return new AsyncFtpClient(ftpConnectConfig.host,
                ftpConnectConfig.username,
                ftpConnectConfig.password,
                ftpConnectConfig.port);
        });

        builder.Services.AddScoped<FileSystem.FileSystemClient>(serviceProvider =>
        {
            var httpsEndpointUrl = builder.Configuration.GetValue<string>("Kestrel:Endpoints:MyHttpsEndpoint:Url");
            var requestUri = new Uri(httpsEndpointUrl);
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            var channel = GrpcChannel.ForAddress(requestUri, new GrpcChannelOptions()
            {
                HttpHandler = handler,
                Credentials = ChannelCredentials.SecureSsl
            });
            return new FileSystem.FileSystemClient(channel);
        });

        builder.Services.AddSingleton<FtpServerConfig>(new FtpServerConfig()
        {
            host = builder.Configuration.GetSection("FtpServerConfig")["Host"],
            port = int.Parse(builder.Configuration.GetSection("FtpServerConfig")["Port"]),
            username = builder.Configuration.GetSection("FtpServerConfig")["Username"],
            password = builder.Configuration.GetSection("FtpServerConfig")["Password"],
            rootDirectory = builder.Configuration.GetSection("FtpServerConfig")["rootDirectory"],
        });

        builder.Services.AddSingleton<ServerConfig>(new ServerConfig()
        {
            Channel = builder.Configuration.GetSection("ServerConfig")["Channel"],
            VirtualFileSystem = builder.Configuration.GetSection("ServerConfig")["VirtualFileSystem"]
        });

        builder.Services.AddSingleton<VirtualFileSystemConfig>(new VirtualFileSystemConfig()
        {
            nodeConfigPathFormat = builder.Configuration.GetSection("VirtualFileSystemConfig")["fileCachesPathDir"],
            fileCachesPathDir = builder.Configuration.GetSection("VirtualFileSystemConfig")["fileCachesPathDir"],
            pluginPathFormat = builder.Configuration.GetSection("VirtualFileSystemConfig")["pluginPathFormat"],
            RequestUri = builder.Configuration.GetValue<string>("Kestrel:Endpoints:MyHttpEndpoint:Url")
        });

        builder.Services.AddScoped<IVirtualFileSystem>(serviceProvider =>
        {
            var serverConfig = serviceProvider.GetService<ServerConfig>();
            switch (serverConfig.VirtualFileSystem)
            {
                case "ftp":
                    var ftpClient = serviceProvider.GetService<AsyncFtpClient>();
                    return new FtpVirtualFileSystem(ftpClient);
                    break;
                default:
                    return null;
                    break;
            }
        });

        builder.Services.AddSingleton<
            IInprocRpc<string,
            string,
            RequestMessage,
            ResponseMessage>>(new InprocRpc<string, string, RequestMessage, ResponseMessage>());

        builder.Services.AddSingleton<
            IInprocMessageQueue<string,
            string,
            Message>>(new InprocMessageQueue<string, string, Message>());


        builder.Services.AddGrpc(grpcServiceOptions =>
        {
           
        });
        builder.Services.AddCors(o => o.AddPolicy("AllowAll", builder =>
        {
            builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding")
                    .WithHeaders("Access-Control-Allow-Headers: *", "Access-Control-Allow-Origin: *");
        }));
    }


}