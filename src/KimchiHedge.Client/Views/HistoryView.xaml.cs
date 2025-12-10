using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace KimchiHedge.Client.Views;

public partial class HistoryView : UserControl
{
    public ObservableCollection<TradeHistoryItem> TradeHistory { get; } = new();

    public HistoryView()
    {
        InitializeComponent();
        LoadSampleData();
        HistoryGrid.ItemsSource = TradeHistory;
    }

    private void LoadSampleData()
    {
        // 샘플 데이터
        TradeHistory.Add(new TradeHistoryItem
        {
            DateTime = "2024-12-10 14:32:15",
            EntryKimchi = "+3.52%",
            CloseKimchi = "+2.15%",
            Amount = "0.01234567",
            Result = "익절",
            PnL = "+45,230 KRW"
        });
        TradeHistory.Add(new TradeHistoryItem
        {
            DateTime = "2024-12-10 11:15:42",
            EntryKimchi = "+3.28%",
            CloseKimchi = "+1.89%",
            Amount = "0.00987654",
            Result = "익절",
            PnL = "+32,100 KRW"
        });
        TradeHistory.Add(new TradeHistoryItem
        {
            DateTime = "2024-12-09 22:48:33",
            EntryKimchi = "+3.65%",
            CloseKimchi = "+5.12%",
            Amount = "0.01500000",
            Result = "손절",
            PnL = "-28,450 KRW"
        });
        TradeHistory.Add(new TradeHistoryItem
        {
            DateTime = "2024-12-09 18:22:10",
            EntryKimchi = "+3.41%",
            CloseKimchi = "+2.95%",
            Amount = "0.00800000",
            Result = "수동",
            PnL = "+12,340 KRW"
        });
        TradeHistory.Add(new TradeHistoryItem
        {
            DateTime = "2024-12-09 09:15:55",
            EntryKimchi = "+3.18%",
            CloseKimchi = "+1.75%",
            Amount = "0.01234567",
            Result = "익절",
            PnL = "+52,180 KRW"
        });
    }

    private void PeriodFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        // TODO: 필터 적용
    }

    private void ResultFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        // TODO: 필터 적용
    }

    private void RefreshHistory_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 데이터 새로고침
        MessageBox.Show("거래 내역을 새로고침했습니다.", "새로고침", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public class TradeHistoryItem
{
    public string DateTime { get; set; } = "";
    public string EntryKimchi { get; set; } = "";
    public string CloseKimchi { get; set; } = "";
    public string Amount { get; set; } = "";
    public string Result { get; set; } = "";
    public string PnL { get; set; } = "";
}
