using FluentAssertions;

using FluentValidation;

using SportsData.Api.Application.User.Commands.UpdateUsername;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpdateUsername;

public class UpdateUsernameCommandHandlerTests : ApiTestBase<UpdateUsernameCommandHandler>
{
    public UpdateUsernameCommandHandlerTests()
    {
        Mocker.Use<IValidator<UpdateUsernameCommand>>(new UpdateUsernameCommandValidator());
    }

    private async Task<Guid> SeedUserAsync(string username, string email = "u@x.com")
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = id,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = email,
            SignInProvider = "password",
            DisplayName = "Display",
            Username = username
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Execute_SetsUsername_WhenValidAndAvailable()
    {
        var userId = await SeedUserAsync("oldhandle");
        var handler = Mocker.CreateInstance<UpdateUsernameCommandHandler>();

        var result = await handler.ExecuteAsync(userId, new UpdateUsernameCommand { Username = "NewHandle" });

        result.IsSuccess.Should().BeTrue();
        var user = await DataContext.Users.FindAsync(userId);
        user!.Username.Should().Be("newhandle"); // stored lowercased
    }

    [Fact]
    public async Task Execute_Rejects_WhenTaken()
    {
        await SeedUserAsync("taken", "a@x.com");
        var userId = await SeedUserAsync("mine", "b@x.com");
        var handler = Mocker.CreateInstance<UpdateUsernameCommandHandler>();

        var result = await handler.ExecuteAsync(userId, new UpdateUsernameCommand { Username = "Taken" });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task Execute_NoOp_WhenSameHandleDifferentCase()
    {
        var userId = await SeedUserAsync("jrandall");
        var handler = Mocker.CreateInstance<UpdateUsernameCommandHandler>();

        var result = await handler.ExecuteAsync(userId, new UpdateUsernameCommand { Username = "JRandall" });

        result.IsSuccess.Should().BeTrue();
        var user = await DataContext.Users.FindAsync(userId);
        user!.Username.Should().Be("jrandall");
    }

    [Fact]
    public async Task Execute_Rejects_WhenInvalid()
    {
        var userId = await SeedUserAsync("validhandle");
        var handler = Mocker.CreateInstance<UpdateUsernameCommandHandler>();

        var result = await handler.ExecuteAsync(userId, new UpdateUsernameCommand { Username = "no spaces" });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task Execute_NotFound_WhenUserMissing()
    {
        var handler = Mocker.CreateInstance<UpdateUsernameCommandHandler>();

        var result = await handler.ExecuteAsync(Guid.NewGuid(), new UpdateUsernameCommand { Username = "ghosthandle" });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
