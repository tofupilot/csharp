using System;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class BatchesTests
{
    private readonly TofuPilot _client;

    public BatchesTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task CreateBatch_ReturnsId()
    {
        var result = await _client.Batches.CreateAsync(new BatchCreateRequest
        {
            Number = $"BATCH-{Uid()}",
        });
        Assert.False(string.IsNullOrEmpty(result.Id));
    }

    [Fact]
    public async Task GetBatch_ReturnsMatchingData()
    {
        var uid = Uid();
        var batchNumber = $"BATCH-GET-{uid}";
        await _client.Batches.CreateAsync(new BatchCreateRequest
        {
            Number = batchNumber,
        });

        var fetched = await _client.Batches.GetAsync(batchNumber);
        Assert.Equal(batchNumber, fetched.Number);
        Assert.NotEqual(default, fetched.CreatedAt);
    }

    [Fact]
    public async Task GetBatch_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Batches.GetAsync($"BATCH-NONE-{Uid()}"));
    }

    [Fact]
    public async Task ListBatches_ReturnsList()
    {
        await _client.Batches.CreateAsync(new BatchCreateRequest
        {
            Number = $"BATCH-LST-{Uid()}",
        });

        var result = await _client.Batches.ListAsync();
        Assert.NotEmpty(result.Data);
        Assert.NotNull(result.Meta);
    }

    [Fact]
    public async Task ListBatches_WithSearchQuery()
    {
        var uid = Uid();
        var batchNumber = $"BATCH-SRQ-{uid}";
        await _client.Batches.CreateAsync(new BatchCreateRequest
        {
            Number = batchNumber,
        });

        var result = await _client.Batches.ListAsync(searchQuery: batchNumber);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListBatches_WithNumberFilter()
    {
        var uid = Uid();
        var batchNumber = $"BATCH-NF-{uid}";
        await _client.Batches.CreateAsync(new BatchCreateRequest
        {
            Number = batchNumber,
        });

        var result = await _client.Batches.ListAsync(
            numbers: new System.Collections.Generic.List<string> { batchNumber });
        Assert.NotEmpty(result.Data);
        Assert.Equal(batchNumber, result.Data[0].Number);
    }

    [Fact]
    public async Task ListBatches_Pagination()
    {
        for (int i = 0; i < 3; i++)
        {
            await _client.Batches.CreateAsync(new BatchCreateRequest
            {
                Number = $"BATCH-PG-{Uid()}",
            });
        }

        var page1 = await _client.Batches.ListAsync(limit: 1);
        Assert.Single(page1.Data);

        if (page1.Meta.HasMore)
        {
            var page2 = await _client.Batches.ListAsync(limit: 1, cursor: page1.Meta.NextCursor);
            Assert.Single(page2.Data);
            Assert.NotEqual(page1.Data[0].Id, page2.Data[0].Id);
        }
    }

    [Fact]
    public async Task ListBatches_SortOrder()
    {
        var uid1 = Uid();
        var uid2 = Uid();
        await _client.Batches.CreateAsync(new BatchCreateRequest { Number = $"BATCH-SO-{uid1}" });
        await _client.Batches.CreateAsync(new BatchCreateRequest { Number = $"BATCH-SO-{uid2}" });

        var asc = await _client.Batches.ListAsync(
            sortBy: BatchListSortBy.CreatedAt,
            sortOrder: BatchListSortOrder.Asc,
            limit: 2);
        var desc = await _client.Batches.ListAsync(
            sortBy: BatchListSortBy.CreatedAt,
            sortOrder: BatchListSortOrder.Desc,
            limit: 2);

        Assert.NotEmpty(asc.Data);
        Assert.NotEmpty(desc.Data);
        Assert.True(asc.Data[0].CreatedAt <= asc.Data[1].CreatedAt);
        Assert.True(desc.Data[0].CreatedAt >= desc.Data[1].CreatedAt);
    }

    [Fact]
    public async Task DeleteBatch_ReturnsId()
    {
        var uid = Uid();
        var batchNumber = $"BATCH-DEL-{uid}";
        var created = await _client.Batches.CreateAsync(new BatchCreateRequest
        {
            Number = batchNumber,
        });

        var deleted = await _client.Batches.DeleteAsync(batchNumber);
        Assert.Contains(created.Id, deleted.Id);
    }

    [Fact]
    public async Task DeleteBatch_Nonexistent_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Batches.DeleteAsync($"BATCH-NONE-{Uid()}"));
    }

    [Fact]
    public async Task UpdateBatch_Number()
    {
        var uid = Uid();
        var oldNumber = $"BATCH-UPO-{uid}";
        var newNumber = $"BATCH-UPN-{uid}";
        await _client.Batches.CreateAsync(new BatchCreateRequest
        {
            Number = oldNumber,
        });

        await _client.Batches.UpdateAsync(oldNumber, new BatchUpdateRequestBody
        {
            NewNumber = newNumber,
        });

        var fetched = await _client.Batches.GetAsync(newNumber);
        Assert.Equal(newNumber, fetched.Number);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _client.Batches.GetAsync(oldNumber));
    }

    [Fact]
    public async Task UpdateBatch_DuplicateNumber_ThrowsConflict()
    {
        var uid = Uid();
        var number1 = $"BATCH-DUP1-{uid}";
        var number2 = $"BATCH-DUP2-{uid}";
        await _client.Batches.CreateAsync(new BatchCreateRequest { Number = number1 });
        await _client.Batches.CreateAsync(new BatchCreateRequest { Number = number2 });

        await Assert.ThrowsAsync<ConflictException>(
            () => _client.Batches.UpdateAsync(number2, new BatchUpdateRequestBody
            {
                NewNumber = number1,
            }));
    }

    [Fact]
    public async Task CreateBatch_EmptyNumber_Fails()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            () => _client.Batches.CreateAsync(new BatchCreateRequest
            {
                Number = "",
            }));
    }
}
