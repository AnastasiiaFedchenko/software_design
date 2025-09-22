using Moq;
using System.Data;
using Xunit;
using Allure.Xunit.Attributes;
using Allure.Xunit;
using Domain;
using UserValidation;
using ConnectionToDB;
using Allure.Net.Commons;

namespace UserValidation.Tests
{
    [AllureSuite("UserValidation Layer")]
    [AllureFeature("Product Management")]
    [AllureSubSuite("Product Entity")]
    public class UserRepoTests
    {
        // абсолютно тупейшие тесты в которых я проверяла то что сама же намокала,
        // но они мне были нужны чтобы разобраться как мокать репозитории 
        // а с этим репозиторием это удобно тк он самый короткий
        [Fact]
        [AllureStory("UserValidation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("UserValidation Team")]
        public void CheckPasswordAndGetUserType_ValidAdminCredentials_ReturnsAdministrator()
        {
            // Arrange
            var mockConnectionFactory = new Mock<IDbConnectionFactory>();
            var mockConnection = new Mock<IDbConnection>();
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var mockParameters = new Mock<IDataParameterCollection>();

            mockConnectionFactory
                .Setup(f => f.CreateOpenConnection())
                .Returns(mockConnection.Object);

            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.CreateParameter()).Returns(new Mock<IDbDataParameter>().Object);
            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.SetupGet(c => c.Parameters).Returns(mockParameters.Object);

            mockReader.SetupSequence(r => r.Read())
                .Returns(true)  
                .Returns(false); 
            mockReader.Setup(r => r.GetString(0)).Returns("администратор");

            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            var repo = new UserRepo(mockConnectionFactory.Object);

            // Act
            var result = repo.CheckPasswordAndGetUserType(1, "admin123");

            // Assert
            Assert.Equal(UserType.Administrator, result);
        }

        [Fact]
        [AllureStory("UserValidation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("UserValidation Team")]
        public void CheckPasswordAndGetUserType_InvalidCredentials_ReturnsNull()
        {
            // Arrange
            var mockConnectionFactory = new Mock<IDbConnectionFactory>();
            var mockConnection = new Mock<IDbConnection>();
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var mockParameters = new Mock<IDataParameterCollection>();

            mockConnectionFactory
                .Setup(f => f.CreateOpenConnection()) 
                .Returns(mockConnection.Object);

            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.CreateParameter()).Returns(new Mock<IDbDataParameter>().Object);
            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.SetupGet(c => c.Parameters).Returns(mockParameters.Object);

            mockReader.SetupSequence(r => r.Read())
                .Returns(false);

            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            var repo = new UserRepo(mockConnectionFactory.Object);

            // Act
            var result = repo.CheckPasswordAndGetUserType(999, "wrongpassword");

            // Assert
            Assert.Null(result);
        }
    }
}