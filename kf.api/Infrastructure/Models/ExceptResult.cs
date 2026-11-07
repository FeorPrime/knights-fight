namespace KnightFight.Api.Infrastructure.Models;

public record ExceptResult<TResult>
{
    public static ExceptResult<TResult> Success(TResult value) =>
        new()
        {
            ResultState = ResultType.Success,
            ResultValue = value
        };

    public static ExceptResult<TResult> Failure(string problem) =>
        new()
        {
            ResultState = ResultType.Error,
            Problem = problem
        };

 public ResultType ResultState { get; init; }
    public string Problem { get; init; } = string.Empty;
    
    public bool IsSuccess => ResultState == ResultType.Success;
    public bool IsError => ResultState == ResultType.Error;
    
    public TResult? ResultValue { get; init; } 
}

public enum ResultType
{
    Success,
    Error
}