using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class UnitsTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public UnitsTests(TestFixture fixture)
    {
        _client = fixture.Client;
        _procedureId = fixture.ProcedureId;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(string partNumber, string serial)> CreatePartAndUnit(string? prefix = null)
    {
        var uid = Uid();
        var partNumber = $"PART-{prefix ?? "U"}-{uid}";
        var serial = $"SN-{prefix ?? "U"}-{uid}";
        var revNumber = $"REV-{prefix ?? "U"}-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber, Name = $"Part {uid}",
        });
        await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });
        await _client.Units.CreateAsync(new UnitCreateRequest
        {
            SerialNumber = serial, PartNumber = partNumber, RevisionNumber = revNumber,
        });
        return (partNumber, serial);
    }

    [Fact]
    public async Task CreateUnit_ReturnsId()
    {
        var uid = Uid();
        var partNumber = $"PART-CRE-{uid}";
        var revNumber = $"REV-CRE-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = partNumber, Name = $"Part {uid}",
        });
        await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });
        var result = await _client.Units.CreateAsync(new UnitCreateRequest
        {
            SerialNumber = $"SN-CRE-{uid}", PartNumber = partNumber, RevisionNumber = revNumber,
        });
        Assert.False(string.IsNullOrEmpty(result.Id));
    }

    [Fact]
    public async Task GetUnit_ReturnsMatchingData()
    {
        var (partNumber, serial) = await CreatePartAndUnit("GET");
        var fetched = await _client.Units.GetAsync(serial);
        Assert.Equal(serial, fetched.SerialNumber);
        Assert.Equal(partNumber, fetched.Part.Number);
    }

    [Fact]
    public async Task GetUnit_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Units.GetAsync($"NONEXISTENT-{Uid()}"));
    }

    [Fact]
    public async Task ListUnits_ReturnsList()
    {
        await CreatePartAndUnit("LST");
        var result = await _client.Units.ListAsync();
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListUnits_FilterBySerialNumber()
    {
        var (_, serial) = await CreatePartAndUnit("FSN");
        var result = await _client.Units.ListAsync(serialNumbers: new List<string> { serial });
        Assert.Single(result.Data);
        Assert.Equal(serial, result.Data[0].SerialNumber);
    }

    [Fact]
    public async Task ListUnits_FilterByPartNumber()
    {
        var (partNumber, _) = await CreatePartAndUnit("FPN");
        var result = await _client.Units.ListAsync(partNumbers: new List<string> { partNumber });
        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, u => Assert.Equal(partNumber, u.Part.Number));
    }

    [Fact]
    public async Task ListUnits_WithSearchQuery()
    {
        var (_, serial) = await CreatePartAndUnit("SRQ");
        var result = await _client.Units.ListAsync(searchQuery: serial);
        Assert.NotEmpty(result.Data);
        Assert.Contains(result.Data, u => u.SerialNumber == serial);
    }

    [Fact]
    public async Task ListUnits_Pagination()
    {
        // Create 3 units to ensure we have enough for pagination
        for (int i = 0; i < 3; i++)
            await CreatePartAndUnit($"PAG{i}");

        var page1 = await _client.Units.ListAsync(limit: 1);
        Assert.Single(page1.Data);
        Assert.True(page1.Meta.HasMore);
        Assert.NotNull(page1.Meta.NextCursor);

        var page2 = await _client.Units.ListAsync(limit: 1, cursor: page1.Meta.NextCursor);
        Assert.Single(page2.Data);
        Assert.NotEqual(page1.Data[0].Id, page2.Data[0].Id);
    }

    [Fact]
    public async Task ListUnits_SortOrder()
    {
        await CreatePartAndUnit("SO1");
        await CreatePartAndUnit("SO2");

        var asc = await _client.Units.ListAsync(
            sortBy: UnitListSortBy.CreatedAt,
            sortOrder: UnitListSortOrder.Asc,
            limit: 2);
        var desc = await _client.Units.ListAsync(
            sortBy: UnitListSortBy.CreatedAt,
            sortOrder: UnitListSortOrder.Desc,
            limit: 2);

        Assert.True(asc.Data[0].CreatedAt <= asc.Data[1].CreatedAt);
        Assert.True(desc.Data[0].CreatedAt >= desc.Data[1].CreatedAt);
    }

    [Fact]
    public async Task ListUnits_FilterByIds()
    {
        var (_, serial) = await CreatePartAndUnit("FID");
        var fetched = await _client.Units.GetAsync(serial);
        var result = await _client.Units.ListAsync(ids: new List<string> { fetched.Id });
        Assert.Single(result.Data);
        Assert.Equal(fetched.Id, result.Data[0].Id);
    }

    [Fact]
    public async Task DeleteUnit_ReturnsIds()
    {
        var (_, serial) = await CreatePartAndUnit("DEL");
        var deleted = await _client.Units.DeleteAsync(new List<string> { serial });
        Assert.NotEmpty(deleted.Id);
    }

    [Fact]
    public async Task DeleteUnit_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Units.DeleteAsync(new List<string> { $"NONEXISTENT-{Uid()}" }));
    }

    [Fact]
    public async Task UpdateUnit_SerialNumber()
    {
        var (_, serial) = await CreatePartAndUnit("UPD");
        var newSerial = $"SN-UPNEW-{Uid()}";
        await _client.Units.UpdateAsync(serial, new UnitUpdateRequestBody
        {
            NewSerialNumber = newSerial,
        });
        var fetched = await _client.Units.GetAsync(newSerial);
        Assert.Equal(newSerial, fetched.SerialNumber);
    }

    [Fact]
    public async Task UpdateUnit_PartRevision()
    {
        var (_, serial) = await CreatePartAndUnit("UPR");
        var uid = Uid();
        var newPartNumber = $"PART-UPRNEW-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest
        {
            Number = newPartNumber, Name = $"New Part {uid}",
        });
        var revNumber = $"REV-UPRNEW-{uid}";
        await _client.Parts.Revisions.CreateAsync(newPartNumber, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });
        await _client.Units.UpdateAsync(serial, new UnitUpdateRequestBody
        {
            PartNumber = newPartNumber, RevisionNumber = revNumber,
        });
        var fetched = await _client.Units.GetAsync(serial);
        Assert.Equal(newPartNumber, fetched.Part.Number);
    }

    [Fact]
    public async Task UpdateUnit_DuplicateSerial_ThrowsConflict()
    {
        var (_, serial1) = await CreatePartAndUnit("DUP1");
        var (_, serial2) = await CreatePartAndUnit("DUP2");
        await Assert.ThrowsAsync<ConflictException>(() =>
            _client.Units.UpdateAsync(serial1, new UnitUpdateRequestBody
            {
                NewSerialNumber = serial2,
            }));
    }

    [Fact]
    public async Task CreateUnit_DuplicateSerial_ThrowsConflict()
    {
        var (partNumber, serial) = await CreatePartAndUnit("CDUP");
        var revNumber = $"REV-CDUP-{Uid()}";
        await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody
        {
            Number = revNumber,
        });
        await Assert.ThrowsAsync<ConflictException>(() =>
            _client.Units.CreateAsync(new UnitCreateRequest
            {
                SerialNumber = serial, PartNumber = partNumber, RevisionNumber = revNumber,
            }));
    }

    [Fact]
    public async Task ListUnits_FilterByRevisionNumbers()
    {
        var uid = Uid();
        var partNumber = $"PART-RV-{uid}";
        var revNumber = $"REV-RV-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest { Number = partNumber, Name = $"Part {uid}" });
        await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody { Number = revNumber });
        await _client.Units.CreateAsync(new UnitCreateRequest
        {
            SerialNumber = $"SN-RV-{uid}", PartNumber = partNumber, RevisionNumber = revNumber,
        });

        var result = await _client.Units.ListAsync(
            partNumbers: new List<string> { partNumber },
            revisionNumbers: new List<string> { revNumber });

        Assert.NotEmpty(result.Data);
        Assert.All(result.Data, u => Assert.Equal(partNumber, u.Part.Number));
    }

    [Fact]
    public async Task ListUnits_FilterByBatchNumbers()
    {
        var uid = Uid();
        var partNumber = $"PART-BN-{uid}";
        var revNumber = $"REV-BN-{uid}";
        var batchNumber = $"BATCH-{uid}";
        await _client.Parts.CreateAsync(new PartCreateRequest { Number = partNumber, Name = $"Part {uid}" });
        await _client.Parts.Revisions.CreateAsync(partNumber, new PartCreateRevisionRequestBody { Number = revNumber });

        // Create a run with batch_number to auto-create the batch and link the unit
        await _client.Runs.CreateAsync(new RunCreateRequest
        {
            SerialNumber = $"SN-BN-{uid}",
            ProcedureId = _procedureId,
            PartNumber = partNumber,
            RevisionNumber = revNumber,
            BatchNumber = batchNumber,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            EndedAt = DateTime.UtcNow,
            Outcome = RunCreateOutcome.Pass,
        });

        var result = await _client.Units.ListAsync(
            partNumbers: new List<string> { partNumber },
            batchNumbers: new List<string> { batchNumber });

        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListUnits_FilterByCreatedAt()
    {
        var now = DateTime.UtcNow;
        var (partNumber, _) = await CreatePartAndUnit("CA");

        var result = await _client.Units.ListAsync(
            partNumbers: new List<string> { partNumber },
            createdAfter: now.AddMinutes(-5),
            createdBefore: now.AddMinutes(5));

        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListUnits_ExcludeUnitsWithParent()
    {
        // Create parent and child
        var (parentPart, parentSerial) = await CreatePartAndUnit("EP");
        var (_, childSerial) = await CreatePartAndUnit("EC");

        await _client.Units.AddChildAsync(parentSerial, new UnitAddChildRequestBody
        {
            ChildSerialNumber = childSerial,
        });

        // With exclude - child should not appear
        var excluded = await _client.Units.ListAsync(
            serialNumbers: new List<string> { parentSerial, childSerial },
            excludeUnitsWithParent: true);

        Assert.All(excluded.Data, u => Assert.Equal(parentSerial, u.SerialNumber));

        // Without exclude - both should appear
        var included = await _client.Units.ListAsync(
            serialNumbers: new List<string> { parentSerial, childSerial },
            excludeUnitsWithParent: false);

        Assert.Equal(2, included.Data.Count);
    }
}
