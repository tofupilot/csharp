using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsGetTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public RunsGetTests(TestFixture fixture)
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
    public async Task GetRun_ReturnsMatchingId()
    {
        var created = await CreateTestRun();
        var fetched = await _client.Runs.GetAsync(created.Id);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task GetRun_Nonexistent_ThrowsNotFound()
    {
        var fakeId = Guid.NewGuid().ToString();
        await Assert.ThrowsAsync<NotFoundException>(() => _client.Runs.GetAsync(fakeId));
    }

    [Fact]
    public async Task GetRun_IncludesPhasesAndMeasurements()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;

        var created = await _client.Runs.CreateAsync(new RunCreateRequest
        {
            SerialNumber = $"SN-{uid}",
            ProcedureId = _procedureId,
            PartNumber = $"PART-{uid}",
            StartedAt = now.AddMinutes(-5),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
            Phases = new List<RunCreatePhases>
            {
                new RunCreatePhases
                {
                    Name = "init_phase",
                    Outcome = RunCreatePhasesOutcome.Pass,
                    StartedAt = now.AddMinutes(-5),
                    EndedAt = now.AddMinutes(-3),
                    Measurements = new List<RunCreateMeasurements>
                    {
                        new RunCreateMeasurements
                        {
                            Name = "boot_time_ms",
                            Outcome = RunCreateMeasurementsOutcome.Pass,
                            MeasuredValue = 120.5,
                        },
                        new RunCreateMeasurements
                        {
                            Name = "memory_mb",
                            Outcome = RunCreateMeasurementsOutcome.Pass,
                            MeasuredValue = 256,
                        },
                    },
                },
                new RunCreatePhases
                {
                    Name = "stress_phase",
                    Outcome = RunCreatePhasesOutcome.Pass,
                    StartedAt = now.AddMinutes(-3),
                    EndedAt = now,
                    Measurements = new List<RunCreateMeasurements>
                    {
                        new RunCreateMeasurements
                        {
                            Name = "cpu_temp",
                            Outcome = RunCreateMeasurementsOutcome.Pass,
                            MeasuredValue = 65.2,
                        },
                    },
                },
            },
        });

        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        Assert.Equal(2, fetched.Phases.Count);

        var initPhase = fetched.Phases.Find(p => p.Name == "init_phase");
        Assert.NotNull(initPhase);
        Assert.Equal(2, initPhase!.Measurements.Count);

        var stressPhase = fetched.Phases.Find(p => p.Name == "stress_phase");
        Assert.NotNull(stressPhase);
        Assert.Single(stressPhase!.Measurements);
        Assert.Equal("cpu_temp", stressPhase.Measurements[0].Name);
    }

    [Fact]
    public async Task GetRun_IncludesLogs()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;

        var created = await _client.Runs.CreateAsync(new RunCreateRequest
        {
            SerialNumber = $"SN-{uid}",
            ProcedureId = _procedureId,
            PartNumber = $"PART-{uid}",
            StartedAt = now.AddMinutes(-5),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
            Logs = new List<RunCreateLogs>
            {
                new RunCreateLogs
                {
                    Level = RunCreateLevel.Info,
                    Timestamp = now.AddMinutes(-4),
                    Message = "Initializing device",
                    SourceFile = "device.cs",
                    LineNumber = 10,
                },
                new RunCreateLogs
                {
                    Level = RunCreateLevel.Error,
                    Timestamp = now.AddMinutes(-1),
                    Message = "Recovered from transient fault",
                    SourceFile = "fault_handler.cs",
                    LineNumber = 55,
                },
            },
        });

        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Logs);
        Assert.Equal(2, fetched.Logs.Count);
        Assert.Contains(fetched.Logs, l => l.Message == "Initializing device" && l.SourceFile == "device.cs");
        Assert.Contains(fetched.Logs, l => l.Message == "Recovered from transient fault" && l.LineNumber == 55);
    }
}
