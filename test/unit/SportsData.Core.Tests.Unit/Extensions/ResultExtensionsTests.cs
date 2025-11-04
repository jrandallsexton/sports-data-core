using FluentAssertions;

using FluentValidation.Results;

using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Extensions;

using Xunit;

namespace SportsData.Core.Tests.Unit.Extensions
{
    public class ResultExtensionsTests
    {
        [Fact]
        public void Returns_Ok_On_Success()
        {
            var dto = new SampleDto { Value = "Success!" };
            var result = new Success<SampleDto>(dto);

            var actionResult = result.ToActionResult();

            actionResult.Result.Should().BeOfType<OkObjectResult>()
                .Which.Value.Should().Be(dto);
        }

        [Fact]
        public void Returns_BadRequest_On_ValidationFailure()
        {
            var errors = new List<ValidationFailure> { new("field", "message") };
            var result = new Failure<SampleDto>(
                value: default!,
                status: ResultStatus.Validation,
                errors: errors);

            var actionResult = result.ToActionResult();

            actionResult.Result.Should().BeOfType<BadRequestObjectResult>()
                .Which.Value.Should().BeEquivalentTo(new { Errors = errors }, opts => opts.ComparingByMembers<object>());
        }

        [Fact]
        public void Returns_NotFound_On_NotFoundFailure()
        {
            var errors = new List<ValidationFailure> { new("field", "not found") };
            var result = new Failure<SampleDto>(
                value: default!,
                status: ResultStatus.NotFound,
                errors: errors);

            var actionResult = result.ToActionResult();

            actionResult.Result.Should().BeOfType<NotFoundObjectResult>()
                .Which.Value.Should().BeEquivalentTo(new { Errors = errors }, opts => opts.ComparingByMembers<object>());
        }

        [Fact]
        public void Returns_Unauthorized_On_UnauthorizedFailure()
        {
            var errors = new List<ValidationFailure>();
            var result = new Failure<SampleDto>(
                value: default!,
                status: ResultStatus.Unauthorized,
                errors: errors);

            var actionResult = result.ToActionResult();

            actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public void Returns_Forbid_On_ForbidFailure()
        {
            var result = new Failure<SampleDto>(
                value: default!,
                status: ResultStatus.Forbid,
                errors: new());

            var actionResult = result.ToActionResult();

            actionResult.Result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public void Returns_InternalServerError_On_UnknownStatus()
        {
            var errors = new List<ValidationFailure> { new("field", "error") };
            var result = new Failure<SampleDto>(
                value: default!,
                status: ResultStatus.Accepted, // Not handled specifically
                errors: errors);

            var actionResult = result.ToActionResult();

            actionResult.Result.Should().BeOfType<ObjectResult>()
                .Which.StatusCode.Should().Be(500);
        }
    }

    public class SampleDto
    {
        public string Value { get; set; } = string.Empty;
    }
}
