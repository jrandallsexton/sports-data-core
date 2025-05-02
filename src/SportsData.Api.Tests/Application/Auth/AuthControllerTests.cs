using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SportsData.Api.Application.Auth;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using System.Security.Claims;
using Xunit;

namespace SportsData.Api.Tests.Application.Auth
{
    public class AuthControllerTests
    {
        private readonly Mock<AppDataContext> _mockDbContext;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockDbContext = new Mock<AppDataContext>();
            _mockLogger = new Mock<ILogger<AuthController>>();
            _controller = new AuthController(_mockDbContext.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SetToken_ValidToken_SetsCookie()
        {
            // Arrange
            var request = new SetTokenRequest { Token = "valid-token" };
            var mockResponse = new Mock<HttpResponse>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.SetToken(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.Equal("Token set successfully", (okResult.Value as dynamic).message);
            Assert.True(_controller.Response.Headers.ContainsKey("Set-Cookie"));
        }

        [Fact]
        public async Task SetToken_EmptyToken_ReturnsBadRequest()
        {
            // Arrange
            var request = new SetTokenRequest { Token = "" };

            // Act
            var result = await _controller.SetToken(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            var badRequestResult = result as BadRequestObjectResult;
            Assert.Equal("Token is required", badRequestResult.Value);
        }

        [Fact]
        public async Task ClearToken_ClearsCookie()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.ClearToken();

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.Equal("Token cleared successfully", (okResult.Value as dynamic).message);
            Assert.True(_controller.Response.Headers.ContainsKey("Set-Cookie"));
        }

        [Fact]
        public async Task SyncUser_ValidUser_CreatesNewUser()
        {
            // Arrange
            var userId = "test-user-id";
            var email = "test@example.com";
            var claims = new List<Claim>
            {
                new Claim("user_id", userId),
                new Claim(ClaimTypes.Email, email)
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            var mockDbSet = new Mock<DbSet<User>>();
            _mockDbContext.Setup(x => x.Users).Returns(mockDbSet.Object);

            // Act
            var result = await _controller.SyncUser();

            // Assert
            Assert.IsType<OkObjectResult>(result);
            mockDbSet.Verify(x => x.Add(It.Is<User>(u => 
                u.FirebaseUid == userId && 
                u.Email == email)), Times.Once);
            _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task SyncUser_ExistingUser_UpdatesLastLogin()
        {
            // Arrange
            var userId = "test-user-id";
            var email = "test@example.com";
            var existingUser = new User
            {
                FirebaseUid = userId,
                Email = email,
                LastLoginUtc = DateTime.UtcNow.AddDays(-1)
            };

            var claims = new List<Claim>
            {
                new Claim("user_id", userId),
                new Claim(ClaimTypes.Email, email)
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            var mockDbSet = new Mock<DbSet<User>>();
            mockDbSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>(), default))
                .ReturnsAsync(existingUser);
            _mockDbContext.Setup(x => x.Users).Returns(mockDbSet.Object);

            // Act
            var result = await _controller.SyncUser();

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }
    }
} 