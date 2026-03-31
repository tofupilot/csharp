using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsListTests
{
    private readonly TofuPilotSDK _client;
    private readonly string _procedureId;

    public RunsListTests(TestFixture fixture)
    {
        _client = fixture.Client;
        _procedureId = fixture.ProcedureId;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private async Task<RunCreateResponse> CreateTestRun(string? uid = null, string? partNumber = null, string? serialNumber = null, RunCreateOutcome outcome = RunCreateOutcome.Pass, DateTime? startedAt = null)
    {
        uid ??= Uid();
        var now = DateTime.UtcNow;
        var started = startedAt ?? now.AddMinutes(-5);
        return await _client.Runs.CreateAsync(new RunCreateRequest
        {
            SerialNumber = serialNumber ?? $"SN-{uid}",
            ProcedureId = _procedureId,
            PartNumber = partNumber ?? $"PART-{uid}",
            StartedAt = started,
            EndedAt = started.AddMinutes(2),
            Outcome = outcome,
        });
    }

    [Fact]
    public async Task ListRuns_ReturnsData()
    {
        var uid = Uid();
        await CreateTestRun(uid);

        var result = await _client.Runs.ListAsync(partNumbers: new List<string> { $"PART-{uid}" });
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListRuns_FilterByOutcome()
    {
        var uid = Uid();
        var part = $"PART-OUT-{uid}";

        var passRun = await CreateTestRun(partNumber: part, serialNumber: $"SN-P-{uid}", outcome: RunCreateOutcome.Pass);
        await CreateTestRun(partNumber: part, serialNumber: $"SN-F-{uid}", outcome: RunCreateOutcome.Fail);

        var result = await _client.Runs.ListAsync(
            outcomes: new List<RunListQueryParamOutcome> { RunListQueryParamOutcome.Pass },
            partNumbers: new List<string> { part });

        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, r => Assert.Equal(RunListDataOutcome.Pass, r.Outcome));
        Assert.Contains(result.Data, r => r.Id == passRun.Id);
    }

    [Fact]
    public async Task ListRuns_FilterByProcedureId()
    {
        var uid = Uid();
        await CreateTestRun(uid);

        var result = await _client.Runs.ListAsync(procedureIds: new List<string> { _procedureId });
        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, r => Assert.Equal(_procedureId, r.Procedure.Id));
    }

    [Fact]
    public async Task ListRuns_FilterBySerialNumber()
    {
        var uid = Uid();
        var serial = $"SN-FILT-{uid}";
        await CreateTestRun(serialNumber: serial);

        var result = await _client.Runs.ListAsync(serialNumbers: new List<string> { serial });
        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, r => Assert.Equal(serial, r.Unit.SerialNumber, ignoreCase: true));
    }

    [Fact]
    public async Task ListRuns_FilterByPartNumber()
    {
        var uid = Uid();
        var part = $"PART-FILT-{uid}";
        await CreateTestRun(partNumber: part);

        var result = await _client.Runs.ListAsync(partNumbers: new List<string> { part });
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListRuns_FilterByDateRange()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        await CreateTestRun(uid, startedAt: now.AddMinutes(-5));

        var result = await _client.Runs.ListAsync(
            partNumbers: new List<string> { $"PART-{uid}" },
            startedAfter: now.AddMinutes(-10),
            startedBefore: now.AddMinutes(1));

        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListRuns_Pagination()
    {
        var uid = Uid();
        var part = $"PART-PAGE-{uid}";

        for (int i = 0; i < 3; i++)
        {
            await CreateTestRun(partNumber: part, serialNumber: $"SN-PG-{i}-{uid}");
        }

        var page1 = await _client.Runs.ListAsync(partNumbers: new List<string> { part }, limit: 1);
        Assert.Single(page1.Data);
        Assert.True(page1.Meta.HasMore);
        Assert.NotNull(page1.Meta.NextCursor);

        var page2 = await _client.Runs.ListAsync(
            partNumbers: new List<string> { part },
            limit: 1,
            cursor: page1.Meta.NextCursor);
        Assert.Single(page2.Data);
        Assert.NotEqual(page1.Data[0].Id, page2.Data[0].Id);
    }

    [Fact]
    public async Task ListRuns_SortOrder()
    {
        var uid = Uid();
        var part = $"PART-SORT-{uid}";
        var now = DateTime.UtcNow;

        await CreateTestRun(partNumber: part, serialNumber: $"SN-S1-{uid}", startedAt: now.AddMinutes(-10));
        await CreateTestRun(partNumber: part, serialNumber: $"SN-S2-{uid}", startedAt: now.AddMinutes(-5));

        var descResult = await _client.Runs.ListAsync(
            partNumbers: new List<string> { part },
            sortBy: RunListSortBy.StartedAt,
            sortOrder: RunListSortOrder.Desc);
        Assert.True(descResult.Data.Count >= 2);
        Assert.True(descResult.Data[0].StartedAt >= descResult.Data[1].StartedAt);

        var ascResult = await _client.Runs.ListAsync(
            partNumbers: new List<string> { part },
            sortBy: RunListSortBy.StartedAt,
            sortOrder: RunListSortOrder.Asc);
        Assert.True(ascResult.Data.Count >= 2);
        Assert.True(ascResult.Data[0].StartedAt <= ascResult.Data[1].StartedAt);
    }

    [Fact]
    public async Task ListRuns_EmptyResult()
    {
        var result = await _client.Runs.ListAsync(
            serialNumbers: new List<string> { $"NONEXISTENT-{Uid()}" });
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task ListRuns_FilterByIds()
    {
        var uid = Uid();
        var run1 = await CreateTestRun(serialNumber: $"SN-ID1-{uid}");
        var run2 = await CreateTestRun(serialNumber: $"SN-ID2-{uid}");

        var result = await _client.Runs.ListAsync(
            ids: new List<string> { run1.Id, run2.Id });

        Assert.Equal(2, result.Data.Count);
        var returnedIds = result.Data.Select(r => r.Id).ToHashSet();
        Assert.Contains(run1.Id, returnedIds);
        Assert.Contains(run2.Id, returnedIds);
    }
}
