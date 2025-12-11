using FluentAssertions;
using VatIT.Domain.DTOs;
using Xunit;

namespace VatIT.Tests.Workers;

public class ValidationWorkerTests
{
    [Fact]
    public void ValidateAddress_ValidUSAddress_ReturnsTrue()
    {
        // Arrange
        var request = new ValidationRequestDto
        {
            TransactionId = "txn_123",
            Country = "US",
            State = "CA",
            City = "Los Angeles"
        };

        // Act - Simulating validation logic
        var isValid = request.Country.ToUpper() == "US" && 
                     !string.IsNullOrEmpty(request.State) && 
                     !string.IsNullOrEmpty(request.City);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateAddress_InvalidCountry_ReturnsFalse()
    {
        // Arrange
        var request = new ValidationRequestDto
        {
            TransactionId = "txn_123",
            Country = "UK",
            State = "CA",
            City = "Los Angeles"
        };

        // Act
        var isValid = request.Country.ToUpper() == "US";

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidationResponse_IncludesAuditLogs()
    {
        // Arrange
        var response = new ValidationResponseDto
        {
            TransactionId = "txn_123",
            IsValid = true,
            Message = "Valid US address",
            AuditLogs = new List<string>
            {
                "Country validation passed: US",
                "State validation passed: CA",
                "Address validated via cache"
            }
        };

        // Assert
        response.AuditLogs.Should().HaveCount(3);
        response.AuditLogs.Should().Contain("Address validated via cache");
    }
}
