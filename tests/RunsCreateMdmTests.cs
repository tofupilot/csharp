using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsCreateMdmTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public RunsCreateMdmTests(TestFixture fixture)
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
            SerialNumber = $"SN-M-{uid}",
            ProcedureId = _procedureId,
            PartNumber = $"PART-M-{uid}",
            StartedAt = now.AddMinutes(-5),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
        };
    }

    [Fact]
    public async Task CreateRun_BasicXAxisYAxis()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "mdm_basic",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "frequency_response",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 100, 1000, 10000 },
                            Units = "Hz",
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { -3.0, 0.0, -6.0 },
                                Units = "dB",
                                Description = "Gain",
                            },
                        },
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        var measurement = fetched.Phases[0].Measurements[0];
        Assert.NotNull(measurement.DataSeries);
        Assert.True(measurement.DataSeries.Count >= 1);
    }

    [Fact]
    public async Task CreateRun_MultipleYAxisSeries()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "multi_y",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "iv_curve",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 0, 1, 2, 3, 4, 5 },
                            Units = "V",
                            Description = "Voltage",
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { 0, 0.1, 0.2, 0.3, 0.4, 0.5 },
                                Units = "A",
                                Description = "Current",
                            },
                            new RunCreateYAxis
                            {
                                Data = new List<double> { 0, 0.1, 0.4, 0.9, 1.6, 2.5 },
                                Units = "W",
                                Description = "Power",
                            },
                        },
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var measurement = fetched.Phases![0].Measurements[0];
        Assert.NotNull(measurement.DataSeries);
        Assert.True(measurement.DataSeries.Count >= 2);
    }

    [Fact]
    public async Task CreateRun_YAxisWithValidators()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "y_validators",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "output_signal",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 1, 2, 3 },
                            Units = "s",
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { 3.0, 3.3, 3.1 },
                                Units = "V",
                                Description = "Voltage",
                                Validators = new List<RunCreateYAxisValidators>
                                {
                                    new RunCreateYAxisValidators
                                    {
                                        Operator = ">=",
                                        ExpectedValue = RunCreateYAxisExpectedValue.CreateNumber(2.5),
                                        Outcome = "PASS",
                                    },
                                    new RunCreateYAxisValidators
                                    {
                                        Operator = "<=",
                                        ExpectedValue = RunCreateYAxisExpectedValue.CreateNumber(4.0),
                                        Outcome = "PASS",
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var measurement = fetched.Phases![0].Measurements[0];
        Assert.NotNull(measurement.DataSeries);
    }

    [Fact]
    public async Task CreateRun_YAxisWithAggregations()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "y_aggs",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "temperature_sweep",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 0, 10, 20, 30, 40 },
                            Units = "min",
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { 22.0, 23.5, 24.0, 23.8, 23.2 },
                                Units = "°C",
                                Description = "Temperature",
                                Aggregations = new List<RunCreateYAxisAggregations>
                                {
                                    new RunCreateYAxisAggregations
                                    {
                                        Type = "avg",
                                        Value = RunCreateYAxisValue.CreateNumber(23.3),
                                        Outcome = "PASS",
                                    },
                                    new RunCreateYAxisAggregations
                                    {
                                        Type = "max",
                                        Value = RunCreateYAxisValue.CreateNumber(24.0),
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var measurement = fetched.Phases![0].Measurements[0];
        Assert.NotNull(measurement.DataSeries);
    }

    [Fact]
    public async Task CreateRun_YAxisAggregationsWithValidators()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "y_agg_val",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "signal_quality",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 1, 2, 3 },
                            Units = "s",
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { 95.0, 96.0, 94.5 },
                                Units = "%",
                                Description = "Quality",
                                Aggregations = new List<RunCreateYAxisAggregations>
                                {
                                    new RunCreateYAxisAggregations
                                    {
                                        Type = "avg",
                                        Value = RunCreateYAxisValue.CreateNumber(95.17),
                                        Outcome = "PASS",
                                        Validators = new List<RunCreateYAxisAggregationsValidators>
                                        {
                                            new RunCreateYAxisAggregationsValidators
                                            {
                                                Operator = ">=",
                                                ExpectedValue = RunCreateYAxisAggregationsExpectedValue.CreateNumber(90.0),
                                                Outcome = "PASS",
                                            },
                                        },
                                    },
                                },
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
    public async Task CreateRun_XAxisWithValidators()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "x_validators",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "time_series",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 0, 1, 2, 3 },
                            Units = "s",
                            Validators = new List<RunCreateValidators>
                            {
                                new RunCreateValidators
                                {
                                    Operator = ">=",
                                    ExpectedValue = RunCreateExpectedValue.CreateNumber(0),
                                    Outcome = "PASS",
                                },
                            },
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { 10, 20, 30, 40 },
                                Units = "mV",
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
    public async Task CreateRun_XAxisWithAggregations()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "x_aggs",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "sampling",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 0, 0.5, 1.0, 1.5, 2.0 },
                            Units = "s",
                            Aggregations = new List<RunCreateAggregations>
                            {
                                new RunCreateAggregations
                                {
                                    Type = "max",
                                    Value = RunCreateValue.CreateNumber(2.0),
                                },
                            },
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { 1, 2, 3, 4, 5 },
                                Units = "V",
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
    public async Task CreateRun_ComprehensiveMdm()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "comprehensive_mdm",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "full_sweep",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        XAxis = new RunCreateXAxis
                        {
                            Data = new List<double> { 100, 1000, 10000 },
                            Units = "Hz",
                            Description = "Frequency",
                        },
                        YAxis = new List<RunCreateYAxis>
                        {
                            new RunCreateYAxis
                            {
                                Data = new List<double> { -1.0, 0.0, -3.0 },
                                Units = "dB",
                                Description = "Gain",
                                Validators = new List<RunCreateYAxisValidators>
                                {
                                    new RunCreateYAxisValidators
                                    {
                                        Operator = ">=",
                                        ExpectedValue = RunCreateYAxisExpectedValue.CreateNumber(-6.0),
                                        Outcome = "PASS",
                                    },
                                },
                                Aggregations = new List<RunCreateYAxisAggregations>
                                {
                                    new RunCreateYAxisAggregations
                                    {
                                        Type = "min",
                                        Value = RunCreateYAxisValue.CreateNumber(-3.0),
                                        Outcome = "PASS",
                                        Validators = new List<RunCreateYAxisAggregationsValidators>
                                        {
                                            new RunCreateYAxisAggregationsValidators
                                            {
                                                Operator = ">=",
                                                ExpectedValue = RunCreateYAxisAggregationsExpectedValue.CreateNumber(-6.0),
                                                Outcome = "PASS",
                                            },
                                        },
                                    },
                                },
                            },
                            new RunCreateYAxis
                            {
                                Data = new List<double> { -5.0, -10.0, -45.0 },
                                Units = "deg",
                                Description = "Phase",
                            },
                        },
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        var measurement = fetched.Phases[0].Measurements[0];
        Assert.NotNull(measurement.DataSeries);
        Assert.True(measurement.DataSeries.Count >= 2);
    }
}
