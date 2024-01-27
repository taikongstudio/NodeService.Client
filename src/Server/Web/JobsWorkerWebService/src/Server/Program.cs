using JobsWorkerWebService.Server.Data;
using JobsWorkerWebService.Server.FileSystemServices;
using JobsWorkerWebService.Server.GrpcServices;
using JobsWorkerWebService.Server.Models;
using JobsWorkerWebService.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NLog.Extensions.Logging;

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
            //app.UseWebAssemblyDebugging();
            //// Add OpenAPI 3.0 document serving middleware
            //// Available at: http://localhost:<port>/swagger/v1/swagger.json
            //app.UseOpenApi();

            //// Add web UIs to interact with the document
            //// Available at: http://localhost:<port>/swagger
            //app.UseSwaggerUi((uiSettings) =>
            //{

            //});
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseCors();
        app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

        MapGrpcServices(app);

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseBlazorFrameworkFiles();

        app.UseRouting();
        app.UseEndpoints(req =>
        {

        });

        app.MapRazorPages();
        app.MapControllers();
        app.MapFallbackToFile("index.html");
    }

    private static void MapGrpcServices(WebApplication app)
    {
        app.MapGrpcService<FileSystemService>().EnableGrpcWeb().RequireCors("AllowAll");
        app.MapGrpcService<JobsWorkerService>().EnableGrpcWeb().RequireCors("AllowAll");
    }

    static void Configure(WebApplicationBuilder builder)
    {
        const string logConfigFileName = "Nlog.config";
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
            logger.AddNLog(logConfigFileName);
        });

        builder.Services.AddDbContext<MachineInfoDbContext>(options =>options.UseMySql(
            builder.Configuration.GetConnectionString("MySQL"),MySqlServerVersion.LatestSupportedServerVersion));



        builder.Services.AddSingleton<FileSystemConfig>(new FileSystemConfig()
        {
            RootPath = builder.Configuration.GetSection("FileSystemConfig")["RootPath"],
            ExcludePathList = null
        });
        builder.Services.AddSingleton<IInprocRpc<string, FileSystemRequest, FileSystemResponse>>(new InprocRpc<string, FileSystemRequest, FileSystemResponse>());


        builder.Services.AddGrpc((options) =>
        {

        });
        builder.Services.AddCors(o => o.AddPolicy("AllowAll", builder =>
        {
            builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
        }));
    }


}

