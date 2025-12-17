using FluentAssertions;
using KimchiHedge.Core.Models;
using KimchiHedge.Core.Trading;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KimchiHedge.Core.Tests.Trading;

/// <summary>
/// TradingEngine 단위 테스트
/// </summary>
public class TradingEngineTests
{
    private readonly Mock<IOrderExecutor> _orderExecutorMock;
    private readonly Mock<IPositionManager> _positionManagerMock;
    private readonly Mock<IRollbackService> _rollbackServiceMock;
    private readonly Mock<ICooldownService> _cooldownServiceMock;
    private readonly Mock<ILogger<TradingEngine>> _loggerMock;
    private readonly TradingEngine _engine;

    public TradingEngineTests()
    {
        _orderExecutorMock = new Mock<IOrderExecutor>();
        _positionManagerMock = new Mock<IPositionManager>();
        _rollbackServiceMock = new Mock<IRollbackService>();
        _cooldownServiceMock = new Mock<ICooldownService>();
        _loggerMock = new Mock<ILogger<TradingEngine>>();

        _engine = new TradingEngine(
            _orderExecutorMock.Object,
            _positionManagerMock.Object,
            _rollbackServiceMock.Object,
            _cooldownServiceMock.Object,
            _loggerMock.Object);
    }

    #region 상태 전이 테스트

    [Fact]
    public void OnToggleOn_FromIdle_ShouldChangeToWaitEntry()
    {
        // Arrange
        _engine.State.Should().Be(TradingState.Idle);

        // Act
        _engine.OnToggleOn();

        // Assert
        _engine.State.Should().Be(TradingState.WaitEntry);
    }

    [Fact]
    public async Task OnToggleOff_FromWaitEntry_ShouldChangeToIdle()
    {
        // Arrange
        _engine.OnToggleOn();
        _engine.State.Should().Be(TradingState.WaitEntry);

        // Act
        await _engine.OnToggleOffAsync();

        // Assert
        _engine.State.Should().Be(TradingState.Idle);
    }

    [Fact]
    public void OnToggleOn_WhenAlreadyOn_ShouldRemainInWaitEntry()
    {
        // Arrange
        _engine.OnToggleOn();

        // Act
        _engine.OnToggleOn();

        // Assert
        _engine.State.Should().Be(TradingState.WaitEntry);
    }

    #endregion

    #region 설정 테스트

    [Fact]
    public void UpdateSettings_WithValidSettings_ShouldSucceed()
    {
        // Arrange
        var settings = CreateValidSettings();

        // Act
        _engine.UpdateSettings(settings);

        // Assert
        _engine.Settings.EntryKimchi.Should().Be(3.0m);
        _engine.Settings.TakeProfitKimchi.Should().Be(1.0m);
        _engine.Settings.StopLossKimchi.Should().Be(5.0m);
    }

