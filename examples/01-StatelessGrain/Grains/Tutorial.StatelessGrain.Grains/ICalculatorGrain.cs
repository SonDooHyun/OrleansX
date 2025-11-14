using Orleans;

namespace Tutorial.StatelessGrain.Grains;

public interface ICalculatorGrain : IGrainWithStringKey
{
    Task<int> AddAsync(int a, int b);
    Task<int> SubtractAsync(int a, int b);
    Task<int> MultiplyAsync(int a, int b);
    Task<double> DivideAsync(double a, double b);
}
