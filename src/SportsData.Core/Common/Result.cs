using System.Collections.Generic;

namespace SportsData.Core.Common
{
    public abstract class Result<T>(T value)
    {
        public T Value { get; } = value;
    }

    public class Failure<T>(T value, IEnumerable<string> errors) :
        Result<T>(value)
    {
        public IEnumerable<string> Errors { get; } = errors;
    }

    public class Success<T>(T value) : Result<T>(value);
}
