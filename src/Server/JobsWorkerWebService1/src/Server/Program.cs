using FluentFTP;
using JobsWorker.Shared.MessageQueue;
using JobsWorker.Shared.MessageQueue.Models;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Client;
using JobsWorkerWebService.Server.Data;
using JobsWorkerWebService.Server.GrpcServices;
using JobsWorkerWebService.Server.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog.Extensions.Logging;
using System.Net;
using System.Security.Cryptography.X509Certificates;

public class Program
{
    public static void Main(string[] args)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        try
        {
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
            app.UseWebAssemblyDebugging();
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
        app.UseBlazorFrameworkFiles();

        app.UseRouting();

        app.UseCors("AllowAll");

        app.UseEndpoints(req =>
        {

        });

        app.MapRazorPages();
        app.MapControllers();
        app.MapFallbackToFile("index.html");

        using (var serviceScope = app.Services.GetService<IServiceScopeFactory>().CreateScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }
    }

    private static void MapGrpcServices(WebApplication app)
    {
        app.MapGrpcService<FileSystemService>().EnableGrpcWeb().RequireCors("AllowAll");
        app.MapGrpcService<JobsWorkerService>().EnableGrpcWeb().RequireCors("AllowAll");
    }

    static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddControllersWithViews();
        builder.Services.AddControllers();
        builder.Services.AddRazorPages();
        builder.Services.AddOpenApiDocument();
        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();
        builder.Services.AddLogging(logger =>
        {
            logger.ClearProviders();
            logger.AddConsole();
            logger.AddNLog();
        });
        builder.Services.Configure<FormOptions>(formOptions =>
        {
            formOptions.MultipartBodyLengthLimit = 1024 * 1024 * 1024;
        });
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseMySql(
    builder.Configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));
        builder.Services.AddDbContext<NodeInfoDbContext>(options => options.UseMySql(
            builder.Configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));
        builder.Services.AddDbContext<PluginInfoDbContext>(options => options.UseMySql(
            builder.Configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));
        builder.Services.AddDbContext<NodeConfigInfoDbContext>(options => options.UseMySql(
            builder.Configuration.GetConnectionString("MySQL"), MySqlServerVersion.LatestSupportedServerVersion));


        builder.Services.AddSingleton<FtpServerConfig>(new FtpServerConfig()
        {
            host = builder.Configuration.GetSection("FtpServerConfig")["Host"],
            port = int.Parse(builder.Configuration.GetSection("FtpServerConfig")["Port"]),
            username = builder.Configuration.GetSection("FtpServerConfig")["Username"],
            password = builder.Configuration.GetSection("FtpServerConfig")["Password"],
            rootDirectory = builder.Configuration.GetSection("FtpServerConfig")["rootDirectory"],
            nodeServiceConfigDir = builder.Configuration.GetSection("FtpServerConfig")["nodeServiceConfigDir"],
            fileServiceConfigDir = builder.Configuration.GetSection("FtpServerConfig")["fileServiceConfigDir"]
        });

        builder.Services.AddSingleton<ServerConfig>(new ServerConfig()
        {
            Channel = builder.Configuration.GetSection("ServerConfig")["Channel"]
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

        builder.Services.AddScoped<AsyncFtpClient>(serviceProvider =>
        {
            var ftpConnectConfig = serviceProvider.GetRequiredService<FtpServerConfig>();
            return new AsyncFtpClient(ftpConnectConfig.host,
                ftpConnectConfig.username,
                ftpConnectConfig.password,
                ftpConnectConfig.port);
        });


        builder.Services.AddGrpc();
        builder.Services.AddCors(o => o.AddPolicy("AllowAll", builder =>
        {
            builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
        }));
    }


}

