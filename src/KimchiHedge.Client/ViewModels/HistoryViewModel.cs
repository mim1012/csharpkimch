using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KimchiHedge.Client.Services;
using KimchiHedge.Core.Exchanges;
using KimchiHedge.Core.Models;
using Microsoft.Win32;

namespace KimchiHedge.Client.ViewModels;

/// <summary>
/// 거래 내역 ViewModel
/// 업비트/BingX 거래소 API에서 실제 거래 내역을 조회
/// </summary>
public partial class HistoryViewModel : ViewModelBase
{
    private readonly IExchangeFactory _exchangeFactory;
    private List<TradeHistoryItem> _allTrades = new();

    #region Statistics

    [ObservableProperty]
    private string _totalTrades = "0";

    [ObservableProperty]
    private string _winRate = "0.0";

    [ObservableProperty]
    private string _totalProfit = "0";

    [ObservableProperty]
    private string _avgProfit = "0";

    [ObservableProperty]
    private bool _isProfitPositive;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    #endregion

    #region Filters

    [ObservableProperty]
    private int _selectedPeriodIndex;

    [ObservableProperty]
    private int _selectedResultIndex;

    #endregion

    #region Data

    [ObservableProperty]
    private ObservableCollection<TradeHistoryItem> _trades = new();

    #endregion

    public HistoryViewModel(IExchangeFactory exchangeFactory)
    {
        _exchangeFactory = exchangeFactory;
    }

