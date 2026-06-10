using JDMallen.IPMITempMonitor.Services;

namespace JDMallen.IPMITempMonitor;

public static class Program
{
	private static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.UseWindowsService()
			.UseSystemd()
			.ConfigureServices((hostContext, services) =>
			{
				// Register settings, validating them against their DataAnnotations at
				// startup so misconfiguration fails fast instead of at first use.
				services.AddOptions<Settings>()
					.Bind(hostContext.Configuration.GetSection("Settings"))
					.ValidateDataAnnotations()
					.ValidateOnStart();

				// Register services as singletons since they maintain state across the application lifetime
				services.AddSingleton<IIPMICommandExecutor, IPMICommandExecutor>();
				services.AddSingleton<ITemperatureMonitor, TemperatureMonitor>();
				services.AddSingleton<IFanController, FanController>();

				// Register the background worker service
				services.AddHostedService<Worker>();
			});

	public static void Main(string[] args)
	{
		IHostBuilder builder = CreateHostBuilder(args);

		IHost host = builder.Build(); // Separated for ease of inspection

		host.Run();
	}
}
