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
                        MeasuredValue = measuredValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
                ExpectedValue = expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                ExpectedValue = "PASS",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                ExpectedValue = "true",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                        MeasuredValue = "true",
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
                ExpectedValue = "0",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
            },
            new RunCreateMeasurementsValidators
            {
                Operator = "<=",
                ExpectedValue = "100",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                ExpectedValue = "90",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Fail,
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
                ExpectedValue = "0",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                ExpectedValue = "0",
                Expression = "voltage within safe range",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                ExpectedValue = "5",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Fail,
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
                        MeasuredValue = "10",
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
                ExpectedValue = "[\"A\",\"B\",\"C\"]",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                ExpectedValue = "[10,50]",
                Outcome = RunCreateMeasurementsValidatorsOutcome.Pass,
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
                        MeasuredValue = "3.3",
                        Validators = new List<RunCreateMeasurementsValidators>
                        {
                            new RunCreateMeasurementsValidators { Operator = ">=", ExpectedValue = "3", Outcome = RunCreateMeasurementsValidatorsOutcome.Pass },
                            new RunCreateMeasurementsValidators { Operator = "<=", ExpectedValue = "3.6", Outcome = RunCreateMeasurementsValidatorsOutcome.Pass },
                        },
                    },
                    new RunCreateMeasurements
                    {
                        Name = "current",
                        Outcome = RunCreateMeasurementsOutcome.Pass,
                        MeasuredValue = "0.5",
                        Validators = new List<RunCreateMeasurementsValidators>
                        {
                            new RunCreateMeasurementsValidators { Operator = "<", ExpectedValue = "1", Outcome = RunCreateMeasurementsValidatorsOutcome.Pass },
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
