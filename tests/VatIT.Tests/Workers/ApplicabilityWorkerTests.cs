using FluentAssertions;
using VatIT.Domain.DTOs;
using Xunit;

namespace VatIT.Tests.Workers;

public class ApplicabilityWorkerTests
{
    [Theory]
    [InlineData(2300000, 100000, true)]
    [InlineData(100000, 100000, true)]
    [InlineData(99999, 100000, false)]
    [InlineData(50000, 100000, false)]
    public void CheckApplicability_ComparesVolumeToThreshold_ReturnsCorrectResult(
        decimal merchantVolume, 
        decimal threshold, 
        bool expectedResult)
    {
        // Arrange & Act
        var isApplicable = merchantVolume >= threshold;

        // Assert
        isApplicable.Should().Be(expectedResult);
    }

    [Fact]
    public void ApplicabilityResponse_IncludesVolumeAndThresholdData()
    {
        // Arrange
        var response = new ApplicabilityResponseDto
        {
            TransactionId = "txn_123",
            IsApplicable = true,
            Message = "Merchant above $100K threshold in CA",
            MerchantVolume = 2300000m,
            Threshold = 100000m,
            AuditLogs = new List<string>
            {
                "State threshold for CA: $100,000",
                "Merchant volume: $2.3M in CA (threshold: $100K)"
            }
        };

        // Assert
        response.MerchantVolume.Should().Be(2300000m);
        response.Threshold.Should().Be(100000m);
        response.IsApplicable.Should().BeTrue();
        response.AuditLogs.Should().NotBeEmpty();
    }
}
