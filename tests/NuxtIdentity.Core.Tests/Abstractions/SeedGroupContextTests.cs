using NUnit.Framework;
using NuxtIdentity.Core.Abstractions;

namespace NuxtIdentity.Core.Tests.Abstractions;

[TestFixture]
public sealed class SeedGroupContextTests
{
    [Test]
    public async Task RunAsync_WithBindingErrorItem_RecordsFailureWithoutInvokingOperation()
    {
        // Given: An item that failed to bind.
        var context = new RecordingContext();
        var item = new SeedItem<SampleOptions>(new SampleOptions(), "Seeding:Demo:Lists:0", "Binding failed.");
        var invoked = false;

        // When: The item is run through the convenience overload.
        await context.RunAsync(item, "list", "Groceries", () =>
        {
            invoked = true;
            return Task.FromResult(SeedResult.Success());
        });

        // Then: The operation is not invoked and the binding error is recorded as a failure at the item path.
        Assert.That(invoked, Is.False);
        Assert.That(context.Calls, Has.Count.EqualTo(1));
        var call = context.Calls[0];
        Assert.That(call.Path, Is.EqualTo("Seeding:Demo:Lists:0"));
        Assert.That(call.ItemKind, Is.EqualTo("list"));
        Assert.That(call.IdentifyingKey, Is.EqualTo("Groceries"));
        Assert.That(call.Result.Outcome, Is.EqualTo(SeedOutcome.Failed));
        Assert.That(call.Result.Reason, Is.EqualTo("Binding failed."));
    }

    [Test]
    public async Task RunAsync_WithValidItem_InvokesOperationAtItemPath()
    {
        // Given: An item that bound successfully.
        var context = new RecordingContext();
        var item = new SeedItem<SampleOptions>(new SampleOptions { Name = "Groceries" }, "Seeding:Demo:Lists:1", null);

        // When: The item is run through the convenience overload.
        await context.RunAsync(item, "list", item.Value.Name!, () => Task.FromResult(SeedResult.AlreadyExists("List already exists.")));

        // Then: The operation's result is recorded at the item path.
        Assert.That(context.Calls, Has.Count.EqualTo(1));
        Assert.That(context.Calls[0].Path, Is.EqualTo("Seeding:Demo:Lists:1"));
        Assert.That(context.Calls[0].Result.Outcome, Is.EqualTo(SeedOutcome.AlreadyExists));
        Assert.That(context.Calls[0].Result.Reason, Is.EqualTo("List already exists."));
    }

    [Test]
    public void SeedResult_FactoriesSetOutcomeAndReason()
    {
        // Given/When: Each result factory is called.
        var success = SeedResult.Success();
        var exists = SeedResult.AlreadyExists("exists");
        var failure = SeedResult.Failure("failed");
        var skipped = SeedResult.Skipped("skipped");

        // Then: Each result carries the matching outcome and reason.
        Assert.That((success.Outcome, success.Reason), Is.EqualTo((SeedOutcome.Succeeded, (string?)null)));
        Assert.That((exists.Outcome, exists.Reason), Is.EqualTo((SeedOutcome.AlreadyExists, (string?)"exists")));
        Assert.That((failure.Outcome, failure.Reason), Is.EqualTo((SeedOutcome.Failed, (string?)"failed")));
        Assert.That((skipped.Outcome, skipped.Reason), Is.EqualTo((SeedOutcome.Skipped, (string?)"skipped")));
    }

    private sealed class SampleOptions
    {
        public string? Name { get; set; }
    }

    private sealed record RecordedCall(string ItemKind, string Path, string IdentifyingKey, SeedResult Result);

    private sealed class RecordingContext : SeedGroupContext
    {
        public List<RecordedCall> Calls { get; } = new();

        public override string GroupName => "Demo";

        public override string Path => "Seeding:Demo";

        public override IEnumerable<SeedItem<T>> Items<T>(string key) => [];

        public override async Task RunAsync(
            string itemKind,
            string path,
            string identifyingKey,
            Func<Task<SeedResult>> operation)
        {
            Calls.Add(new RecordedCall(itemKind, path, identifyingKey, await operation()));
        }
    }
}
