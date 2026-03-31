using System;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RevisionsTests
{
    private readonly TofuPilotSDK _client;

    public RevisionsTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private async Task<string> CreatePartAsync(string? uid = null)
    {
        uid ??= Uid();
        var partNumber = $"PART-RV-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber,
            Name = $"Rev Part {uid}",
        });
        return partNumber;
    }

    [Fact]
    public async Task CreateRevision_ReturnsId()
    {
        var uid = Uid();
        var partNumber = await CreatePartAsync(uid);

        var revision = await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
        {
            Number = $"REV-{uid}",
        });
        Assert.False(string.IsNullOrEmpty(revision.Id));
    }

    [Fact]
    public async Task GetRevision_ReturnsMatchingData()
    {
        var uid = Uid();
        var partNumber = await CreatePartAsync(uid);
        var revNumber = $"REV-G-{uid}";

        var created = await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });

        var fetched = await _client.Parts.Revisions.GetAsync(partNumber, revNumber);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(revNumber, fetched.Number);
        Assert.NotNull(fetched.Part);
    }

    [Fact]
    public async Task GetRevision_Nonexistent_ThrowsNotFound()
    {
        var uid = Uid();
        var partNumber = await CreatePartAsync(uid);

        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Parts.Revisions.GetAsync(partNumber, $"REV-NONE-{Uid()}"));
    }

    [Fact]
    public async Task DeleteRevision_ReturnsId()
    {
        var uid = Uid();
        var partNumber = await CreatePartAsync(uid);
        var revNumber = $"REV-D-{uid}";

        var created = await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });

        var deleted = await _client.Parts.Revisions.DeleteAsync(partNumber, revNumber);
        Assert.Equal(created.Id, deleted.Id);
    }

    [Fact]
    public async Task DeleteRevision_Nonexistent_ThrowsNotFound()
    {
        var uid = Uid();
        var partNumber = await CreatePartAsync(uid);

        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Parts.Revisions.DeleteAsync(partNumber, $"REV-NONE-{Uid()}"));
    }

    [Fact]
    public async Task CreateRevision_DuplicateOnSamePart_ThrowsConflict()
    {
        var uid = Uid();
        var partNumber = await CreatePartAsync(uid);
        var revNumber = $"REV-DUP-{uid}";

        await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });

        await Assert.ThrowsAsync<ErrorCONFLICT>(
            () => _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
            {
                Number = revNumber,
            }));
    }

    [Fact]
    public async Task CreateRevision_SameNumberDifferentParts_Succeeds()
    {
        var uid = Uid();
        var revNumber = $"REV-SHARED-{uid}";

        var partNumber1 = await CreatePartAsync($"{uid}a");
        var partNumber2 = await CreatePartAsync($"{uid}b");

        var rev1 = await _client.Parts.Revisions.CreateAsync(partNumber1, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });
        var rev2 = await _client.Parts.Revisions.CreateAsync(partNumber2, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });

        Assert.False(string.IsNullOrEmpty(rev1.Id));
        Assert.False(string.IsNullOrEmpty(rev2.Id));
        Assert.NotEqual(rev1.Id, rev2.Id);
    }

    [Fact]
    public async Task CreateRevision_InvalidPartNumber_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Parts.Revisions.CreateAsync($"PART-INVALID-{Uid()}", new PartCreateRevisionRequestBody
            {
                Number = $"REV-{Uid()}",
            }));
    }
}
