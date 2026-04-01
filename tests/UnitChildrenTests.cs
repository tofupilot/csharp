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
public class UnitChildrenTests
{
    private readonly TofuPilot _client;

    public UnitChildrenTests(TestFixture fixture)
    {
        _client = fixture.Client;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private async Task<string> CreateUnit(string prefix)
    {
        var uid = Uid();
        var partNumber = $"PART-{prefix}-{uid}";
        var serial = $"SN-{prefix}-{uid}";
        var revNumber = $"REV-{prefix}-{uid}";
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
        return serial;
    }

    [Fact]
    public async Task AddChild_Success()
    {
        var parent = await CreateUnit("ACHP");
        var child = await CreateUnit("ACHC");

        await _client.Units.AddChildAsync(parent, new UnitAddChildRequestBody
        {
            ChildSerialNumber = child,
        });

        var parentUnit = await _client.Units.GetAsync(parent);
        Assert.NotNull(parentUnit.Children);
        Assert.Contains(parentUnit.Children, c => c.SerialNumber == child);

        var childUnit = await _client.Units.GetAsync(child);
        Assert.NotNull(childUnit.Parent);
        Assert.Equal(parent, childUnit.Parent.SerialNumber);
    }

    [Fact]
    public async Task AddMultipleChildren()
    {
        var parent = await CreateUnit("AMCP");
        var children = new List<string>();
        for (int i = 0; i < 3; i++)
            children.Add(await CreateUnit($"AMC{i}"));

        foreach (var child in children)
        {
            await _client.Units.AddChildAsync(parent, new UnitAddChildRequestBody
            {
                ChildSerialNumber = child,
            });
        }

        var parentUnit = await _client.Units.GetAsync(parent);
        Assert.NotNull(parentUnit.Children);
        Assert.Equal(3, parentUnit.Children.Count);
    }

    [Fact]
    public async Task RemoveChild_Success()
    {
        var parent = await CreateUnit("RMCP");
        var child = await CreateUnit("RMCC");
        await _client.Units.AddChildAsync(parent, new UnitAddChildRequestBody
        {
            ChildSerialNumber = child,
        });

        await _client.Units.RemoveChildAsync(parent, child);

        var parentUnit = await _client.Units.GetAsync(parent);
        var hasChild = parentUnit.Children?.Any(c => c.SerialNumber == child) ?? false;
        Assert.False(hasChild);
    }

    [Fact]
    public async Task RemoveChild_FromMultiple()
    {
        var parent = await CreateUnit("RFMP");
        var children = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var c = await CreateUnit($"RFM{i}");
            children.Add(c);
            await _client.Units.AddChildAsync(parent, new UnitAddChildRequestBody
            {
                ChildSerialNumber = c,
            });
        }

        await _client.Units.RemoveChildAsync(parent, children[1]);

        var parentUnit = await _client.Units.GetAsync(parent);
        Assert.NotNull(parentUnit.Children);
        Assert.Equal(2, parentUnit.Children.Count);
        Assert.DoesNotContain(parentUnit.Children, c => c.SerialNumber == children[1]);
    }

    [Fact]
    public async Task AddChild_SelfReference_Fails()
    {
        var unit = await CreateUnit("SELF");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _client.Units.AddChildAsync(unit, new UnitAddChildRequestBody
            {
                ChildSerialNumber = unit,
            }));
    }

    [Fact]
    public async Task AddChild_CycleDetection()
    {
        var a = await CreateUnit("CYCA");
        var b = await CreateUnit("CYCB");
        await _client.Units.AddChildAsync(a, new UnitAddChildRequestBody
        {
            ChildSerialNumber = b,
        });

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _client.Units.AddChildAsync(b, new UnitAddChildRequestBody
            {
                ChildSerialNumber = a,
            }));
    }

    [Fact]
    public async Task AddChild_ParentNotFound_ThrowsNotFound()
    {
        var child = await CreateUnit("ACPNF");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Units.AddChildAsync($"NONEXISTENT-{Uid()}", new UnitAddChildRequestBody
            {
                ChildSerialNumber = child,
            }));
    }

    [Fact]
    public async Task AddChild_ChildNotFound_ThrowsNotFound()
    {
        var parent = await CreateUnit("ACCNF");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Units.AddChildAsync(parent, new UnitAddChildRequestBody
            {
                ChildSerialNumber = $"NONEXISTENT-{Uid()}",
            }));
    }

    [Fact]
    public async Task RemoveChild_NotActuallyChild_Fails()
    {
        var parent = await CreateUnit("RNCP");
        var notChild = await CreateUnit("RNCC");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _client.Units.RemoveChildAsync(parent, notChild));
    }

    [Fact]
    public async Task RemoveChild_ParentNotFound_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _client.Units.RemoveChildAsync($"NONEXISTENT-{Uid()}", $"WHATEVER-{Uid()}"));
    }

    [Fact]
    public async Task ExcludeUnitsWithParent()
    {
        var parent = await CreateUnit("EXWP");
        var child = await CreateUnit("EXWC");
        await _client.Units.AddChildAsync(parent, new UnitAddChildRequestBody
        {
            ChildSerialNumber = child,
        });

        var withParent = await _client.Units.ListAsync(
            serialNumbers: new List<string> { parent, child },
            excludeUnitsWithParent: true);

        Assert.All(withParent.Data, u => Assert.NotEqual(child, u.SerialNumber));
        Assert.Contains(withParent.Data, u => u.SerialNumber == parent);
    }
}
