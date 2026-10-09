using CShells.DependencyInjection;
using CShells.Hosting;
using CShells.Lifecycle;
using CShells.Lifecycle.Blueprints;
using CShells.Lifecycle.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CShells.Tests.Integration.Hosting;

public sealed class ShellActivationRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Registration_IsOptInLazyAndRootOnly()
    {
        await using (var plain = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { })))
        {
            Assert.Null(plain.GetService<IShellActivationRunner>());
            Assert.NotNull(plain.GetRequiredService<IShellRegistry>());
        }

        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }),
            services => services.AddShellActivationRunner().AddShellActivationRunner());
        var runner = host.GetRequiredService<IShellActivationRunner>();
        var run = runner.Start(["tenant"]);
        await run.InitialPass.WaitAsync(Timeout);

        var shell = Assert.IsType<Shell>(host.GetRequiredService<IShellRegistry>().GetActive("tenant"));
        Assert.Null(shell.ServiceProvider.GetService<IShellActivationRunner>());
        await run.StopAsync();
    }

    [Fact]
    public async Task Start_ValidatesCopiesDeduplicatesAndRunsInitialPassSerially()
    {
        var events = new List<string>();
        var attempts = new List<ShellActivationAttempt>();
        await using var host = BuildHost(cshells => cshells.WithAssemblies()
            .AddShell("a", _ => { })
            .AddShell("b", _ => { }),
            services => services.AddShellActivationRunner());
        var runner = host.GetRequiredService<IShellActivationRunner>();
        Assert.Throws<ArgumentNullException>(() => runner.Start(null!));
        Assert.Throws<ArgumentException>(() => runner.Start(["a", " "]));

        var names = new List<string> { "a", "A", "b" };
        var run = runner.Start(names, observer: new CallbackObserver(attempt =>
        {
            events.Add(attempt.ShellName);
            attempts.Add(attempt);
        }));
        names.Clear();
        await run.InitialPass.WaitAsync(Timeout);
        Assert.Equal(["a", "b"], events);
        Assert.Equal([ShellActivationTargetStatus.Succeeded, ShellActivationTargetStatus.Succeeded], run.Snapshot.Select(s => s.Status));
        Assert.All(run.Snapshot, state => Assert.Equal(1, state.VerifiedGeneration));
        long?[] expectedReturnedGenerations = [1, 1];
        Assert.Equal(expectedReturnedGenerations, attempts.Select(attempt => attempt.ReturnedGeneration));
        Assert.All(run.Snapshot, state => Assert.Equal(state.VerifiedGeneration, state.ReturnedGeneration));
        await run.StopAsync();

        var empty = runner.Start([]);
        await empty.InitialPass.WaitAsync(Timeout);
        Assert.Empty(empty.Snapshot);
        await empty.StopAsync();
    }

    [Fact]
    public async Task ExistingCustomRunnerRegisteredBeforeAddCShellsIsPreserved()
    {
        var custom = new CustomRunner();
        await using var host = BuildHost(cshells => cshells.WithAssemblies(), services =>
        {
            services.AddSingleton<IShellActivationRunner>(custom);
            services.AddShellActivationRunner();
        });

        Assert.Same(custom, host.GetRequiredService<IShellActivationRunner>());
    }

    [Fact]
    public async Task RetryTimersAreNotArmedUntilTheSerialInitialPassCompletes()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = new List<string>();
        await using var host = BuildHost(cshells => cshells.WithAssemblies()
                .AddShell("first", _ => { })
                .AddShell("second", _ => { }),
            services =>
            {
                services.AddSingleton<TimeProvider>(time);
                services.AddShellActivationRunner();
                services.AddSingleton<IShellRegistry>(sp => new InitialPassGateRegistry(
                    sp.GetRequiredService<ShellRegistry>(), attempts, secondEntered, releaseSecond));
            });
        var runner = host.GetRequiredService<IShellActivationRunner>();
        IShellActivationRun? run = null;
        try
        {
            run = await Task.Run(() => runner.Start(
                ["first", "second"], retryPolicy: _ => ShellActivationRetryDecision.RetryAfter(TimeSpan.FromSeconds(5))))
                .WaitAsync(Timeout);
            await secondEntered.Task.WaitAsync(Timeout);
            Assert.False(run.InitialPass.IsCompleted);
            Assert.Equal(["first", "second"], attempts);
            Assert.Equal(ShellActivationTargetStatus.RetryPending, run.Snapshot[0].Status);
            Assert.Null(run.Snapshot[0].NextAttemptAt);
            time.Advance(TimeSpan.FromMinutes(1));
            Assert.Equal(1, run.Snapshot[0].AttemptCount);

            releaseSecond.TrySetResult();
            await run.InitialPass.WaitAsync(Timeout);
            await WaitUntilAsync(() => run.Snapshot[0].NextAttemptAt is not null);
            Assert.Equal(TimeSpan.FromSeconds(5), run.Snapshot[0].RetryDelay);
            Assert.Equal(time.GetUtcNow() + TimeSpan.FromSeconds(5), run.Snapshot[0].NextAttemptAt);
            await run.StopAsync();
            Assert.Equal(ShellActivationTargetStatus.Stopped, run.Snapshot[0].Status);
            Assert.Null(run.Snapshot[0].RetryDelay);
            Assert.Null(run.Snapshot[0].NextAttemptAt);
            Assert.Equal(1, run.Snapshot[0].AttemptCount);
            await run.StopAsync();
        }
        finally
        {
            releaseSecond.TrySetResult();
            if (run is not null)
                await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public async Task StartupCancellationDuringInitialPassDoesNotStartLaterTargetsOrRetryPolicy()
    {
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = new List<string>();
        await using var host = BuildHost(cshells => cshells.WithAssemblies()
                .AddShell("first", _ => { })
                .AddShell("second", _ => { })
                .AddShell("third", _ => { }),
            services =>
            {
                services.AddShellActivationRunner();
                services.AddSingleton<IShellRegistry>(sp => new InitialPassGateRegistry(
                    sp.GetRequiredService<ShellRegistry>(), attempts, secondEntered, releaseSecond));
            });
        using var startup = new CancellationTokenSource();
        var policyCalls = 0;
        var run = host.GetRequiredService<IShellActivationRunner>().Start(
            ["first", "second", "third"],
            retryPolicy: _ => { Interlocked.Increment(ref policyCalls); return ShellActivationRetryDecision.RetryAfter(TimeSpan.FromSeconds(1)); },
            startupCancellationToken: startup.Token);
        try
        {
            await secondEntered.Task.WaitAsync(Timeout);
            await startup.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.InitialPass);
            Assert.Equal(["first", "second"], attempts);
            Assert.Equal(1, policyCalls);
            Assert.Equal(ShellActivationTargetStatus.Stopped, run.Snapshot[2].Status);
            Assert.Null(run.Snapshot[1].ReturnedGeneration);
        }
        finally
        {
            releaseSecond.TrySetResult();
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public async Task UnsupportedDelayEndsOnlyItsTargetWhileSiblingRetryContinues()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        await using var host = BuildHost(cshells => cshells.WithAssemblies()
                .AddShell("long", _ => { })
                .AddShell("short", _ => { }),
            services =>
            {
                services.AddSingleton<TimeProvider>(time);
                services.AddShellActivationRunner();
                services.AddSingleton<IShellRegistry>(sp => new FirstFailurePerTargetRegistry(sp.GetRequiredService<ShellRegistry>()));
            });

        var run = host.GetRequiredService<IShellActivationRunner>().Start(["long", "short"], retryPolicy: attempt =>
            ShellActivationRetryDecision.RetryAfter(attempt.ShellName == "long" ? TimeSpan.FromDays(100) : TimeSpan.FromSeconds(5)));
        try
        {
            await run.InitialPass.WaitAsync(Timeout);
            await WaitUntilAsync(() => run.Snapshot[0].Status == ShellActivationTargetStatus.PolicyError
                && run.Snapshot[1].Status == ShellActivationTargetStatus.RetryScheduled);

            Assert.Equal("RetryDelayOutOfRange", run.Snapshot[0].PolicyErrorCode);
            time.Advance(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => run.Snapshot[1].Status == ShellActivationTargetStatus.Succeeded);
            Assert.Equal(2, run.Snapshot[1].AttemptCount);
            Assert.Equal(1, run.Snapshot[1].FailedAttemptCount);
            Assert.NotNull(run.Snapshot[1].FirstFailedAt);
            Assert.Equal(run.Snapshot[1].FirstFailedAt, run.Snapshot[1].LastFailedAt);
            Assert.Null(run.Snapshot[1].RetryDelay);

            var longShell = await host.GetRequiredService<ShellRegistry>().ActivateAsync("long");
            var longState = Assert.Single(run.Snapshot, state => state.ShellName == "long");
            Assert.Equal(ShellActivationTargetStatus.SatisfiedExternally, longState.Status);
            Assert.Equal(1, longState.FailedAttemptCount);
            Assert.NotNull(longState.FirstFailedAt);
            await (await host.GetRequiredService<ShellRegistry>().DrainAsync(longShell)).WaitAsync().WaitAsync(Timeout);
        }
        finally
        {
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public async Task StopWinsAgainstInFlightSnapshotReconciliation()
    {
        var readEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }));
        var inner = host.GetRequiredService<IShellRegistry>();
        await inner.ActivateAsync("tenant");
        var registry = new BlockingReadRegistry(inner, readEntered, releaseRead);
        var services = new ServiceCollection();
        services.AddSingleton<IShellRegistry>(registry);
        services.AddShellActivationRunner();
        await using var runnerHost = services.BuildServiceProvider();
        var run = runnerHost.GetRequiredService<IShellActivationRunner>().Start(["tenant"]);
        Task<IReadOnlyList<ShellActivationAttemptState>>? snapshot = null;
        try
        {
            await run.InitialPass.WaitAsync(Timeout);
            snapshot = Task.Run(() => run.Snapshot);
            await readEntered.Task.WaitAsync(Timeout);
            var stop = run.StopAsync();
            releaseRead.TrySetResult();
            await stop.WaitAsync(Timeout);
            Assert.Equal(ShellActivationTargetStatus.Failed, Assert.Single(await snapshot.WaitAsync(Timeout)).Status);
        }
        finally
        {
            releaseRead.TrySetResult();
            await IgnoreFailureAsync(snapshot);
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public async Task StopBoundsWaitForStubbornActivationAndRunStillObservesItsResult()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource<ShellActivationAttempt>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = BuildRunnerHost(new StubbornRegistry(entered, release));
        var run = host.GetRequiredService<IShellActivationRunner>().Start(
            ["tenant"], observer: new CallbackObserver(attempt => observed.TrySetResult(attempt)));
        try
        {
            await entered.Task.WaitAsync(Timeout);
            using var bound = new CancellationTokenSource();
            var boundedStop = run.StopAsync(bound.Token);
            bound.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => boundedStop);
            release.TrySetResult();
            var attempt = await observed.Task.WaitAsync(Timeout);
            Assert.Equal(ShellActivationAttemptOutcome.ActivationFailed, attempt.Outcome);
            Assert.Equal(typeof(StubbornFailure).FullName, attempt.ErrorType);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.InitialPass);
        }
        finally
        {
            release.TrySetResult();
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationWaitsForBlockedCallbacksBeforeDisposingLifetime(bool cancelFromStartup)
    {
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = BuildRunnerHost(new CancellationCallbackRegistry(activationEntered, callbackEntered, releaseCallback));
        using var startup = new CancellationTokenSource();
        var policyCalls = 0;
        var run = host.GetRequiredService<IShellActivationRunner>().Start(["tenant"],
            retryPolicy: _ => { Interlocked.Increment(ref policyCalls); return ShellActivationRetryDecision.RetryAfter(TimeSpan.FromSeconds(1)); },
            startupCancellationToken: startup.Token);
        await activationEntered.Task.WaitAsync(Timeout);
        using var bound = new CancellationTokenSource();
        Task cancellation = Task.CompletedTask;
        Task? finalStop = null;
        try
        {
            if (cancelFromStartup)
            {
                cancellation = startup.CancelAsync();
                await callbackEntered.Task.WaitAsync(Timeout);
            }
            var boundedStop = run.StopAsync(bound.Token);
            if (!cancelFromStartup)
                await callbackEntered.Task.WaitAsync(Timeout);
            bound.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => boundedStop);
            finalStop = run.StopAsync();
            Assert.False(finalStop.IsCompleted);

            releaseCallback.TrySetResult();
            await cancellation.WaitAsync(Timeout);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.InitialPass);
            await finalStop.WaitAsync(Timeout);
            Assert.Equal(0, policyCalls);
        }
        finally
        {
            releaseCallback.TrySetResult();
            await IgnoreFailureAsync(cancellation);
            await IgnoreFailureAsync(finalStop);
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public async Task ObserverCanReadSnapshotAndItsFailureDoesNotChangeAttemptResult()
    {
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }),
            services => services.AddShellActivationRunner());
        IShellActivationRun? run = null;
        var runPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ShellActivationTargetStatus? observedStatus = null;
        var observer = new CallbackObserver(attempt =>
        {
            runPublished.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
            observedStatus = Assert.Single(run!.Snapshot).Status;
            throw new InvalidOperationException("observer detail");
        });
        run = host.GetRequiredService<IShellActivationRunner>().Start(["tenant"], observer: observer);
        runPublished.TrySetResult();

        await run.InitialPass.WaitAsync(Timeout);
        Assert.Equal(ShellActivationTargetStatus.Succeeded, observedStatus);
        Assert.Equal(ShellActivationTargetStatus.Succeeded, Assert.Single(run.Snapshot).Status);
        await run.StopAsync();
    }

    [Fact]
    public async Task NotCurrentHasNoSyntheticExceptionAndPolicyFailurePreservesAttemptMetadata()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decorator = new GatedReturnRegistryProxy(entered, release);
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }), services =>
        {
            services.AddShellActivationRunner();
            services.AddSingleton<IShellRegistry>(sp =>
            {
                decorator.InnerOverride = sp.GetRequiredService<ShellRegistry>();
                return decorator;
            });
        });
        var registry = host.GetRequiredService<ShellRegistry>();
        var stale = await registry.ActivateAsync("tenant");
        await (await registry.DrainAsync(stale)).WaitAsync().WaitAsync(Timeout);
        decorator.ReturnedShell = stale;
        release.TrySetResult();

        var run = host.GetRequiredService<IShellActivationRunner>().Start(["tenant"], retryPolicy: _ => throw new InvalidOperationException("policy detail"));
        await run.InitialPass.WaitAsync(Timeout);
        var state = Assert.Single(run.Snapshot);
        Assert.Equal(ShellActivationAttemptOutcome.NotCurrent, state.LastOutcome);
        Assert.Equal("NotCurrent", state.ErrorCode);
        Assert.Null(state.ErrorType);
        Assert.Equal("RetryPolicyFailed", state.PolicyErrorCode);
        Assert.Equal(1, state.FailedAttemptCount);
        await run.StopAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomRegistrySuccessRequiresCurrentIdentityAndHasNoDefaultGeneration(bool returnsCurrent)
    {
        var returned = CShells.Tests.Integration.AspNetCore.ShellMiddlewareTests.FakeShell.WithServices(_ => { }, "custom", generation: 37);
        var current = returnsCurrent
            ? returned
            : CShells.Tests.Integration.AspNetCore.ShellMiddlewareTests.FakeShell.WithServices(_ => { }, "custom");
        await using var host = BuildRunnerHost(new FixedCustomRegistry(returned, current));
        try
        {
            ShellActivationAttempt? observedAttempt = null;
            var run = host.GetRequiredService<IShellActivationRunner>().Start(["custom"],
                observer: new CallbackObserver(attempt => observedAttempt = attempt));
            await run.InitialPass.WaitAsync(Timeout);
            Assert.NotNull(observedAttempt);
            var state = Assert.Single(run.Snapshot);
            Assert.Equal(returnsCurrent ? ShellActivationAttemptOutcome.Succeeded : ShellActivationAttemptOutcome.NotCurrent, state.LastOutcome);
            Assert.Equal(returnsCurrent ? ShellActivationTargetStatus.Succeeded : ShellActivationTargetStatus.Failed, state.Status);
            Assert.Null(state.VerifiedGeneration);
            if (returnsCurrent)
            {
                Assert.Equal((long)returned.Descriptor.Generation, observedAttempt.ReturnedGeneration);
                Assert.Equal((long)returned.Descriptor.Generation, state.ReturnedGeneration);
            }
            else
            {
                Assert.Null(observedAttempt.ReturnedGeneration);
                Assert.Null(state.ReturnedGeneration);
            }
            await run.StopAsync();
        }
        finally
        {
            await ((ServiceProvider)returned.ServiceProvider).DisposeAsync();
            if (!ReferenceEquals(returned, current))
                await ((ServiceProvider)current.ServiceProvider).DisposeAsync();
        }
    }

    [Fact]
    public async Task CancellationAfterSuccessfulReturnPreservesReturnedGeneration()
    {
        var attemptEntered = new TaskCompletionSource<ShellActivationAttempt>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseObserver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }),
            services => services.AddShellActivationRunner());
        using var startup = new CancellationTokenSource();
        var observer = new CancellationGatedObserver(attemptEntered, releaseObserver);
        var run = host.GetRequiredService<IShellActivationRunner>().Start(
            ["tenant"], observer: observer, startupCancellationToken: startup.Token);
        try
        {
            var completedAttempt = await attemptEntered.Task.WaitAsync(Timeout);
            Assert.Equal(ShellActivationAttemptOutcome.Succeeded, completedAttempt.Outcome);
            var returnedGeneration = completedAttempt.ReturnedGeneration;
            Assert.NotNull(returnedGeneration);

            var duringObserver = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.Succeeded, duringObserver.Status);
            Assert.Equal(completedAttempt.ReturnedGeneration, duringObserver.ReturnedGeneration);

            await startup.CancelAsync().WaitAsync(Timeout);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.InitialPass.WaitAsync(Timeout));

            var afterCancellation = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.Succeeded, afterCancellation.Status);
            Assert.Equal(returnedGeneration, completedAttempt.ReturnedGeneration);
            Assert.Equal(returnedGeneration, afterCancellation.ReturnedGeneration);
        }
        finally
        {
            releaseObserver.TrySetResult();
            await IgnoreFailureAsync(run.InitialPass);
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public void ExistingActivationResultConstructorsRemainAvailableAndReturnedGenerationDefaultsToNull()
    {
        Type[] attemptParameterTypes =
        [
            typeof(string),
            typeof(int),
            typeof(ShellActivationAttemptOutcome),
            typeof(DateTimeOffset),
            typeof(DateTimeOffset),
            typeof(string),
            typeof(string),
            typeof(Exception),
            typeof(long?)
        ];
        Type[] stateParameterTypes =
        [
            typeof(string),
            typeof(int),
            typeof(ShellActivationTargetStatus),
            typeof(int),
            typeof(DateTimeOffset?),
            typeof(DateTimeOffset?),
            typeof(TimeSpan?),
            typeof(long?),
            typeof(ShellActivationAttemptOutcome?),
            typeof(DateTimeOffset?),
            typeof(DateTimeOffset?),
            typeof(DateTimeOffset?),
            typeof(string),
            typeof(string),
            typeof(string)
        ];

        var attemptConstructor = typeof(ShellActivationAttempt).GetConstructor(attemptParameterTypes);
        var stateConstructor = typeof(ShellActivationAttemptState).GetConstructor(stateParameterTypes);
        Assert.NotNull(attemptConstructor);
        Assert.NotNull(stateConstructor);
        Assert.True(attemptConstructor!.GetParameters()[^1].IsOptional);
        Assert.All(stateConstructor!.GetParameters().Take(3), parameter => Assert.False(parameter.IsOptional));
        Assert.All(stateConstructor.GetParameters().Skip(3), parameter => Assert.True(parameter.IsOptional));

        var attempt = new ShellActivationAttempt("tenant", 1, ShellActivationAttemptOutcome.Succeeded,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null, null, verifiedGeneration: 1);
        var state = new ShellActivationAttemptState("tenant", 1, ShellActivationTargetStatus.Succeeded,
            verifiedGeneration: 1);
        Assert.Null(attempt.ReturnedGeneration);
        Assert.Null(state.ReturnedGeneration);
    }

    [Fact]
    public async Task ReturnedGenerationRemainsAssociatedWithSuccessfulAttemptAfterLaterReload()
    {
        var attempts = new List<ShellActivationAttempt>();
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }),
            services => services.AddShellActivationRunner());
        var registry = host.GetRequiredService<ShellRegistry>();
        IShellActivationRun? run = null;
        IDrainOperation? pendingDrain = null;
        try
        {
            run = host.GetRequiredService<IShellActivationRunner>().Start(["tenant"],
                observer: new CallbackObserver(attempt => attempts.Add(attempt)));
            await run.InitialPass.WaitAsync(Timeout);

            var original = Assert.Single(run.Snapshot);
            var successfulAttempt = Assert.Single(attempts);
            Assert.Equal(ShellActivationAttemptOutcome.Succeeded, successfulAttempt.Outcome);
            Assert.Equal(successfulAttempt.ReturnedGeneration, original.ReturnedGeneration);
            Assert.Equal(original.VerifiedGeneration, original.ReturnedGeneration);

            var reload = await registry.ReloadAsync("tenant");
            pendingDrain = reload.Drain;
            var replacement = Assert.IsType<Shell>(reload.NewShell);
            Assert.NotEqual(original.ReturnedGeneration, (long)replacement.Descriptor.Generation);
            if (pendingDrain is not null)
                await pendingDrain.WaitAsync().WaitAsync(Timeout);
            pendingDrain = null;

            var afterReload = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.Succeeded, afterReload.Status);
            Assert.Equal(original.ReturnedGeneration, afterReload.ReturnedGeneration);
            Assert.Equal(original.VerifiedGeneration, afterReload.VerifiedGeneration);
            await run.StopAsync();
        }
        finally
        {
            if (pendingDrain is not null)
            {
                await IgnoreFailureAsync(pendingDrain.WaitAsync());
            }

            if (run is not null)
                await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public async Task UnknownCustomRegistryActiveValueIsNotExternallyReconciled()
    {
        var current = CShells.Tests.Integration.AspNetCore.ShellMiddlewareTests.FakeShell.WithServices(_ => { }, "custom");
        await using var host = BuildRunnerHost(new FixedCustomRegistry(current, current, failAttempt: true));
        try
        {
            var run = host.GetRequiredService<IShellActivationRunner>().Start(["custom"]);
            await run.InitialPass.WaitAsync(Timeout);
            var state = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.Failed, state.Status);
            Assert.Null(state.ReturnedGeneration);
            await run.StopAsync();
        }
        finally
        {
            await ((ServiceProvider)current.ServiceProvider).DisposeAsync();
        }
    }

    [Fact]
    public async Task DefaultRegistryGenerationRemovedBeforeVerificationIsNotCurrent()
    {
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }), services =>
        {
            services.AddShellActivationRunner();
            services.AddSingleton<IShellRegistry>(sp => new DrainBeforeReturnRegistry(sp.GetRequiredService<ShellRegistry>()));
        });
        var run = host.GetRequiredService<IShellActivationRunner>().Start(["tenant"], retryPolicy: _ => ShellActivationRetryDecision.Stop);

        await run.InitialPass.WaitAsync(Timeout);
        var state = Assert.Single(run.Snapshot);
        Assert.Equal(ShellActivationAttemptOutcome.NotCurrent, state.LastOutcome);
        Assert.Null(state.VerifiedGeneration);
        Assert.Null(state.ReturnedGeneration);
        Assert.Equal(ShellActivationTargetStatus.Stopped, state.Status);
    }

    [Fact]
    public async Task AttemptInputHasTransientErrorWhileSnapshotRemainsSanitized()
    {
        var observed = new TaskCompletionSource<ShellActivationAttempt>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = BuildHost(cshells => cshells.WithAssemblies(), services => services.AddShellActivationRunner());
        var registry = host.GetRequiredService<IShellRegistry>();
        var failing = new FirstFailureRegistry(registry, new ClassifiedException("private connection text"));
        var services = new ServiceCollection();
        services.AddSingleton<IShellRegistry>(failing);
        services.AddShellActivationRunner();
        using var runnerHost = services.BuildServiceProvider();
        var run = runnerHost.GetRequiredService<IShellActivationRunner>().Start(["missing"], retryPolicy: attempt =>
        {
            Assert.IsType<ClassifiedException>(attempt.Exception);
            return ShellActivationRetryDecision.Stop;
        }, observer: new CallbackObserver(attempt => observed.TrySetResult(attempt)));

        await run.InitialPass.WaitAsync(Timeout);
        var attempt = await observed.Task.WaitAsync(Timeout);
        var state = Assert.Single(run.Snapshot);
        Assert.Equal(ShellActivationAttemptOutcome.ActivationFailed, attempt.Outcome);
        Assert.Null(attempt.ReturnedGeneration);
        Assert.Equal("private connection text", attempt.Exception!.Message);
        Assert.DoesNotContain("private connection", state.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ShellActivationTargetStatus.Stopped, state.Status);
        Assert.Null(state.ReturnedGeneration);
        await run.StopAsync();
    }

    [Fact]
    public async Task ExternalProvisionalCandidateDoesNotSatisfyRunButCommittedCandidateDoes()
    {
        var entered = new TaskCompletionSource<IShell>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var participant = new GatedCommitParticipant(entered, release);
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }),
            services =>
            {
                services.AddSingleton<IShellGenerationActivationParticipant>(participant);
                services.AddShellActivationRunner();
                services.AddSingleton<IShellRegistry>(sp => new FirstFailureRegistry(
                    sp.GetRequiredService<ShellRegistry>(), new InvalidOperationException("initial transient")));
            });
        var registry = host.GetRequiredService<IShellRegistry>();
        var runner = host.GetRequiredService<IShellActivationRunner>();

        var run = runner.Start(["tenant"], retryPolicy: _ => ShellActivationRetryDecision.Stop);
        Task<IShell>? activation = null;
        try
        {
            await run.InitialPass.WaitAsync(Timeout);
            Assert.Equal(ShellActivationTargetStatus.Stopped, Assert.Single(run.Snapshot).Status);
            activation = Task.Run(() => registry.ActivateAsync("tenant"));
            var candidate = await entered.Task.WaitAsync(Timeout);
            Assert.Same(candidate, registry.GetActive("tenant"));
            Assert.Equal(ShellActivationTargetStatus.Stopped, Assert.Single(run.Snapshot).Status);
            release.TrySetResult();
            await activation.WaitAsync(Timeout);
            var state = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.SatisfiedExternally, state.Status);
            Assert.Equal(1, state.FailedAttemptCount);
            Assert.NotNull(state.FirstFailedAt);
            Assert.Equal(state.FirstFailedAt, state.LastFailedAt);
        }
        finally
        {
            release.TrySetResult();
            await IgnoreFailureAsync(activation);
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalSatisfactionIsTerminalAgainstLateResultAndLaterDrain(bool returnCurrentShell)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registryDecorator = new GatedReturnRegistryProxy(entered, release);
        var attempts = new List<ShellActivationAttempt>();
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }), services =>
        {
            services.AddShellActivationRunner();
            services.AddSingleton<IShellRegistry>(sp =>
            {
                registryDecorator.InnerOverride = sp.GetRequiredService<ShellRegistry>();
                return registryDecorator;
            });
        });
        var registry = host.GetRequiredService<ShellRegistry>();
        var run = host.GetRequiredService<IShellActivationRunner>().Start(["tenant"],
            observer: new CallbackObserver(attempt => attempts.Add(attempt)));
        try
        {
            await entered.Task.WaitAsync(Timeout);
            var firstGeneration = await registry.ActivateAsync("tenant");
            var externallySatisfied = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.SatisfiedExternally, externallySatisfied.Status);
            Assert.Equal(((Shell)firstGeneration).Descriptor.Generation, externallySatisfied.VerifiedGeneration);
            Assert.Null(externallySatisfied.ReturnedGeneration);

            var drain = await registry.DrainAsync(firstGeneration);
            await drain.WaitAsync().WaitAsync(Timeout);
            var returnedShell = returnCurrentShell ? await registry.ActivateAsync("tenant") : firstGeneration;
            registryDecorator.ReturnedShell = returnedShell;
            release.TrySetResult();
            await run.InitialPass.WaitAsync(Timeout);

            var terminal = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.SatisfiedExternally, terminal.Status);
            Assert.Equal(returnCurrentShell ? ShellActivationAttemptOutcome.Succeeded : ShellActivationAttemptOutcome.NotCurrent, terminal.LastOutcome);
            Assert.Equal(externallySatisfied.VerifiedGeneration, terminal.VerifiedGeneration);
            var attempt = Assert.Single(attempts);
            if (returnCurrentShell)
            {
                Assert.Equal(((Shell)returnedShell).Descriptor.Generation, attempt.ReturnedGeneration);
                Assert.Equal(attempt.ReturnedGeneration, terminal.ReturnedGeneration);
            }
            else
            {
                Assert.Null(attempt.ReturnedGeneration);
                Assert.Null(terminal.ReturnedGeneration);
            }
        }
        finally
        {
            release.TrySetResult();
            await IgnoreFailureAsync(run.InitialPass);
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    [Fact]
    public async Task ExternalSatisfactionSurvivesCancellationOfAnInFlightOwnedAttempt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decorator = new GatedReturnRegistryProxy(entered, release);
        await using var host = BuildHost(cshells => cshells.WithAssemblies().AddShell("tenant", _ => { }), services =>
        {
            services.AddShellActivationRunner();
            services.AddSingleton<IShellRegistry>(sp =>
            {
                decorator.InnerOverride = sp.GetRequiredService<ShellRegistry>();
                return decorator;
            });
        });
        var registry = host.GetRequiredService<ShellRegistry>();
        using var startup = new CancellationTokenSource();
        var run = host.GetRequiredService<IShellActivationRunner>().Start(["tenant"], startupCancellationToken: startup.Token);
        try
        {
            await entered.Task.WaitAsync(Timeout);
            var external = await registry.ActivateAsync("tenant");
            var beforeCancellation = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.SatisfiedExternally, beforeCancellation.Status);
            Assert.Null(beforeCancellation.ReturnedGeneration);

            await startup.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.InitialPass);
            await (await registry.DrainAsync(external)).WaitAsync().WaitAsync(Timeout);

            var afterCancellationAndDrain = Assert.Single(run.Snapshot);
            Assert.Equal(ShellActivationTargetStatus.SatisfiedExternally, afterCancellationAndDrain.Status);
            Assert.Equal(beforeCancellation.VerifiedGeneration, afterCancellationAndDrain.VerifiedGeneration);
            Assert.Null(afterCancellationAndDrain.ReturnedGeneration);
        }
        finally
        {
            release.TrySetResult();
            await IgnoreFailureAsync(run.InitialPass);
            await IgnoreFailureAsync(run.StopAsync());
        }
    }

    private static TestHost BuildHost(Action<CShellsBuilder> configure, Action<IServiceCollection>? configureServices = null)
        => new(CShells.Tests.Integration.Lifecycle.ShellRegistryActivateTests.BuildHost(configure, configureServices));

    private static ServiceProvider BuildRunnerHost(IShellRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddSingleton(registry);
        services.AddShellActivationRunner();
        return services.BuildServiceProvider();
    }

    private sealed class TestHost(ServiceProvider services) : IServiceProvider, IAsyncDisposable
    {
        public object? GetService(Type serviceType) => services.GetService(serviceType);

        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            try
            {
                var registry = services.GetService<ShellRegistry>() ?? services.GetService<IShellRegistry>();
                if (registry is not null)
                {
                    foreach (var shell in registry.GetActiveShells().ToArray())
                    {
                        try
                        {
                            var drain = await registry.DrainAsync(shell);
                            await drain.WaitAsync().WaitAsync(Timeout);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    await services.DisposeAsync();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (failures.Count > 0)
                throw new AggregateException("The shell activation runner test host failed to drain or dispose cleanly.", failures);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("The expected runner state was not reached.");
            await Task.Delay(10);
        }
    }

    private static async Task IgnoreFailureAsync(Task? task)
    {
        if (task is null)
            return;

        try
        {
            await task.WaitAsync(Timeout);
        }
        catch
        {
            // Expected operation failures are asserted by each test; cleanup only joins remaining work.
        }
    }

    private sealed class CallbackObserver(Action<ShellActivationAttempt> callback) : IShellActivationAttemptObserver
    {
        public ValueTask OnAttemptCompletedAsync(ShellActivationAttempt attempt, CancellationToken cancellationToken)
        {
            callback(attempt);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellationGatedObserver(
        TaskCompletionSource<ShellActivationAttempt> entered,
        TaskCompletionSource release) : IShellActivationAttemptObserver
    {
        public async ValueTask OnAttemptCompletedAsync(ShellActivationAttempt attempt, CancellationToken cancellationToken)
        {
            entered.TrySetResult(attempt);
            await release.Task.WaitAsync(Timeout, cancellationToken);
        }
    }

    private sealed class CustomRunner : IShellActivationRunner
    {
        public IShellActivationRun Start(IReadOnlyList<string> shellNames, ShellActivationRetryPolicy? retryPolicy = null,
            IShellActivationAttemptObserver? observer = null, CancellationToken startupCancellationToken = default)
            => throw new NotSupportedException("The custom test runner should only prove TryAdd preservation.");
    }

    private sealed class ClassifiedException(string message) : Exception(message) { }

    private abstract class RegistryDecorator(IShellRegistry inner) : IShellRegistry
    {
        protected IShellRegistry Inner { get; } = inner;
        public virtual Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default) => Inner.GetOrActivateAsync(name, cancellationToken);
        public virtual Task<IShell> ActivateAsync(string name, CancellationToken cancellationToken = default) => Inner.ActivateAsync(name, cancellationToken);
        public virtual Task<ReloadResult> ReloadAsync(string name, CancellationToken cancellationToken = default) => Inner.ReloadAsync(name, cancellationToken);
        public virtual Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(ReloadOptions? options = null, CancellationToken cancellationToken = default) => Inner.ReloadActiveAsync(options, cancellationToken);
        public virtual Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken cancellationToken = default) => Inner.DrainAsync(shell, cancellationToken);
        public virtual Task UnregisterBlueprintAsync(string name, CancellationToken cancellationToken = default) => Inner.UnregisterBlueprintAsync(name, cancellationToken);
        public virtual Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken cancellationToken = default) => Inner.GetBlueprintAsync(name, cancellationToken);
        public virtual Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken cancellationToken = default) => Inner.GetManagerAsync(name, cancellationToken);
        public virtual Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken cancellationToken = default) => Inner.ListAsync(query, cancellationToken);
        public virtual IShell? GetActive(string name) => Inner.GetActive(name);
        public virtual IReadOnlyCollection<IShell> GetAll(string name) => Inner.GetAll(name);
        public virtual IReadOnlyCollection<IShell> GetActiveShells() => Inner.GetActiveShells();
        public virtual void Subscribe(IShellLifecycleSubscriber subscriber) => Inner.Subscribe(subscriber);
        public virtual void Unsubscribe(IShellLifecycleSubscriber subscriber) => Inner.Unsubscribe(subscriber);
    }

    private sealed class InitialPassGateRegistry(
        IShellRegistry inner,
        List<string> attempts,
        TaskCompletionSource secondEntered,
        TaskCompletionSource releaseSecond) : RegistryDecorator(inner)
    {
        public override Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
        {
            attempts.Add(name);
            if (name == "first")
                return Task.FromException<IShell>(new InvalidOperationException("first attempt fails"));
            secondEntered.TrySetResult();
            releaseSecond.Task.WaitAsync(Timeout, cancellationToken).GetAwaiter().GetResult();
            return Inner.GetOrActivateAsync(name, cancellationToken);
        }
    }

    private sealed class FirstFailureRegistry(IShellRegistry inner, Exception exception) : RegistryDecorator(inner)
    {
        private int calls;
        public override Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
            => Interlocked.Increment(ref calls) == 1 ? Task.FromException<IShell>(exception) : Inner.GetOrActivateAsync(name, cancellationToken);
    }

    private sealed class FirstFailurePerTargetRegistry(IShellRegistry inner) : RegistryDecorator(inner)
    {
        private readonly HashSet<string> attempted = new(StringComparer.OrdinalIgnoreCase);
        public override Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
            => attempted.Add(name) ? Task.FromException<IShell>(new InvalidOperationException("transient")) : Inner.GetOrActivateAsync(name, cancellationToken);
    }

    private sealed class FixedCustomRegistry(IShell returned, IShell current, bool failAttempt = false) : RegistryDecorator(EmptyRegistry.Instance)
    {
        public override Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
            => failAttempt ? Task.FromException<IShell>(new InvalidOperationException("custom activation failed")) : Task.FromResult(returned);
        public override IShell? GetActive(string name) => current;
    }

    private sealed class DrainBeforeReturnRegistry(IShellRegistry inner) : RegistryDecorator(inner)
    {
        public override async Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
        {
            var shell = await Inner.GetOrActivateAsync(name, cancellationToken);
            var drain = await Inner.DrainAsync(shell, cancellationToken);
            await drain.WaitAsync().WaitAsync(Timeout);
            return shell;
        }
    }

    private sealed class BlockingReadRegistry(
        IShellRegistry inner,
        TaskCompletionSource entered,
        TaskCompletionSource release) : RegistryDecorator(inner)
    {
        private int block = 1;
        public override Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromException<IShell>(new InvalidOperationException("initial activation fails"));
        public override IShell? GetActive(string name)
        {
            if (Interlocked.Exchange(ref block, 0) == 1)
            {
                entered.TrySetResult();
                release.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
            }
            return Inner.GetActive(name);
        }
    }

    private sealed class GatedReturnRegistryProxy(
        TaskCompletionSource entered,
        TaskCompletionSource release) : RegistryDecorator(EmptyRegistry.Instance)
    {
        public IShellRegistry? InnerOverride { get; set; }
        public IShell? ReturnedShell { get; set; }
        private IShellRegistry Registry => InnerOverride ?? throw new InvalidOperationException("Inner registry was not assigned.");
        public override async Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Timeout, cancellationToken);
            return ReturnedShell ?? throw new InvalidOperationException("Returned shell was not assigned.");
        }
        public override IShell? GetActive(string name) => Registry.GetActive(name);
    }

    private sealed class StubbornRegistry(
        TaskCompletionSource entered,
        TaskCompletionSource release) : RegistryDecorator(EmptyRegistry.Instance)
    {
        public override async Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Timeout);
            throw new StubbornFailure();
        }
    }

    private sealed class CancellationCallbackRegistry(
        TaskCompletionSource activationEntered,
        TaskCompletionSource callbackEntered,
        TaskCompletionSource releaseCallback) : RegistryDecorator(EmptyRegistry.Instance)
    {
        public override async Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default)
        {
            activationEntered.TrySetResult();
            using var registration = cancellationToken.Register(() =>
            {
                callbackEntered.TrySetResult();
                releaseCallback.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
            });
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled delay unexpectedly completed.");
        }
    }

    private sealed class StubbornFailure : Exception { }

    private sealed class EmptyRegistry : RegistryDecorator
    {
        private EmptyRegistry() : base(new EmptyInnerRegistry()) { }
        public static EmptyRegistry Instance { get; } = new();
    }

    private sealed class EmptyInnerRegistry : IShellRegistry
    {
        public Task<IShell> GetOrActivateAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IShell> ActivateAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ReloadResult> ReloadAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReloadResult>> ReloadActiveAsync(ReloadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDrainOperation> DrainAsync(IShell shell, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UnregisterBlueprintAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProvidedBlueprint?> GetBlueprintAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IShellBlueprintManager?> GetManagerAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShellPage> ListAsync(ShellListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IShell? GetActive(string name) => null;
        public IReadOnlyCollection<IShell> GetAll(string name) => [];
        public IReadOnlyCollection<IShell> GetActiveShells() => [];
        public void Subscribe(IShellLifecycleSubscriber subscriber) => throw new NotSupportedException();
        public void Unsubscribe(IShellLifecycleSubscriber subscriber) => throw new NotSupportedException();
    }

    private sealed class GatedCommitParticipant(TaskCompletionSource<IShell> entered, TaskCompletionSource release) : IShellGenerationActivationParticipant
    {
        public Task PrepareAsync(IShell shell, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Commit(IShell shell)
        {
            entered.TrySetResult(shell);
            release.Task.WaitAsync(Timeout).GetAwaiter().GetResult();
        }
        public void Complete(IShell shell) { }
        public void Rollback(IShell shell) { }
    }
}
