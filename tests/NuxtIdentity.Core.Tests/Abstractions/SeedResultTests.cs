using NUnit.Framework;
using NuxtIdentity.Core.Abstractions;

namespace NuxtIdentity.Core.Tests.Abstractions;

[TestFixture]
public sealed class SeedResultTests
{
    [Test]
    public void Factories_SetOutcomeAndReason()
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

    [Test]
    public void SeedItem_ToStringOmitsValue()
    {
        // Given: An item whose bound value contains a secret.
        var item = new SeedItem<SecretOptions>(
            new SecretOptions { Password = "secret-password" }, "Seeding:Demo:Accounts:0", null);

        // When: The item is formatted.
        var text = item.ToString();

        // Then: The path is included but the bound value is not.
        Assert.That(text, Does.Contain("Seeding:Demo:Accounts:0"));
        Assert.That(text, Does.Not.Contain("secret-password"));
        Assert.That(text, Does.Not.Contain(nameof(SeedItem<SecretOptions>.Value)));
    }

    private sealed record SecretOptions
    {
        public string? Password { get; init; }
    }
}
