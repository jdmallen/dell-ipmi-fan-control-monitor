using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JDMallen.IPMITempMonitor
{
	public static class Program
	{
		public static void Main(string[] args)
		{
			IHostBuilder builder = CreateHostBuilder(args);

			IHost host = builder.Build(); // Separated for ease of inspection

			host.Run();
		}

		private static IHostBuilder CreateHostBuilder(string[] args) =>
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
