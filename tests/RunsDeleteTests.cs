using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsDeleteTests
{
    private readonly TofuPilotSDK _client;
    private readonly string _procedureId;

    public RunsDeleteTests(TestFixture fixture)
    {
        _client = fixture.Client;
        _procedureId = fixture.ProcedureId;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private async Task<RunCreateResponse> CreateTestRun(string? uid = null)
    {
        uid ??= Uid();
        var now = DateTime.UtcNow;
        return await _client.Runs.CreateAsync(new RunCreateRequest
        {
            SerialNumber = $"SN-{uid}",
            ProcedureId = _procedureId,
            PartNumber = $"PART-{uid}",
            StartedAt = now.AddMinutes(-5),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
        });
    }

    [Fact]
    public async Task DeleteRun_ReturnsIds()
    {
        var created = await CreateTestRun();
        var deleted = await _client.Runs.DeleteAsync(new List<string> { created.Id });

        Assert.NotEmpty(deleted.Id);
        Assert.Contains(created.Id, deleted.Id);
    }

    [Fact]
    public async Task DeleteMultipleRuns_ReturnsIds()
    {
        var run1 = await CreateTestRun();
        var run2 = await CreateTestRun();

        var deleted = await _client.Runs.DeleteAsync(new List<string> { run1.Id, run2.Id });

        Assert.Equal(2, deleted.Id.Count);
        Assert.Contains(run1.Id, deleted.Id);
        Assert.Contains(run2.Id, deleted.Id);
    }

    [Fact]
    public async Task DeleteRun_Nonexistent_ThrowsNotFound()
    {
        var fakeId = Guid.NewGuid().ToString();
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Runs.DeleteAsync(new List<string> { fakeId }));
    }
}
