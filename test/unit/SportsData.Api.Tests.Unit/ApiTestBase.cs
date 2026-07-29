using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Infrastructure.Data;
using SportsData.Tests.Shared;

namespace SportsData.Api.Tests.Unit
{
    public abstract class ApiTestBase<T> : UnitTestBase<T>
        where T : class
    {
        public AppDataContext DataContext { get; }

        protected ApiTestBase()
        {
            DataContext = new AppDataContext(GetAppDataContextOptions());
            Mocker.Use(typeof(AppDataContext), DataContext);

            // The league membership guard defaults to "the caller is a member"
            // so handler tests exercise their own logic rather than authorization.
            // Authorization tests call DenyLeagueMembership() to flip it.
            // See docs/audit/league-authorization-idor.md.
            AllowLeagueMembership();
        }

        /// <summary>Guard returns true for any (league, user) — the default.</summary>
        protected void AllowLeagueMembership() => SetLeagueMembership(true);

        /// <summary>Guard returns false for any (league, user).</summary>
        protected void DenyLeagueMembership() => SetLeagueMembership(false);

        private void SetLeagueMembership(bool isMember)
        {
            var guard = Mocker.GetMock<ILeagueMembershipGuard>();
            guard.Setup(x => x.IsMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(isMember);
            guard.Setup(x => x.IsMemberOfThreadGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(isMember);
            guard.Setup(x => x.IsMemberOfPostGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(isMember);
        }

        private static DbContextOptions<AppDataContext> GetAppDataContextOptions()
        {
            var dbName = Guid.NewGuid().ToString()[..5];
            return new DbContextOptionsBuilder<AppDataContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }
    }
}