    /// <summary>
    /// View가 로드될 때 호출
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadHistoryFromExchangesAsync();
    }

    /// <summary>
    /// 거래소 API에서 거래 내역 조회
    /// </summary>
    private async Task LoadHistoryFromExchangesAsync()
    {
        IsBusy = true;
        StatusMessage = "거래 내역 조회 중...";

        try
        {
            var allHistories = new List<TradeHistory>();

            // 기간 필터 계산
            DateTime? startTime = SelectedPeriodIndex switch
            {
                1 => DateTime.Today,  // 오늘
                2 => DateTime.Today.AddDays(-7),  // 최근 7일
                3 => DateTime.Today.AddDays(-30),  // 최근 30일
                _ => null  // 전체
            };

            // 업비트 거래 내역 조회
            try
            {
                var upbitExchange = await _exchangeFactory.CreateSpotExchangeAsync();
                if (upbitExchange != null)
                {
                    var upbitHistory = await upbitExchange.GetOrderHistoryAsync(
                        symbol: null,  // 전체 심볼
                        startTime: startTime,
                        endTime: null,
                        limit: 100);
                    allHistories.AddRange(upbitHistory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"업비트 조회 실패: {ex.Message}");
            }

            // BingX 거래 내역 조회
            try
            {
                var bingxExchange = await _exchangeFactory.CreateFuturesExchangeAsync();
                if (bingxExchange != null)
                {
                    var bingxHistory = await bingxExchange.GetOrderHistoryAsync(
                        symbol: null,  // 전체 심볼
                        startTime: startTime,
                        endTime: null,
                        limit: 100);
                    allHistories.AddRange(bingxHistory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BingX 조회 실패: {ex.Message}");
            }

            // 시간순 정렬 (최신 먼저)
            allHistories = allHistories.OrderByDescending(h => h.CreatedAt).ToList();

            // TradeHistoryItem으로 변환
            _allTrades = allHistories.Select(h => new TradeHistoryItem
            {
                DateTime = h.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Symbol = h.Symbol,
                Side = h.Side,
                Amount = $"{h.ExecutedQuantity:G6}",
                Price = $"{h.AveragePrice:N0}",
                Total = $"{h.ExecutedAmount:N0}",
                Fee = $"{h.Fee:N2}",
                Exchange = h.Exchange,
                PnL = h.RealizedPnL.HasValue ? $"{(h.RealizedPnL >= 0 ? "+" : "")}{h.RealizedPnL:N0}" : "-",
                Result = DetermineResult(h)
            }).ToList();

            // 필터링 적용
            FilterTrades();

            if (_allTrades.Count == 0)
            {
                StatusMessage = "거래 내역이 없습니다.";
            }
            else
            {
                StatusMessage = $"총 {_allTrades.Count}건의 거래 내역을 조회했습니다.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"조회 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 거래 결과 판정
    /// </summary>
    private static string DetermineResult(TradeHistory h)
    {
        if (h.RealizedPnL.HasValue)
        {
            return h.RealizedPnL >= 0 ? "익절" : "손절";
        }
        // 현물은 매수/매도로 표시
        return h.Side == "Buy" ? "매수" : "매도";
    }

    private void FilterTrades()
    {
        IEnumerable<TradeHistoryItem> filtered = _allTrades;

        // 결과 필터링 (0: 전체, 1: 익절, 2: 손절, 3: 매수, 4: 매도)
        if (SelectedResultIndex > 0)
        {
            var filterValue = SelectedResultIndex switch
            {
                1 => "익절",
                2 => "손절",
                3 => "매수",
                4 => "매도",
                _ => null
            };

            if (filterValue != null)
            {
                filtered = filtered.Where(t => t.Result == filterValue);
            }
        }

        Trades = new ObservableCollection<TradeHistoryItem>(filtered);
        CalculateStatistics();
    }

    private void CalculateStatistics()
    {
        if (Trades.Count == 0)
        {
            TotalTrades = "0";
            WinRate = "0.0";
            TotalProfit = "0";
            AvgProfit = "0";
            IsProfitPositive = false;
            return;
        }

        TotalTrades = Trades.Count.ToString();

        // 익절 비율 계산
        var profitTrades = Trades.Count(t => t.Result == "익절");
        var lossTrades = Trades.Count(t => t.Result == "손절");
        var totalPnlTrades = profitTrades + lossTrades;

        if (totalPnlTrades > 0)
        {
            WinRate = ((decimal)profitTrades / totalPnlTrades * 100).ToString("0.0");
        }
        else
        {
            WinRate = "0.0";
        }

        // 총 손익 계산 (PnL 파싱)
        decimal totalPnl = 0;
        foreach (var trade in Trades)
        {
            if (TryParsePnL(trade.PnL, out var pnl))
            {
                totalPnl += pnl;
            }
        }

        TotalProfit = totalPnl >= 0 ? $"+{totalPnl:N0}" : totalPnl.ToString("N0");
        IsProfitPositive = totalPnl >= 0;

        if (Trades.Count > 0)
        {
            var avgPnl = totalPnl / Trades.Count;
            AvgProfit = avgPnl >= 0 ? $"+{avgPnl:N0}" : avgPnl.ToString("N0");
        }
        else
        {
            AvgProfit = "0";
        }
    }

    private static bool TryParsePnL(string pnlStr, out decimal pnl)
    {
        pnl = 0;
        if (string.IsNullOrEmpty(pnlStr) || pnlStr == "-")
            return false;

        // "+1,234" 또는 "-1,234" 형식 파싱
        var cleaned = pnlStr.Replace(",", "").Replace("+", "").Replace(" ", "").Replace("KRW", "");
        return decimal.TryParse(cleaned, out pnl);
    }

    partial void OnSelectedPeriodIndexChanged(int value)
    {
        _ = LoadHistoryFromExchangesAsync();
    }

    partial void OnSelectedResultIndexChanged(int value)
    {
        FilterTrades();
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        await LoadHistoryFromExchangesAsync();
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (Trades.Count == 0)
        {
            System.Windows.MessageBox.Show("내보낼 거래 내역이 없습니다.", "내보내기", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV 파일|*.csv",
            FileName = $"KimchiHedge_Trades_{DateTime.Now:yyyyMMdd}.csv",
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("DateTime,Symbol,Side,Amount,Price,Total,Fee,Exchange,PnL,Result");

                foreach (var trade in Trades)
                {
                    sb.AppendLine($"{trade.DateTime},{trade.Symbol},{trade.Side},{trade.Amount},{trade.Price},{trade.Total},{trade.Fee},{trade.Exchange},{trade.PnL},{trade.Result}");
                }

                await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
                System.Windows.MessageBox.Show($"거래 내역이 저장되었습니다.\n{dialog.FileName}", "내보내기 완료", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"저장 실패: {ex.Message}", "내보내기 실패", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}

/// <summary>
/// 거래 내역 항목 (UI 표시용)
/// </summary>
public class TradeHistoryItem
{
    public string DateTime { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public string Fee { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string PnL { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}
