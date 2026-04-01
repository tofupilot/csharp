using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsUpdateTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public RunsUpdateTests(TestFixture fixture)
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
    public async Task UpdateRun_ReturnsId()
    {
        var created = await CreateTestRun();
        var updated = await _client.Runs.UpdateAsync(created.Id, new RunUpdateRequestBody());
        Assert.False(string.IsNullOrEmpty(updated.Id));
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public async Task UpdateRun_Nonexistent_ThrowsNotFound()
    {
        var fakeId = Guid.NewGuid().ToString();
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Runs.UpdateAsync(fakeId, new RunUpdateRequestBody()));
    }
}
