using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Errors;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsCreateTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public RunsCreateTests(TestFixture fixture)
    {
        _client = fixture.Client;
        _procedureId = fixture.ProcedureId;
    }

    private string Uid() => Guid.NewGuid().ToString("N")[..8];

    private RunCreateRequest BaseRequest(string? uid = null)
    {
        uid ??= Uid();
        var now = DateTime.UtcNow;
        return new RunCreateRequest
        {
            SerialNumber = $"SN-{uid}",
            ProcedureId = _procedureId,
            PartNumber = $"PART-{uid}",
            StartedAt = now.AddMinutes(-5),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
        };
    }

    [Fact]
    public async Task CreateRun_ReturnsId()
    {
        var result = await _client.Runs.CreateAsync(BaseRequest());
        Assert.False(string.IsNullOrEmpty(result.Id));
    }

    [Fact]
    public async Task CreateRun_WithProcedureVersion()
    {
        var uid = Uid();
        var req = BaseRequest(uid);
        req.ProcedureVersion = "1.2.3";

        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Procedure.Version);
        Assert.Equal("1.2.3", fetched.Procedure.Version!.Tag);
    }

    [Fact]
    public async Task CreateRun_WithDocstring()
    {
        var uid = Uid();
        var req = BaseRequest(uid);
        req.Docstring = "Test docstring for verification";

        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal("Test docstring for verification", fetched.Docstring);
    }

    [Fact]
    public async Task CreateRun_WithPhases()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "voltage_check",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "output_voltage",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = 3.3,
                        Validators = new List<RunCreateValidators>
                        {
                            new RunCreateValidators
                            {
                                Operator = ">=",
                                ExpectedValue = RunCreateExpectedValue.CreateNumber(0),
                            },
                            new RunCreateValidators
                            {
                                Operator = "<=",
                                ExpectedValue = RunCreateExpectedValue.CreateNumber(5),
                            },
                        },
                    },
                },
            },
        };

        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        Assert.Single(fetched.Phases);
        Assert.Equal("voltage_check", fetched.Phases[0].Name);
        Assert.NotEmpty(fetched.Phases[0].Measurements);
        Assert.Equal("output_voltage", fetched.Phases[0].Measurements[0].Name);
    }

    [Fact]
    public async Task CreateRun_WithLogs()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Logs = new List<RunCreateLogs>
        {
            new RunCreateLogs
            {
                Level = RunCreateLevel.Info,
                Timestamp = now.AddMinutes(-4),
                Message = "Test started successfully",
                SourceFile = "test_runner.cs",
                LineNumber = 42,
            },
            new RunCreateLogs
            {
                Level = RunCreateLevel.Warning,
                Timestamp = now.AddMinutes(-2),
                Message = "Voltage slightly above nominal",
                SourceFile = "test_runner.cs",
                LineNumber = 88,
            },
        };

        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Logs);
        Assert.Equal(2, fetched.Logs.Count);
        Assert.Contains(fetched.Logs, l => l.Message == "Test started successfully");
        Assert.Contains(fetched.Logs, l => l.Message == "Voltage slightly above nominal");
    }

    [Fact]
    public async Task CreateRun_EmptySerialNumber_Fails()
    {
        var req = BaseRequest();
        req.SerialNumber = "";

        await Assert.ThrowsAsync<BadRequestException>(() => _client.Runs.CreateAsync(req));
    }

    [Fact]
    public async Task CreateRun_InvalidProcedureId_Fails()
    {
        var req = BaseRequest();
        req.ProcedureId = Guid.NewGuid().ToString();

        await Assert.ThrowsAsync<NotFoundException>(() => _client.Runs.CreateAsync(req));
    }

    [Fact]
    public async Task CreateRun_WithLegacyLimits()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        #pragma warning disable CS0618
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "limit_phase",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "temperature",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = 25.0,
                        LowerLimit = 10.0,
                        UpperLimit = 40.0,
                    },
                },
            },
        };
        #pragma warning restore CS0618

        var created = await _client.Runs.CreateAsync(req);
        Assert.False(string.IsNullOrEmpty(created.Id));

        var fetched = await _client.Runs.GetAsync(created.Id);
        Assert.NotNull(fetched.Phases);
        Assert.Single(fetched.Phases);
        Assert.NotEmpty(fetched.Phases[0].Measurements);
    }

    [Fact]
    public async Task CreateRun_WithAggregations()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "agg_phase",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "signal_strength",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = 75.5,
                        Aggregations = new List<RunCreateMeasurementsAggregations>
                        {
                            new RunCreateMeasurementsAggregations
                            {
                                Type = "avg",
                                Value = RunCreateMeasurementsValue.CreateNumber(72.3),
                                Outcome = "PASS",
                            },
                            new RunCreateMeasurementsAggregations
                            {
                                Type = "max",
                                Value = RunCreateMeasurementsValue.CreateNumber(80.1),
                            },
                        },
                    },
                },
            },
        };

        var created = await _client.Runs.CreateAsync(req);
        Assert.False(string.IsNullOrEmpty(created.Id));
    }

    [Fact]
    public async Task CreateRun_WithSubUnits()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;

        // Create sub-unit runs first so the sub-units exist
        var subSerial1 = $"SUB1-{uid}";
        var subSerial2 = $"SUB2-{uid}";

        await _client.Runs.CreateAsync(new RunCreateRequest
        {
            SerialNumber = subSerial1,
            ProcedureId = _procedureId,
            PartNumber = $"SUB-PART-{uid}",
            StartedAt = now.AddMinutes(-10),
            EndedAt = now.AddMinutes(-8),
            Outcome = RunCreateOutcome.Pass,
        });

        await _client.Runs.CreateAsync(new RunCreateRequest
        {
            SerialNumber = subSerial2,
            ProcedureId = _procedureId,
            PartNumber = $"SUB-PART-{uid}",
            StartedAt = now.AddMinutes(-10),
            EndedAt = now.AddMinutes(-8),
            Outcome = RunCreateOutcome.Pass,
        });

        // Create main run with sub-units
        var mainReq = BaseRequest(uid);
        mainReq.SubUnits = new List<string> { subSerial1, subSerial2 };

        var created = await _client.Runs.CreateAsync(mainReq);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.SubUnits);
        Assert.Equal(2, fetched.SubUnits.Count);
        Assert.Contains(fetched.SubUnits, su => su.SerialNumber.Equals(subSerial1, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fetched.SubUnits, su => su.SerialNumber.Equals(subSerial2, StringComparison.OrdinalIgnoreCase));
    }
}
