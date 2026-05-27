

/// <summary>
/// 输入接口
/// </summary>
public interface IInput
{
    
}

/// <summary>
/// 输出接口
/// </summary>
public interface IOutput
{
    
}


/// <summary>
/// 计算器接口，定义了一个通用的计算方法，接受输入并返回输出
/// </summary>
/// <typeparam name="TInput"></typeparam>
/// <typeparam name="TOutput"></typeparam>
public interface ICalculator<TInput, TOutput>
    where TInput : IInput
    where TOutput : IOutput
{
    TOutput Calculate(TInput input);
}