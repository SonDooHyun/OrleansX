using Microsoft.Extensions.Logging;
using OrleansX.Grains;

namespace Tutorial.StatelessGrain.Grains;

public class CalculatorGrain : StatelessGrainBase, ICalculatorGrain
{
    public CalculatorGrain(ILogger<CalculatorGrain> logger)
        : base(logger)
    {
    }

    public Task<int> AddAsync(int a, int b)
    {
        Logger.LogInformation("Adding {A} + {B}", a, b);
        var result = a + b;
        Logger.LogInformation("Result: {Result}", result);
        return Task.FromResult(result);
    }

    public Task<int> SubtractAsync(int a, int b)
    {
        Logger.LogInformation("Subtracting {A} - {B}", a, b);
        var result = a - b;
        Logger.LogInformation("Result: {Result}", result);
        return Task.FromResult(result);
    }

    public Task<int> MultiplyAsync(int a, int b)
    {
        Logger.LogInformation("Multiplying {A} * {B}", a, b);
        var result = a * b;
        Logger.LogInformation("Result: {Result}", result);
        return Task.FromResult(result);
    }

    public Task<double> DivideAsync(double a, double b)
    {
        if (b == 0)
        {
            Logger.LogError("Division by zero attempted");
            throw new DivideByZeroException("Cannot divide by zero");
        }

        Logger.LogInformation("Dividing {A} / {B}", a, b);
        var result = a / b;
        Logger.LogInformation("Result: {Result}", result);
        return Task.FromResult(result);
    }
}
