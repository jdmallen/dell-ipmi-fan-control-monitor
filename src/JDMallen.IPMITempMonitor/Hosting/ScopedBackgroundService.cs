using Microsoft.Extensions.DependencyInjection;

namespace JDMallen.IPMITempMonitor.Hosting;

/// <summary>
///     Base class for background services that execute work repeatedly with support
///     for scoped dependency injection and overlap prevention.
/// </summary>
/// <typeparam name="TService">
///     The type of the service implementation. Used for generic logging and scoping.
/// </typeparam>
/// <remarks>
///     Vendored, trimmed-down copy of JDMallen.Toolbox.Hosting.ScopedBackgroundService.
///     Only the functionality used by this project is retained; the cron-scheduling
///     and ITimeProvider machinery from the original library was dropped. If more of
///     the abstraction is needed later, promote this back into the shared toolbox.
///     Adapted from: https://thinkrethink.net/2018/02/21/asp-net-core-background-processing/
/// </remarks>
public abstract class ScopedBackgroundService<TService>(
	ILogger<TService> logger,
	IServiceScopeFactory scopeFactory) : BackgroundService
{
	private readonly ILogger<TService> _logger =
		logger ?? throw new ArgumentNullException(nameof(logger));

	private readonly IServiceScopeFactory _scopeFactory =
		scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

	private int _isExecuting;

	/// <summary>
	///     Gets the delay between execution iterations. For direct subtypes this is
	///     the time to wait between the completion of one execution and the start of
	///     the next.
	/// </summary>
	protected abstract TimeSpan LoopDelay { get; }

	/// <summary>
	///     Gets the behavior for handling overlapping executions when work takes longer
	///     than <see cref="LoopDelay" />. Defaults to
	///     <see cref="Hosting.OverlapBehavior.AllowOverlap" />.
	/// </summary>
	protected virtual OverlapBehavior OverlapBehavior => OverlapBehavior.AllowOverlap;

	/// <summary>
	///     Repeatedly executes work within a scoped context until the stopping token is
	///     cancelled. Each iteration is wrapped in error handling so a single failure
	///     does not stop the service.
	/// </summary>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		do
		{
			var shouldExecute = true;
			var needsWait = false;

			if (OverlapBehavior != OverlapBehavior.AllowOverlap)
			{
				// Try to claim the execution slot. If it's already claimed, the exchange fails.
				int previousValue = Interlocked.CompareExchange(ref _isExecuting, 1, 0);

				if (previousValue == 1)
				{
					// Another execution is in progress.
					if (OverlapBehavior == OverlapBehavior.SkipIfBusy)
					{
						shouldExecute = false;
						_logger.LogDebug(
							"Skipping execution iteration because previous iteration is still running");
					}
					else if (OverlapBehavior == OverlapBehavior.WaitForCompletion)
					{
						shouldExecute = false;
						needsWait = true;
					}
				}
			}

			if (shouldExecute)
			{
				try
				{
					var sessionId = Guid.NewGuid();
					using IDisposable? scope = _logger.BeginScope(sessionId);

					_logger.LogTrace("Begin service execution iteration");
					await ExecuteInScopeAsync(stoppingToken).ConfigureAwait(false);
					_logger.LogTrace("End service execution iteration");
				}
				catch (Exception ex)
				{
					_logger.LogError(
						ex,
						"Unhandled exception in service execution iteration");
				}
				finally
				{
					if (OverlapBehavior != OverlapBehavior.AllowOverlap)
					{
						Interlocked.Exchange(ref _isExecuting, 0);
					}
				}
			}

			// When waiting for an in-flight iteration, use a short delay before retrying.
			TimeSpan delayTime = needsWait
				? TimeSpan.FromMilliseconds(100)
				: LoopDelay;

			await Task.Delay(delayTime, stoppingToken).ConfigureAwait(false);
		}
		while (!stoppingToken.IsCancellationRequested);
	}

	/// <summary>
	///     Creates a service scope and executes
	///     <see cref="ExecuteInScopeAsync(IServiceScope, CancellationToken)" /> within it.
	/// </summary>
	protected virtual Task ExecuteInScopeAsync(CancellationToken stoppingToken)
	{
		using IServiceScope scope = _scopeFactory.CreateScope();

		return ExecuteInScopeAsync(scope, stoppingToken);
	}

	/// <summary>
	///     Executes the service logic within a dependency injection scope. Derived
	///     classes implement this to define their background work.
	/// </summary>
	/// <param name="scope">
	///     The service scope containing scoped services for this execution iteration.
	/// </param>
	/// <param name="stoppingToken">
	///     The cancellation token that indicates when the service should stop.
	/// </param>
	protected abstract Task ExecuteInScopeAsync(
		IServiceScope scope,
		CancellationToken stoppingToken);
}
