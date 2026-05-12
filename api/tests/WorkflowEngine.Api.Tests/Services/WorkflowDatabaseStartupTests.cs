using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowEngine.Api.Options;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Persistence;

namespace WorkflowEngine.Api.Tests.Services;

public sealed class WorkflowDatabaseStartupTests
{
    [Fact]
    public async Task HostedServiceDoesNothingWhenStartupDatabaseOptionsAreDisabled()
    {
        var scopeFactory = new TrackingScopeFactory(new Dictionary<Type, object?>());
        var hostedService = new WorkflowDatabaseStartupHostedService(
            scopeFactory,
            Microsoft.Extensions.Options.Options.Create(new WorkflowDatabaseStartupOptions()),
            new NoopLogger<WorkflowDatabaseStartupHostedService>());

        await hostedService.StartAsync(CancellationToken.None);

        Assert.Equal(0, scopeFactory.CreatedScopes);
    }

    [Fact]
    public async Task HostedServiceUsesScopeWhenStartupDatabaseWorkIsEnabled()
    {
        var initializer = new RecordingDatabaseInitializer();
        var verifier = new RecordingSeedVerifier([]);
        var readiness = new WorkflowDatabaseReadinessService(
            initializer,
            verifier,
            new NoopLogger<WorkflowDatabaseReadinessService>());
        var scopeFactory = new TrackingScopeFactory(new Dictionary<Type, object?>
        {
            [typeof(WorkflowDatabaseReadinessService)] = readiness
        });
        var hostedService = new WorkflowDatabaseStartupHostedService(
            scopeFactory,
            Microsoft.Extensions.Options.Options.Create(new WorkflowDatabaseStartupOptions
            {
                VerifySeedOnStartup = true
            }),
            new NoopLogger<WorkflowDatabaseStartupHostedService>());

        await hostedService.StartAsync(CancellationToken.None);

        Assert.Equal(1, scopeFactory.CreatedScopes);
        Assert.Equal(0, initializer.InitializeCalls);
        Assert.Equal(1, verifier.VerifyCalls);
    }

    private sealed class RecordingDatabaseInitializer : IWorkflowDatabaseInitializer
    {
        public int InitializeCalls { get; private set; }

        public string Initialize()
        {
            InitializeCalls++;
            return "initialized";
        }
    }

    private sealed class RecordingSeedVerifier(IReadOnlyList<string> errors) : ISeedVerificationService
    {
        public int VerifyCalls { get; private set; }

        public SeedVerificationResult Verify()
        {
            VerifyCalls++;
            return new SeedVerificationResult(errors);
        }
    }

    private sealed class TrackingScopeFactory(IReadOnlyDictionary<Type, object?> services) : IServiceScopeFactory
    {
        public int CreatedScopes { get; private set; }

        public IServiceScope CreateScope()
        {
            CreatedScopes++;
            return new TrackingScope(new TrackingServiceProvider(services));
        }
    }

    private sealed class TrackingScope(IServiceProvider serviceProvider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public void Dispose()
        {
        }
    }

    private sealed class TrackingServiceProvider(IReadOnlyDictionary<Type, object?> services) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return services.TryGetValue(serviceType, out var service) ? service : null;
        }
    }

    private sealed class NoopLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
