using System;
using System.Net.Http;
using System.Threading.Tasks;
using AntDesign.ProLayout;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using JobsWorker.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace JobsWorkerWebService.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddAntDesign();
            builder.Services.Configure<ProSettings>(builder.Configuration.GetSection("ProSettings"));
            //builder.Services.AddScoped<IChartService, ChartService>();
            //builder.Services.AddScoped<IProjectService, ProjectService>();
            //builder.Services.AddScoped<IUserService, UserService>();
            //builder.Services.AddScoped<IAccountService, AccountService>();
            //builder.Services.AddScoped<IProfileService, ProfileService>();

            //builder.Services.AddSingleton(services =>
            //{
            //    var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
            //    httpClient.Timeout = TimeSpan.FromSeconds(30);
            //    var baseUri = services.GetRequiredService<NavigationManager>().BaseUri;
            //    var channel = GrpcChannel.ForAddress(baseUri, new GrpcChannelOptions { HttpClient = httpClient });
            //    return new FileSystem.FileSystemClient(channel);
            //});

            await builder.Build().RunAsync();
        }
    }
}