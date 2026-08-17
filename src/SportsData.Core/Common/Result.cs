using FluentValidation.Results;

using System.Collections.Generic;

namespace SportsData.Core.Common
{
    public abstract class Result<T>(T value, ResultStatus status)
    {
        public T Value { get; } = value;

        public ResultStatus Status { get; } = status;

        public bool IsSuccess => this is Success<T>;
    }

    public class Failure<T>(T value, ResultStatus status, List<ValidationFailure> errors) :
        Result<T>(value, status)
    {
        public List<ValidationFailure> Errors { get; } = errors;
    }

    public class Success<T>(T value, ResultStatus status = ResultStatus.Success) : Result<T>(value, status);

    public enum ResultStatus
    {
        Accepted,
        BadRequest,
        Created,
        Error,
        Forbid,
        NotFound,
        RateLimited,
        Success,
        Unauthorized,
        Validation,

        // Appended (not alphabetical) deliberately: enum members number by
        // position, and inserting mid-enum would renumber everything after it
        // — a silent remap anywhere a ResultStatus is ever serialized
        // numerically across a rolling deploy.
        Conflict
    }
}
