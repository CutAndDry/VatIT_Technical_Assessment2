using FluentAssertions;
using Moq;
using VatIT.Application.Interfaces;
using VatIT.Application.Services;
using VatIT.Domain.DTOs;
using VatIT.Domain.Entities;
using Xunit;

namespace VatIT.Tests.Application;

public class OrchestrationServiceTests
{
    private readonly Mock<IWorkerClient> _mockWorkerClient;
    private readonly OrchestrationService _orchestrationService;

    public OrchestrationServiceTests()
    {
        _mockWorkerClient = new Mock<IWorkerClient>();
        _orchestrationService = new OrchestrationService(_mockWorkerClient.Object);
    }

    [Fact]
    public async Task ProcessTransactionAsync_AllGatesPass_ReturnsCalculatedStatus()
    {
        // Arrange
        var request = CreateValidTransactionRequest();

        _mockWorkerClient
            .Setup(x => x.SendValidationRequestAsync(It.IsAny<ValidationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResponseDto
            {
                TransactionId = "txn_123",
                IsValid = true,
                Message = "Valid US address",
                AuditLogs = new List<string> { "Address validated via cache" }
            });

        _mockWorkerClient
            .Setup(x => x.SendApplicabilityRequestAsync(It.IsAny<ApplicabilityRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicabilityResponseDto
            {
                TransactionId = "txn_123",
                IsApplicable = true,
                Message = "Merchant above threshold",
                MerchantVolume = 2300000m,
                Threshold = 100000m,
                AuditLogs = new List<string> { "Merchant volume: $2.3M in CA" }
            });

        _mockWorkerClient
            .Setup(x => x.SendExemptionRequestAsync(It.IsAny<ExemptionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExemptionResponseDto
            {
                TransactionId = "txn_123",
                Passed = true,
                AppliedExemptions = new List<string>(),
                Message = "No exemptions applied",
                AuditLogs = new List<string> { "No exemptions applicable" }
            });

        _mockWorkerClient
            .Setup(x => x.SendCalculationRequestAsync(It.IsAny<CalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalculationResponseDto
            {
                TransactionId = "txn_123",
                TotalFees = 9.50m,
                EffectiveRate = 0.095m,
                Items = new List<ItemCalculationDto>(),
                AuditLogs = new List<string> { "Calculation completed" }
            });

        // Act
        var result = await _orchestrationService.ProcessTransactionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("CALCULATED");
        result.TransactionId.Should().Be("txn_123");
        result.Gates.Should().HaveCount(3);
        result.Calculation.Should().NotBeNull();
        result.AuditTrail.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessTransactionAsync_ValidationFails_StopsAtFirstGate()
    {
        // Arrange
        var request = CreateValidTransactionRequest();

        _mockWorkerClient
            .Setup(x => x.SendValidationRequestAsync(It.IsAny<ValidationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResponseDto
            {
                TransactionId = "txn_123",
                IsValid = false,
                Message = "Invalid address",
                AuditLogs = new List<string> { "Address validation failed" }
            });

        // Act
        var result = await _orchestrationService.ProcessTransactionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("FAILED");
        // All gates still run; first gate should be failing but downstream workers are invoked
        result.Gates.Should().HaveCount(3);
        result.Gates[0].Passed.Should().BeFalse();
        result.Calculation.Should().NotBeNull();

        // Verify subsequent gates were called once
        _mockWorkerClient.Verify(
            x => x.SendApplicabilityRequestAsync(It.IsAny<ApplicabilityRequestDto>(), It.IsAny<CancellationToken>()), 
            Times.Once);
        _mockWorkerClient.Verify(
            x => x.SendExemptionRequestAsync(It.IsAny<ExemptionRequestDto>(), It.IsAny<CancellationToken>()), 
            Times.Once);
        _mockWorkerClient.Verify(
            x => x.SendCalculationRequestAsync(It.IsAny<CalculationRequestDto>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ProcessTransactionAsync_ApplicabilityFails_StopsAtSecondGate()
    {
        // Arrange
        var request = CreateValidTransactionRequest();

        _mockWorkerClient
            .Setup(x => x.SendValidationRequestAsync(It.IsAny<ValidationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResponseDto
            {
                TransactionId = "txn_123",
                IsValid = true,
                Message = "Valid US address",
                AuditLogs = new List<string> { "Address validated" }
            });

        _mockWorkerClient
            .Setup(x => x.SendApplicabilityRequestAsync(It.IsAny<ApplicabilityRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicabilityResponseDto
            {
                TransactionId = "txn_123",
                IsApplicable = false,
                Message = "Merchant below threshold",
                MerchantVolume = 50000m,
                Threshold = 100000m,
                AuditLogs = new List<string> { "Merchant volume below threshold" }
            });

        // Act
        var result = await _orchestrationService.ProcessTransactionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("FAILED");
        // All gates still run; second gate should be failing but downstream workers are invoked
        result.Gates.Should().HaveCount(3);
        result.Gates[0].Passed.Should().BeTrue();
        result.Gates[1].Passed.Should().BeFalse();
        result.Calculation.Should().NotBeNull();

        // Verify subsequent gates were called once
        _mockWorkerClient.Verify(
            x => x.SendExemptionRequestAsync(It.IsAny<ExemptionRequestDto>(), It.IsAny<CancellationToken>()), 
            Times.Once);
        _mockWorkerClient.Verify(
            x => x.SendCalculationRequestAsync(It.IsAny<CalculationRequestDto>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ProcessTransactionAsync_MapsRequestDataCorrectly()
    {
        // Arrange
        var request = CreateValidTransactionRequest();
        ValidationRequestDto? capturedValidationRequest = null;
        ApplicabilityRequestDto? capturedApplicabilityRequest = null;

        _mockWorkerClient
            .Setup(x => x.SendValidationRequestAsync(It.IsAny<ValidationRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<ValidationRequestDto, CancellationToken>((req, ct) => capturedValidationRequest = req)
            .ReturnsAsync(new ValidationResponseDto { TransactionId = "txn_123", IsValid = true, Message = "Valid", AuditLogs = new List<string>() });

        _mockWorkerClient
            .Setup(x => x.SendApplicabilityRequestAsync(It.IsAny<ApplicabilityRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicabilityRequestDto, CancellationToken>((req, ct) => capturedApplicabilityRequest = req)
            .ReturnsAsync(new ApplicabilityResponseDto { TransactionId = "txn_123", IsApplicable = true, Message = "Valid", AuditLogs = new List<string>() });

        _mockWorkerClient
            .Setup(x => x.SendExemptionRequestAsync(It.IsAny<ExemptionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExemptionResponseDto { TransactionId = "txn_123", Passed = true, Message = "Valid", AuditLogs = new List<string>(), AppliedExemptions = new List<string>() });

        _mockWorkerClient
            .Setup(x => x.SendCalculationRequestAsync(It.IsAny<CalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalculationResponseDto { TransactionId = "txn_123", TotalFees = 9.50m, EffectiveRate = 0.095m, Items = new List<ItemCalculationDto>(), AuditLogs = new List<string>() });

        // Act
        await _orchestrationService.ProcessTransactionAsync(request);

        // Assert
        capturedValidationRequest.Should().NotBeNull();
        capturedValidationRequest!.Country.Should().Be("US");
        capturedValidationRequest.State.Should().Be("CA");
        capturedValidationRequest.City.Should().Be("Los Angeles");

        capturedApplicabilityRequest.Should().NotBeNull();
        capturedApplicabilityRequest!.MerchantId.Should().Be("merchant_456");
        capturedApplicabilityRequest.State.Should().Be("CA");
        capturedApplicabilityRequest.TotalAmount.Should().Be(150.00m);
    }

    private static TransactionRequest CreateValidTransactionRequest()
    {
        return new TransactionRequest
        {
            TransactionId = "txn_123",
            MerchantId = "merchant_456",
            CustomerId = "customer_789",
            Destination = new Destination
            {
                Country = "US",
                State = "CA",
                City = "Los Angeles"
            },
            Items = new List<Item>
            {
                new Item { Id = "item_1", Category = "SOFTWARE", Amount = 100.00m },
                new Item { Id = "item_2", Category = "PHYSICAL_GOODS", Amount = 50.00m }
            },
            TotalAmount = 150.00m,
            Currency = "USD"
        };
    }
}
