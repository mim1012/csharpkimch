using FluentAssertions;
using KimchiHedge.Core.Models;
using Xunit;

namespace KimchiHedge.Core.Tests.Models;

/// <summary>
/// TradingSettings 유효성 검증 테스트
/// </summary>
public class TradingSettingsTests
{
    #region 유효한 설정 테스트

    [Fact]
    public void Validate_WithValidSettings_ShouldReturnTrue()
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = 1,
            CooldownSeconds = 300
        };

        // Act
        var result = settings.Validate(out var errorMessage);

        // Assert
        result.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 1800)]
    [InlineData(1, 300)]
    public void Validate_WithValidLeverageAndCooldown_ShouldReturnTrue(int leverage, int cooldown)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = leverage,
            CooldownSeconds = cooldown
        };

        // Act
        var result = settings.Validate(out _);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region 진입 김프 검증 테스트

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidEntryKimchi_ShouldReturnFalse(decimal entryKimchi)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = entryKimchi,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = 1,
            CooldownSeconds = 300
        };

        // Act
        var result = settings.Validate(out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("진입 김프값");
    }

    #endregion

    #region 익절 김프 검증 테스트

    [Theory]
    [InlineData(3.0, 3.0)]  // 같은 값
    [InlineData(3.0, 4.0)]  // 익절 > 진입
    public void Validate_WithInvalidTakeProfitKimchi_ShouldReturnFalse(decimal entry, decimal takeProfit)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = entry,
            TakeProfitKimchi = takeProfit,
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = 1,
            CooldownSeconds = 300
        };

        // Act
        var result = settings.Validate(out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("익절 김프값");
    }

    #endregion

    #region 손절 김프 검증 테스트

    [Theory]
    [InlineData(3.0, 3.0)]  // 같은 값
    [InlineData(3.0, 2.0)]  // 손절 < 진입
    public void Validate_WithInvalidStopLossKimchi_ShouldReturnFalse(decimal entry, decimal stopLoss)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = entry,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = stopLoss,
            EntryRatio = 50m,
            Leverage = 1,
            CooldownSeconds = 300
        };

        // Act
        var result = settings.Validate(out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("손절 김프값");
    }

    #endregion

    #region 진입 비율 검증 테스트

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(101)]
    [InlineData(150)]
    public void Validate_WithInvalidEntryRatio_ShouldReturnFalse(decimal entryRatio)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = entryRatio,
            Leverage = 1,
            CooldownSeconds = 300
        };

        // Act
        var result = settings.Validate(out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("진입 비율");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WithValidEntryRatio_ShouldReturnTrue(decimal entryRatio)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = entryRatio,
            Leverage = 1,
            CooldownSeconds = 300
        };

        // Act
        var result = settings.Validate(out _);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region 레버리지 검증 테스트

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(10)]
    public void Validate_WithInvalidLeverage_ShouldReturnFalse(int leverage)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = leverage,
            CooldownSeconds = 300
        };

        // Act
        var result = settings.Validate(out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("레버리지");
    }

    #endregion

    #region 쿨다운 검증 테스트

    [Theory]
    [InlineData(59)]
    [InlineData(0)]
    [InlineData(1801)]
    [InlineData(3600)]
    public void Validate_WithInvalidCooldown_ShouldReturnFalse(int cooldown)
    {
        // Arrange
        var settings = new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = 1,
            CooldownSeconds = cooldown
        };

        // Act
        var result = settings.Validate(out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("쿨다운");
    }

    #endregion

    #region 기본값 테스트

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var settings = new TradingSettings();

        // Assert
        settings.Leverage.Should().Be(1);
        settings.CooldownSeconds.Should().Be(300);
        settings.QuantityTolerance.Should().Be(0.00000001m);
    }

    #endregion
}
