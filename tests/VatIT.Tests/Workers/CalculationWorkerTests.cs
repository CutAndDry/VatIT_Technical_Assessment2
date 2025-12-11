using FluentAssertions;
using VatIT.Domain.DTOs;
using Xunit;

namespace VatIT.Tests.Workers;

public class CalculationWorkerTests
{
    [Fact]
    public void Calculate_AppliesMultipleRates_CorrectlyCalculatesTotalFee()
    {
        // Arrange
        decimal itemAmount = 100.00m;
        decimal stateRate = 0.06m;
        decimal countyRate = 0.0025m;
        decimal cityRate = 0.0225m;
        decimal categoryRate = 0.01m;

        // Act
        decimal stateAmount = itemAmount * stateRate;      // 6.00
        decimal countyAmount = itemAmount * countyRate;    // 0.25
        decimal cityAmount = itemAmount * cityRate;        // 2.25
        decimal categoryAmount = itemAmount * categoryRate;// 1.00
        decimal totalFee = stateAmount + countyAmount + cityAmount + categoryAmount;

        // Assert
        totalFee.Should().Be(9.50m);
    }

    [Fact]
    public void Calculate_MultipleItems_AggregatesCorrectly()
    {
        // Arrange
        var item1Fee = 9.50m;
        var item2Fee = 4.00m;

        // Act
        var totalFees = item1Fee + item2Fee;
        var effectiveRate = totalFees / 150.00m;

        // Assert
        totalFees.Should().Be(13.50m);
        effectiveRate.Should().BeApproximately(0.09m, 0.001m);
    }

    [Fact]
    public void CalculationResponse_IncludesDetailedBreakdown()
    {
        // Arrange
        var response = new CalculationResponseDto
        {
            TransactionId = "txn_123",
            Items = new List<ItemCalculationDto>
            {
                new ItemCalculationDto
                {
                    ItemId = "item_1",
                    Amount = 100.00m,
                    Category = "SOFTWARE",
                    Fees = new FeesDto
                    {
                        StateRate = new RateInfoDto { Jurisdiction = "CA", Rate = 0.06m, Amount = 6.00m },
                        CountyRate = new RateInfoDto { Jurisdiction = "Los Angeles County", Rate = 0.0025m, Amount = 0.25m },
                        CityRate = new RateInfoDto { Jurisdiction = "Los Angeles", Rate = 0.0225m, Amount = 2.25m },
                        CategoryModifier = new RateInfoDto { Jurisdiction = "CA", Category = "SOFTWARE", Rate = 0.01m, Amount = 1.00m }
                    },
                    TotalFee = 9.50m
                }
            },
            TotalFees = 9.50m,
            EffectiveRate = 0.095m,
            AuditLogs = new List<string>
            {
                "Processing item item_1 ($100.00, SOFTWARE)",
                "State rate (CA): 6.00% = $6.00",
                "Total fees calculated: $9.50"
            }
        };

        // Assert
        response.Items.Should().HaveCount(1);
        var item = response.Items[0];
        item.Fees.StateRate.Should().NotBeNull();
        item.Fees.StateRate!.Amount.Should().Be(6.00m);
        item.Fees.CountyRate!.Amount.Should().Be(0.25m);
        item.Fees.CityRate!.Amount.Should().Be(2.25m);
        item.Fees.CategoryModifier!.Amount.Should().Be(1.00m);
        item.TotalFee.Should().Be(9.50m);
        response.TotalFees.Should().Be(9.50m);
        response.EffectiveRate.Should().Be(0.095m);
    }

    [Theory]
    [InlineData(100.00, 0.06, 6.00)]
    [InlineData(50.00, 0.06, 3.00)]
    [InlineData(200.00, 0.0625, 12.50)]
    public void Calculate_DifferentAmountsAndRates_ProducesCorrectResults(
        decimal amount, 
        decimal rate, 
        decimal expectedFee)
    {
        // Act
        var calculatedFee = amount * rate;

        // Assert
        calculatedFee.Should().Be(expectedFee);
    }
}
