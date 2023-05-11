using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Play.Common.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Play.Trading.Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host
                .CreateDefaultBuilder(args)
                .ConfigureAzureKeyVault()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                // .ConfigureLogging(cfg => cfg.AddJsonConsole(opt =>
                // {
                //     opt.JsonWriterOptions = new JsonWriterOptions() { Indented = true };
                // }))
                ;
    }
}
