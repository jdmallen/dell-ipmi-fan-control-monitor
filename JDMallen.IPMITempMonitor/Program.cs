using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JDMallen.IPMITempMonitor
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = CreateHostBuilder(args);

			var host = builder.Build(); // Separated for ease of inspection

			host.Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.UseWindowsService()
				.UseSystemd()
				.ConfigureServices(
					(hostContext, services) =>
					{
						services.AddHostedService<Worker>();
						services.Configure<Settings>(
							hostContext.Configuration.GetSection("Settings"));
					});
	}
}
