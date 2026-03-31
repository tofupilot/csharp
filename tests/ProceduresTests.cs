using System;
using System.Linq;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class ProceduresTests
{
    private readonly TofuPilotSDK _client;
    private readonly string _procedureId;

    public ProceduresTests(TestFixture fixture)
    {
        _client = fixture.Client;
        _procedureId = fixture.ProcedureId;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task CreateProcedure_ReturnsId()
    {
        var result = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc {Uid()}",
        });
        Assert.False(string.IsNullOrEmpty(result.Id));
    }

    [Fact]
    public async Task GetProcedure_ReturnsMatchingData()
    {
        var uid = Uid();
        var name = $"Proc Get {uid}";
        var created = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = name,
        });

        var fetched = await _client.Procedures.GetAsync(created.Id);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(name, fetched.Name);
        Assert.NotEqual(default, fetched.CreatedAt);
    }

    [Fact]
    public async Task GetProcedure_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Procedures.GetAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task GetProcedure_IncludesRecentRuns()
    {
        var uid = Uid();
        var proc = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc Runs {uid}",
        });

        var now = DateTime.UtcNow;
        await _client.Runs.CreateAsync(new RunCreateRequest
        {
            ProcedureId = proc.Id,
            SerialNumber = $"SN-{uid}",
            PartNumber = $"PART-{uid}",
            StartedAt = now.AddMinutes(-1),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
        });

        var fetched = await _client.Procedures.GetAsync(proc.Id);
        Assert.NotEmpty(fetched.RecentRuns);
    }

    [Fact]
    public async Task ListProcedures_ReturnsList()
    {
        var result = await _client.Procedures.ListAsync();
        Assert.NotEmpty(result.Data);
        Assert.NotNull(result.Meta);
    }

    [Fact]
    public async Task ListProcedures_WithSearchQuery()
    {
        var uid = Uid();
        var name = $"Proc Srq {uid}";
        await _client.Procedures.CreateAsync(new ProcedureCreateRequest { Name = name });

        var result = await _client.Procedures.ListAsync(searchQuery: name);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListProcedures_Pagination()
    {
        for (int i = 0; i < 3; i++)
        {
            await _client.Procedures.CreateAsync(new ProcedureCreateRequest
            {
                Name = $"Proc Pg {Uid()}",
            });
        }

        var page1 = await _client.Procedures.ListAsync(limit: 1);
        Assert.Single(page1.Data);

        if (page1.Meta.HasMore)
        {
            var page2 = await _client.Procedures.ListAsync(limit: 1, cursor: page1.Meta.NextCursor);
            Assert.Single(page2.Data);
            Assert.NotEqual(page1.Data[0].Id, page2.Data[0].Id);
        }
    }

    [Fact]
    public async Task ListProcedures_WithDateRange()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var uid = Uid();
        await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc Date {uid}",
        });
        var after = DateTime.UtcNow.AddSeconds(1);

        var result = await _client.Procedures.ListAsync(
            createdAfter: before,
            createdBefore: after);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task DeleteProcedure_ReturnsId()
    {
        var created = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc Del {Uid()}",
        });
        var deleted = await _client.Procedures.DeleteAsync(created.Id);
        Assert.Equal(created.Id, deleted.Id);
    }

    [Fact]
    public async Task DeleteProcedure_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Procedures.DeleteAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task UpdateProcedure_Name()
    {
        var uid = Uid();
        var created = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc Old {uid}",
        });

        var newName = $"Proc New {uid}";
        var updated = await _client.Procedures.UpdateAsync(created.Id, new ProcedureUpdateRequestBody
        {
            Name = newName,
        });
        Assert.False(string.IsNullOrEmpty(updated.Id));

        var fetched = await _client.Procedures.GetAsync(created.Id);
        Assert.Equal(newName, fetched.Name);
    }

    [Fact]
    public async Task UpdateProcedure_EmptyName_Fails()
    {
        var created = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc Empty {Uid()}",
        });

        await Assert.ThrowsAnyAsync<Exception>(
            () => _client.Procedures.UpdateAsync(created.Id, new ProcedureUpdateRequestBody
            {
                Name = "",
            }));
    }

    [Fact]
    public async Task UpdateProcedure_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<ErrorNOTFOUND>(
            () => _client.Procedures.UpdateAsync(Guid.NewGuid().ToString(), new ProcedureUpdateRequestBody
            {
                Name = "Doesn't matter",
            }));
    }

    [Fact]
    public async Task UpdateProcedure_MultipleUpdates()
    {
        var uid = Uid();
        var created = await _client.Procedures.CreateAsync(new ProcedureCreateRequest
        {
            Name = $"Proc Multi {uid}",
        });

        for (int i = 1; i <= 3; i++)
        {
            await _client.Procedures.UpdateAsync(created.Id, new ProcedureUpdateRequestBody
            {
                Name = $"Proc Multi {uid} v{i}",
            });
        }

        var fetched = await _client.Procedures.GetAsync(created.Id);
        Assert.Equal($"Proc Multi {uid} v3", fetched.Name);
    }
}
