using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace R620TempMonitor
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = CreateHostBuilder(args);

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				builder.UseWindowsService();
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				builder.UseSystemd();
			}

			builder.Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureServices(
					(hostContext, services) =>
					{
						services.AddHostedService<Worker>();
						services.Configure<Settings>(
							hostContext.Configuration.GetSection("Settings"));
					});
	}
}
