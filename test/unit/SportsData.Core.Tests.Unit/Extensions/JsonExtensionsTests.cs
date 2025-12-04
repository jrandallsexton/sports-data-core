using FluentAssertions;

using System.Text.Json;

using SportsData.Core.Extensions;

using Xunit;

namespace SportsData.Core.Tests.Unit.Extensions
{
    public class JsonExtensionsTests
    {
        private class SampleModel
        {
            public string Id { get; set; }
            public int Count { get; set; }
            public string NullValue { get; set; }
        }

        [Fact]
        public void ToJson_And_FromJson_Should_Serialize_And_Deserialize_Correctly()
        {
            // arrange
            var model = new SampleModel
            {
                Id = "abc123",
                Count = 42,
                NullValue = null
            };

            // act
            string json = model.ToJson();
            var deserialized = json.FromJson<SampleModel>();

            // assert
            deserialized.Id.Should().Be("abc123");
            deserialized.Count.Should().Be(42);
            deserialized.NullValue.Should().BeNull();

            // Also ensure camelCasing is applied
            json.Should().Contain("\"id\"");
            json.Should().Contain("\"count\"");
            json.Should().NotContain("nullValue"); // Should be omitted
        }
    }
}