    [Fact]
    public void UpdateSettings_WithInvalidSettings_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidSettings = new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 4.0m, // 잘못된 값: 익절 > 진입
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = 1,
            CooldownSeconds = 300
        };

        // Act & Assert
        var act = () => _engine.UpdateSettings(invalidSettings);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region 진입 조건 테스트

    [Fact]
    public async Task OnKimchiUpdate_WhenBelowEntryCondition_ShouldNotEnter()
    {
        // Arrange
        var settings = CreateValidSettings();
        _engine.UpdateSettings(settings);
        _engine.OnToggleOn();

        var data = new KimchiPremiumData { Kimchi = 2.5m }; // 진입 조건(3.0%) 미달

        // Act
        await _engine.OnKimchiUpdateAsync(data);

        // Assert
        _engine.State.Should().Be(TradingState.WaitEntry);
        _orderExecutorMock.Verify(x => x.ExecuteUpbitBuyAsync(It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task OnKimchiUpdate_WhenMeetsEntryCondition_ShouldStartEntry()
    {
        // Arrange
        var settings = CreateValidSettings();
        _engine.UpdateSettings(settings);
        _engine.OnToggleOn();

        SetupSuccessfulEntry();

        var data = new KimchiPremiumData { Kimchi = 3.5m }; // 진입 조건(3.0%) 충족

        // Act
        await _engine.OnKimchiUpdateAsync(data);

        // Assert
        _orderExecutorMock.Verify(x => x.ExecuteUpbitBuyAsync(It.IsAny<decimal>()), Times.Once);
        _engine.State.Should().Be(TradingState.PositionOpen);
    }

    #endregion

    #region 청산 조건 테스트

    [Fact]
    public async Task OnKimchiUpdate_WhenTakeProfitConditionMet_ShouldExit()
    {
        // Arrange
        var settings = CreateValidSettings();
        _engine.UpdateSettings(settings);
        _engine.OnToggleOn();

        SetupSuccessfulEntry();
        SetupSuccessfulExit();
        SetupPositionManagerWithPosition();

        // 진입
        await _engine.OnKimchiUpdateAsync(new KimchiPremiumData { Kimchi = 3.5m });
        _engine.State.Should().Be(TradingState.PositionOpen);

        // Act - 익절 조건 (김프 <= 1.0%)
        await _engine.OnKimchiUpdateAsync(new KimchiPremiumData { Kimchi = 0.8m });

        // Assert
        _orderExecutorMock.Verify(x => x.ExecuteUpbitSellAllAsync(), Times.Once);
        _orderExecutorMock.Verify(x => x.ExecuteBingXCloseAsync(), Times.Once);
    }

    [Fact]
    public async Task OnKimchiUpdate_WhenStopLossConditionMet_ShouldExit()
    {
        // Arrange
        var settings = CreateValidSettings();
        _engine.UpdateSettings(settings);
        _engine.OnToggleOn();

        SetupSuccessfulEntry();
        SetupSuccessfulExit();
        SetupPositionManagerWithPosition();

        // 진입
        await _engine.OnKimchiUpdateAsync(new KimchiPremiumData { Kimchi = 3.5m });

        // Act - 손절 조건 (김프 >= 5.0%)
        await _engine.OnKimchiUpdateAsync(new KimchiPremiumData { Kimchi = 5.5m });

        // Assert
        _orderExecutorMock.Verify(x => x.ExecuteUpbitSellAllAsync(), Times.Once);
    }

    #endregion

    #region 롤백 테스트

    [Fact]
    public async Task OnKimchiUpdate_WhenBingXFails_ShouldTriggerRollback()
    {
        // Arrange
        var settings = CreateValidSettings();
        _engine.UpdateSettings(settings);
        _engine.OnToggleOn();

        // 업비트 성공, BingX 실패 설정
        _orderExecutorMock
            .Setup(x => x.ExecuteUpbitBuyAsync(It.IsAny<decimal>()))
            .ReturnsAsync(OrderResult.Success(0.001m, 50000000m, 250m));

        _orderExecutorMock
            .Setup(x => x.ExecuteBingXShortAsync(It.IsAny<decimal>()))
            .ReturnsAsync(OrderResult.Failure("BingX API Error"));

        var data = new KimchiPremiumData { Kimchi = 3.5m };

        // Act
        await _engine.OnKimchiUpdateAsync(data);

        // Assert
        _rollbackServiceMock.Verify(x => x.ExecuteRollbackAsync(), Times.Once);
        _engine.State.Should().Be(TradingState.ErrorRollback);
    }

    [Fact]
    public async Task OnKimchiUpdate_WhenQuantityMismatch_ShouldTriggerRollback()
    {
        // Arrange
        var settings = CreateValidSettings();
        _engine.UpdateSettings(settings);
        _engine.OnToggleOn();

        // 수량 불일치 설정
        _orderExecutorMock
            .Setup(x => x.ExecuteUpbitBuyAsync(It.IsAny<decimal>()))
            .ReturnsAsync(OrderResult.Success(0.001m, 50000000m, 250m));

        _orderExecutorMock
            .Setup(x => x.ExecuteBingXShortAsync(It.IsAny<decimal>()))
            .ReturnsAsync(OrderResult.Success(0.0009m, 40000m, 0.5m)); // 불일치

        var data = new KimchiPremiumData { Kimchi = 3.5m };

        // Act
        await _engine.OnKimchiUpdateAsync(data);

        // Assert
        _rollbackServiceMock.Verify(x => x.ExecuteRollbackAsync(), Times.Once);
    }

    #endregion

    #region 동시성 테스트

    [Fact]
    public async Task OnKimchiUpdate_ConcurrentCalls_ShouldProcessOnlyOne()
    {
        // Arrange
        var settings = CreateValidSettings();
        _engine.UpdateSettings(settings);
        _engine.OnToggleOn();

        var tcs = new TaskCompletionSource();
        var callCount = 0;

        _orderExecutorMock
            .Setup(x => x.ExecuteUpbitBuyAsync(It.IsAny<decimal>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref callCount);
                await tcs.Task; // 첫 번째 호출 블록
                return OrderResult.Success(0.001m, 50000000m, 250m);
            });

        var data = new KimchiPremiumData { Kimchi = 3.5m };

        // Act - 동시에 여러 호출
        var task1 = _engine.OnKimchiUpdateAsync(data);
        await Task.Delay(50); // 첫 번째 호출이 시작되도록 대기
        var task2 = _engine.OnKimchiUpdateAsync(data);
        var task3 = _engine.OnKimchiUpdateAsync(data);

        tcs.SetResult(); // 블록 해제
        await Task.WhenAll(task1, task2, task3);

        // Assert - 첫 번째 호출만 실행됨
        callCount.Should().Be(1);
    }

    #endregion

    #region 헬퍼 메서드

    private static TradingSettings CreateValidSettings()
    {
        return new TradingSettings
        {
            EntryKimchi = 3.0m,
            TakeProfitKimchi = 1.0m,
            StopLossKimchi = 5.0m,
            EntryRatio = 50m,
            Leverage = 1,
            CooldownSeconds = 300,
            QuantityTolerance = 0.00000001m
        };
    }

    private void SetupSuccessfulEntry()
    {
        _orderExecutorMock
            .Setup(x => x.ExecuteUpbitBuyAsync(It.IsAny<decimal>()))
            .ReturnsAsync(OrderResult.Success(0.001m, 50000000m, 250m));

        _orderExecutorMock
            .Setup(x => x.ExecuteBingXShortAsync(It.IsAny<decimal>()))
            .ReturnsAsync(OrderResult.Success(0.001m, 40000m, 0.5m));

        _orderExecutorMock
            .Setup(x => x.SetLeverageAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupSuccessfulExit()
    {
        _orderExecutorMock
            .Setup(x => x.ExecuteUpbitSellAllAsync())
            .ReturnsAsync(OrderResult.Success(0.001m, 51000000m, 255m));

        _orderExecutorMock
            .Setup(x => x.ExecuteBingXCloseAsync())
            .ReturnsAsync(OrderResult.Success(0.001m, 39500m, 0.5m));
    }

    private void SetupPositionManagerWithPosition()
    {
        var position = new Position
        {
            Status = PositionStatus.Open,
            UpbitBtcAmount = 0.001m,
            FuturesShortAmount = 0.001m
        };

        _positionManagerMock
            .SetupGet(x => x.CurrentPosition)
            .Returns(position);

        _positionManagerMock
            .SetupGet(x => x.HasPosition)
            .Returns(true);
    }

    #endregion
}
