using FluentAssertions;
using KimchiHedge.Core.Models;
using KimchiHedge.Core.Trading;
using Xunit;

namespace KimchiHedge.Core.Tests.Trading;

/// <summary>
/// PositionManager 단위 테스트
/// </summary>
public class PositionManagerTests
{
    private readonly PositionManager _manager;

    public PositionManagerTests()
    {
        _manager = new PositionManager();
    }

    #region 포지션 생성 테스트

    [Fact]
    public void CreatePosition_ShouldCreateWithOpeningStatus()
    {
        // Arrange
        var entryKimchi = 3.5m;

        // Act
        _manager.CreatePosition(entryKimchi);

        // Assert
        _manager.CurrentPosition.Should().NotBeNull();
        _manager.CurrentPosition!.Status.Should().Be(PositionStatus.Opening);
        _manager.CurrentPosition.EntryKimchi.Should().Be(entryKimchi);
        _manager.CurrentPosition.EntryTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void HasPosition_BeforeCreate_ShouldBeFalse()
    {
        // Assert
        _manager.HasPosition.Should().BeFalse();
        _manager.CurrentPosition.Should().BeNull();
    }

    #endregion

    #region 진입 완료 테스트

    [Fact]
    public void CompleteEntry_ShouldUpdatePositionToOpen()
    {
        // Arrange
        _manager.CreatePosition(3.5m);

        // Act
        _manager.CompleteEntry(
            upbitAmount: 0.001m,
            upbitPrice: 50000000m,
            bingxAmount: 0.001m,
            bingxPrice: 40000m,
            upbitFee: 250m,
            bingxFee: 0.5m);

        // Assert
        var position = _manager.CurrentPosition!;
        position.Status.Should().Be(PositionStatus.Open);
        position.UpbitBtcAmount.Should().Be(0.001m);
        position.UpbitEntryPrice.Should().Be(50000000m);
        position.FuturesShortAmount.Should().Be(0.001m);
        position.FuturesEntryPrice.Should().Be(40000m);
        position.UpbitFee.Should().Be(250m);
        position.FuturesFee.Should().Be(0.5m);
        _manager.HasPosition.Should().BeTrue();
    }

    [Fact]
    public void CompleteEntry_ShouldRaisePositionOpenedEvent()
    {
        // Arrange
        _manager.CreatePosition(3.5m);
        Position? openedPosition = null;
        _manager.PositionOpened += (_, pos) => openedPosition = pos;

        // Act
        _manager.CompleteEntry(0.001m, 50000000m, 0.001m, 40000m, 250m, 0.5m);

        // Assert
        openedPosition.Should().NotBeNull();
        openedPosition!.Status.Should().Be(PositionStatus.Open);
    }

    #endregion

    #region 청산 완료 테스트

    [Fact]
    public void CompleteClose_ShouldCalculatePnLAndClearPosition()
    {
        // Arrange
        _manager.CreatePosition(3.5m);
        _manager.CompleteEntry(0.001m, 50000000m, 0.001m, 40000m, 250m, 0.5m);

        Position? closedPosition = null;
        _manager.PositionClosed += (_, pos) => closedPosition = pos;

        // Act
        _manager.CompleteClose(
            closeKimchi: 1.0m,
            reason: CloseReason.TakeProfit,
            upbitSellPrice: 51000000m,
            bingxClosePrice: 39500m,
            upbitFee: 255m,
            bingxFee: 0.5m);

        // Assert
        closedPosition.Should().NotBeNull();
        closedPosition!.Status.Should().Be(PositionStatus.Closed);
        closedPosition.CloseReason.Should().Be(CloseReason.TakeProfit);
        closedPosition.CloseKimchi.Should().Be(1.0m);
        closedPosition.CloseTime.Should().NotBeNull();
        closedPosition.RealizedPnL.Should().NotBe(0); // 손익이 계산되었는지
        _manager.CurrentPosition.Should().BeNull();
        _manager.HasPosition.Should().BeFalse();
    }

    [Fact]
    public void CompleteClose_WithDifferentReasons_ShouldSetCorrectReason()
    {
        // Arrange & Act - Manual
        _manager.CreatePosition(3.5m);
        _manager.CompleteEntry(0.001m, 50000000m, 0.001m, 40000m, 250m, 0.5m);

        Position? closedPosition = null;
        _manager.PositionClosed += (_, pos) => closedPosition = pos;

        _manager.CompleteClose(1.0m, CloseReason.Manual, 51000000m, 39500m, 255m, 0.5m);

        // Assert
        closedPosition!.CloseReason.Should().Be(CloseReason.Manual);
    }

    #endregion

    #region 손익 계산 테스트

    [Fact]
    public void CompleteClose_WithProfitScenario_ShouldCalculatePositivePnL()
    {
        // Arrange
        _manager.CreatePosition(3.5m);
        // 진입: 업비트 50,000,000 KRW, BingX $40,000 (수수료 최소화)
        _manager.CompleteEntry(0.01m, 50000000m, 0.01m, 40000m, 50m, 0.1m);

        Position? closedPosition = null;
        _manager.PositionClosed += (_, pos) => closedPosition = pos;

        // Act
        // 청산: 업비트 52,000,000 KRW (+2,000,000/BTC), BingX $38,000 (숏이익 $2,000/BTC)
        // 업비트 손익: (52M - 50M) * 0.01 - 50 - 50 = 20,000 - 100 = 19,900 KRW
        // BingX 손익(USD): (40,000 - 38,000) * 0.01 - 0.1 - 0.1 = 20 - 0.2 = 19.8 USD
        // BingX 손익(KRW): 19.8 * (52,000,000/38,000) ≈ 27,095 KRW
        // 총 손익: 19,900 + 27,095 ≈ 46,995 KRW (양수)
        _manager.CompleteClose(1.0m, CloseReason.TakeProfit, 52000000m, 38000m, 50m, 0.1m);

        // Assert
        closedPosition!.RealizedPnL.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompleteClose_WithLossScenario_ShouldCalculateNegativePnL()
    {
        // Arrange
        _manager.CreatePosition(3.5m);
        _manager.CompleteEntry(0.001m, 50000000m, 0.001m, 40000m, 250m, 0.5m);

        Position? closedPosition = null;
        _manager.PositionClosed += (_, pos) => closedPosition = pos;

        // Act
        // 손실 시나리오: 업비트 49,000,000 KRW (-1,000,000), BingX $40,500 (숏손실 $500)
        _manager.CompleteClose(5.5m, CloseReason.StopLoss, 49000000m, 40500m, 245m, 0.5m);

        // Assert
        closedPosition!.RealizedPnL.Should().BeLessThan(0);
    }

    #endregion

    #region 롤백 테스트

    [Fact]
    public void MarkAsRolledBack_ShouldSetRollbackStatusAndClear()
    {
        // Arrange
        _manager.CreatePosition(3.5m);
        _manager.CompleteEntry(0.001m, 50000000m, 0.001m, 40000m, 250m, 0.5m);

        Position? rolledBackPosition = null;
        _manager.PositionClosed += (_, pos) => rolledBackPosition = pos;

        // Act
        _manager.MarkAsRolledBack(CloseReason.Rollback);

        // Assert
        rolledBackPosition.Should().NotBeNull();
        rolledBackPosition!.Status.Should().Be(PositionStatus.Rollback);
        rolledBackPosition.CloseReason.Should().Be(CloseReason.Rollback);
        _manager.CurrentPosition.Should().BeNull();
    }

    #endregion

    #region Clear 테스트

    [Fact]
    public void Clear_ShouldRemovePosition()
    {
        // Arrange
        _manager.CreatePosition(3.5m);

        // Act
        _manager.Clear();

        // Assert
        _manager.CurrentPosition.Should().BeNull();
        _manager.HasPosition.Should().BeFalse();
    }

    #endregion

    #region 상태 변경 테스트

    [Fact]
    public void SetStatus_ShouldChangePositionStatus()
    {
        // Arrange
        _manager.CreatePosition(3.5m);

        // Act
        _manager.SetStatus(PositionStatus.Closing);

        // Assert
        _manager.CurrentPosition!.Status.Should().Be(PositionStatus.Closing);
    }

    [Fact]
    public void SetStatus_WhenNoPosition_ShouldNotThrow()
    {
        // Act
        var act = () => _manager.SetStatus(PositionStatus.Open);

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
