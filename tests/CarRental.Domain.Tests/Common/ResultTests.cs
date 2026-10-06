using CarRental.Domain.Common;
using Shouldly;

namespace CarRental.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ExposesValueAndNoErrors()
    {
        var result = Result<int, string>.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_ExposesAllErrors()
    {
        var result = Result<int, string>.Failure(["first problem", "second problem"]);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldBe(["first problem", "second problem"]);
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var result = Result<int, string>.Failure(["a problem"]);

        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_WithNoErrors_Throws()
    {
        // A failure must say why it failed.
        Should.Throw<ArgumentException>(() => Result<int, string>.Failure([]));
    }
}
