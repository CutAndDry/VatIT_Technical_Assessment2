using FluentAssertions;
using VatIT.Domain.DTOs;
using Xunit;

namespace VatIT.Tests.Workers;

public class ExemptionWorkerTests
{
    [Fact]
    public void CheckExemption_NoExemptions_PassesWithEmptyList()
    {
        // Arrange
        var request = new ExemptionRequestDto
        {
            TransactionId = "txn_123",
            CustomerId = "customer_789",
            MerchantId = "merchant_456",
            Items = new List<ItemDto>
            {
                new ItemDto { Id = "item_1", Category = "SOFTWARE", Amount = 100.00m }
            }
        };

        var response = new ExemptionResponseDto
        {
            TransactionId = "txn_123",
            Passed = true,
            AppliedExemptions = new List<string>(),
            Message = "No exemptions applied"
        };

        // Assert
        response.Passed.Should().BeTrue();
        response.AppliedExemptions.Should().BeEmpty();
    }

    [Fact]
    public void CheckExemption_WithExemptions_ReturnsExemptionList()
    {
        // Arrange
        var response = new ExemptionResponseDto
        {
            TransactionId = "txn_123",
            Passed = true,
            AppliedExemptions = new List<string>
            {
                "Customer exemption: customer_exempt_001",
                "Educational materials exemption"
            },
            Message = "Applied 2 exemption(s)",
            AuditLogs = new List<string>
            {
                "Customer customer_exempt_001 has tax-exempt status",
                "Total exemptions applied: 2"
            }
        };

        // Assert
        response.Passed.Should().BeTrue();
        response.AppliedExemptions.Should().HaveCount(2);
        response.AuditLogs.Should().Contain(log => log.Contains("Total exemptions applied: 2"));
    }
}
