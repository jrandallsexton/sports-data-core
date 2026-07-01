using FluentAssertions;

using FluentValidation;

using SportsData.Api.Application.User.Commands.UpdateDisplayName;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpdateDisplayName;

public class UpdateDisplayNameCommandHandlerTests : ApiTestBase<UpdateDisplayNameCommandHandler>
{
    public UpdateDisplayNameCommandHandlerTests()
    {
        Mocker.Use<IValidator<UpdateDisplayNameCommand>>(new UpdateDisplayNameCommandValidator());
    }

    private async Task<Guid> SeedUserAsync()
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = id,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "u@x.com",
            SignInProvider = "password",
            DisplayName = "Old Name",
            Username = "handle"
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Execute_UpdatesDisplayName_AndTrims()
    {
        var userId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<UpdateDisplayNameCommandHandler>();

        var result = await handler.ExecuteAsync(userId, new UpdateDisplayNameCommand { DisplayName = "  YouWillAllLose  " });

        result.IsSuccess.Should().BeTrue();
        var user = await DataContext.Users.FindAsync(userId);
        user!.DisplayName.Should().Be("YouWillAllLose");
    }

    [Fact]
    public async Task Execute_Rejects_WhenBlank()
    {
        var userId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<UpdateDisplayNameCommandHandler>();

        var result = await handler.ExecuteAsync(userId, new UpdateDisplayNameCommand { DisplayName = "   " });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task Execute_NotFound_WhenUserMissing()
    {
        var handler = Mocker.CreateInstance<UpdateDisplayNameCommandHandler>();

        var result = await handler.ExecuteAsync(Guid.NewGuid(), new UpdateDisplayNameCommand { DisplayName = "Ghost" });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
