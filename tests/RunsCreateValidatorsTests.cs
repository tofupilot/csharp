using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TofuPilot;
using TofuPilot.Models.Requests;
using Xunit;

namespace TofuPilot.Tests;

[Collection("API")]
public class RunsCreateValidatorsTests
{
    private readonly TofuPilot _client;
    private readonly string _procedureId;

    public RunsCreateValidatorsTests(TestFixture fixture)
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
            SerialNumber = $"SN-V-{uid}",
            ProcedureId = _procedureId,
            PartNumber = $"PART-V-{uid}",
            StartedAt = now.AddMinutes(-5),
            EndedAt = now,
            Outcome = RunCreateOutcome.Pass,
        };
    }

    private RunCreateRequest WithMeasurement(string uid, string name, double measuredValue, RunCreateMeasurementsOutcome outcome, List<RunCreateMeasurementsValidators> validators)
    {
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "validation_phase",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = name,
                        Outcome = outcome,
                        MeasuredValue = measuredValue,
                        Validators = validators,
                    },
                },
            },
        };
        return req;
    }

    [Theory]
    [InlineData(">=", 10.0, 5.0)]
    [InlineData("<=", 10.0, 15.0)]
    [InlineData(">", 10.0, 5.0)]
    [InlineData("<", 10.0, 15.0)]
    [InlineData("==", 10.0, 10.0)]
    [InlineData("!=", 10.0, 5.0)]
    public async Task CreateRun_ValidatorOperator(string op, double measured, double expected)
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = op,
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(expected),
                Outcome = "PASS",
            },
        };
        var req = WithMeasurement(uid, $"test_{op}", measured, RunCreateMeasurementsOutcome.Pass, validators);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        var measurement = fetched.Phases[0].Measurements[0];
        Assert.NotNull(measurement.Validators);
        Assert.Single(measurement.Validators);
        Assert.Equal(op, measurement.Validators[0].Operator);
    }

    [Fact]
    public async Task CreateRun_ValidatorWithStringExpectedValue()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = "==",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateStr("PASS"),
                Outcome = "PASS",
            },
        };
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "string_check",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "status",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = "PASS",
                        Validators = validators,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        Assert.NotNull(fetched.Phases[0].Measurements[0].Validators);
    }

    [Fact]
    public async Task CreateRun_ValidatorWithBooleanExpectedValue()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = "==",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateBoolean(true),
                Outcome = "PASS",
            },
        };
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "bool_check",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "is_calibrated",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = true,
                        Validators = validators,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        Assert.False(string.IsNullOrEmpty(created.Id));
    }

    [Fact]
    public async Task CreateRun_MultipleValidatorsRangeCheck()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = ">=",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(0),
                Outcome = "PASS",
            },
            new RunCreateMeasurementsValidators
            {
                Operator = "<=",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(100),
                Outcome = "PASS",
            },
        };
        var req = WithMeasurement(uid, "range_value", 50.0, RunCreateMeasurementsOutcome.Pass, validators);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases);
        var measurement = fetched.Phases[0].Measurements[0];
        Assert.NotNull(measurement.Validators);
        Assert.Equal(2, measurement.Validators.Count);
    }

    [Fact]
    public async Task CreateRun_ValidatorWithIsDecisiveFalse()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = ">=",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(90),
                Outcome = "FAIL",
                IsDecisive = false,
            },
        };
        var req = WithMeasurement(uid, "marginal_check", 85.0, RunCreateMeasurementsOutcome.Pass, validators);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var validator = fetched.Phases![0].Measurements[0].Validators![0];
        Assert.False(validator.IsDecisive);
    }

    [Fact]
    public async Task CreateRun_ValidatorWithIsDecisiveTrue()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = ">=",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(0),
                Outcome = "PASS",
                IsDecisive = true,
            },
        };
        var req = WithMeasurement(uid, "decisive_check", 50.0, RunCreateMeasurementsOutcome.Pass, validators);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var validator = fetched.Phases![0].Measurements[0].Validators![0];
        Assert.True(validator.IsDecisive);
    }

    [Fact]
    public async Task CreateRun_ExpressionOnlyValidator()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Expression = "value > threshold && value < max_threshold",
                Outcome = "PASS",
            },
        };
        var req = WithMeasurement(uid, "expr_check", 50.0, RunCreateMeasurementsOutcome.Pass, validators);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var validator = fetched.Phases![0].Measurements[0].Validators![0];
        Assert.True(validator.IsExpressionOnly);
        Assert.Contains("threshold", validator.Expression);
    }

    [Fact]
    public async Task CreateRun_ValidatorWithCustomExpression()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = ">=",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(0),
                Expression = "voltage within safe range",
                Outcome = "PASS",
            },
        };
        var req = WithMeasurement(uid, "custom_expr", 3.3, RunCreateMeasurementsOutcome.Pass, validators);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var validator = fetched.Phases![0].Measurements[0].Validators![0];
        Assert.True(validator.HasCustomExpression);
        Assert.Equal("voltage within safe range", validator.Expression);
    }

    [Fact]
    public async Task CreateRun_ValidatorFailOutcome()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = "<=",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(5),
                Outcome = "FAIL",
            },
        };
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Outcome = RunCreateOutcome.Fail;
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "fail_phase",
                Outcome = RunCreatePhasesOutcome.Fail,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "over_limit",
                        Outcome = RunCreateMeasurementsOutcome.Fail,
                        MeasuredValue = 10.0,
                        Validators = validators,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        var validator = fetched.Phases![0].Measurements[0].Validators![0];
        Assert.Equal(RunGetValidatorsOutcome.Fail, validator.Outcome);
    }

    [Fact]
    public async Task CreateRun_ValidatorInOperatorWithStringList()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = "in",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateArrayOfStr(new List<string> { "A", "B", "C" }),
                Outcome = "PASS",
            },
        };
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "in_check",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "grade",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = "A",
                        Validators = validators,
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases![0].Measurements[0].Validators);
        Assert.Equal("in", fetched.Phases[0].Measurements[0].Validators![0].Operator);
    }

    [Fact]
    public async Task CreateRun_ValidatorRangeOperator()
    {
        var uid = Uid();
        var validators = new List<RunCreateMeasurementsValidators>
        {
            new RunCreateMeasurementsValidators
            {
                Operator = "range",
                ExpectedValue = RunCreateMeasurementsExpectedValue.CreateArrayOfNumber(new List<double> { 10.0, 50.0 }),
                Outcome = "PASS",
            },
        };
        var req = WithMeasurement(uid, "range_check", 25.0, RunCreateMeasurementsOutcome.Pass, validators);
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.NotNull(fetched.Phases![0].Measurements[0].Validators);
        Assert.NotEmpty(fetched.Phases[0].Measurements[0].Validators!);
    }

    [Fact]
    public async Task CreateRun_MultipleMeasurementsWithValidators()
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var req = BaseRequest(uid);
        req.Phases = new List<RunCreatePhases>
        {
            new RunCreatePhases
            {
                Name = "multi_meas",
                Outcome = RunCreatePhasesOutcome.Pass,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-3),
                Measurements = new List<RunCreateMeasurements>
                {
                    new RunCreateMeasurements
                    {
                        Name = "voltage",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = 3.3,
                        Validators = new List<RunCreateMeasurementsValidators>
                        {
                            new RunCreateMeasurementsValidators { Operator = ">=", ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(3.0), Outcome = "PASS" },
                            new RunCreateMeasurementsValidators { Operator = "<=", ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(3.6), Outcome = "PASS" },
                        },
                    },
                    new RunCreateMeasurements
                    {
                        Name = "current",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = 0.5,
                        Validators = new List<RunCreateMeasurementsValidators>
                        {
                            new RunCreateMeasurementsValidators { Operator = "<", ExpectedValue = RunCreateMeasurementsExpectedValue.CreateNumber(1.0), Outcome = "PASS" },
                        },
                    },
                },
            },
        };
        var created = await _client.Runs.CreateAsync(req);
        var fetched = await _client.Runs.GetAsync(created.Id);

        Assert.Equal(2, fetched.Phases![0].Measurements.Count);
        Assert.NotNull(fetched.Phases[0].Measurements[0].Validators);
        Assert.NotEmpty(fetched.Phases[0].Measurements[0].Validators!);
        Assert.NotNull(fetched.Phases[0].Measurements[1].Validators);
        Assert.NotEmpty(fetched.Phases[0].Measurements[1].Validators!);
    }
}
