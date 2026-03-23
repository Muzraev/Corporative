using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Анализатор_сетевых_подключений;

namespace NetworkAnalyzer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<NetworkInterfaceInfo> _interfaces = new ObservableCollection<NetworkInterfaceInfo>();

        private ObservableCollection<string> _history = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            LoadNetworkInterfaces();
            InterfacesListBox.ItemsSource = _interfaces;
            HistoryListBox.ItemsSource = _history;
        }

        private void LoadNetworkInterfaces()
        {
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    var ipProps = nic.GetIPProperties();
                    var ipv4Addresses = ipProps.UnicastAddresses
                        .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                    foreach (var addr in ipv4Addresses)
                    {
                        _interfaces.Add(new NetworkInterfaceInfo
                        {
                            Name = nic.Name,
                            Description = nic.Description,
                            IPAddress = addr.Address.ToString(),
                            SubnetMask = addr.IPv4Mask?.ToString() ?? "N/A",
                            MACAddress = nic.GetPhysicalAddress().ToString(),
                            Status = nic.OperationalStatus.ToString(),
                            Speed = nic.Speed > 0 ? (nic.Speed / 1_000_000).ToString() + " Mbps" : "Unknown",
                            InterfaceType = nic.NetworkInterfaceType.ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении сетевых интерфейсов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InterfacesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InterfacesListBox.SelectedItem is NetworkInterfaceInfo selected)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Имя: {selected.Name}");
                sb.AppendLine($"Описание: {selected.Description}");
                sb.AppendLine($"IP-адрес: {selected.IPAddress}");
                sb.AppendLine($"Маска подсети: {selected.SubnetMask}");
                sb.AppendLine($"MAC-адрес: {selected.MACAddress}");
                sb.AppendLine($"Состояние: {selected.Status}");
                sb.AppendLine($"Скорость: {selected.Speed}");
                sb.AppendLine($"Тип: {selected.InterfaceType}");
                InterfaceDetailsTextBox.Text = sb.ToString();
            }
            else
            {
                InterfaceDetailsTextBox.Clear();
            }
        }

        // Анализ URL
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Введите URL", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_history.Contains(url))
                _history.Add(url);

            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                }

                Uri uri = new Uri(url);
                StringBuilder result = new StringBuilder();
                result.AppendLine($"Схема (протокол): {uri.Scheme}");
                result.AppendLine($"Хост: {uri.Host}");
                result.AppendLine($"Порт: {uri.Port}");
                result.AppendLine($"Путь: {uri.AbsolutePath}");
                result.AppendLine($"Параметры запроса: {uri.Query}");
                result.AppendLine($"Фрагмент: {uri.Fragment}");

                UrlParseResultTextBox.Text = result.ToString();
            }
            catch (Exception ex)
            {
                UrlParseResultTextBox.Text = $"Ошибка URL: {ex.Message}";
            }
        }

        private async void PingButton_Click(object sender, RoutedEventArgs e)
        {
            string host = GetHostFromUrl();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Не удалось извлечь хост из URL. Введите корректный URL.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            PingResultTextBox.Text = "Выполняется ping...";
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(host);
                    if (reply.Status == IPStatus.Success)
                    {
                        PingResultTextBox.Text = $"Ping успешен: {reply.RoundtripTime} мс\nАдрес: {reply.Address}";
                    }
                    else
                    {
                        PingResultTextBox.Text = $"Ping не удался: {reply.Status}";
                    }
                }
            }
            catch (Exception ex)
            {
                PingResultTextBox.Text = $"Ошибка при выполнении ping: {ex.Message}";
            }
        }

        private async void DnsButton_Click(object sender, RoutedEventArgs e)
        {
            string host = GetHostFromUrl();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Не удалось извлечь хост из URL. Введите корректный URL.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DnsResultTextBox.Text = "Выполняется DNS-запрос...";
            try
            {
                IPHostEntry hostEntry = await Dns.GetHostEntryAsync(host);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Каноническое имя: {hostEntry.HostName}");
                sb.AppendLine("IP-адреса:");
                foreach (var ip in hostEntry.AddressList)
                {
                    sb.AppendLine($"  {ip} ({GetAddressType(ip)})");
                }
                DnsResultTextBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                DnsResultTextBox.Text = $"Ошибка DNS: {ex.Message}";
            }
        }

        private string GetHostFromUrl()
        {
            string url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
                return null;

            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                }
                Uri uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return url;
            }
        }

        private string GetAddressType(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
                return "loopback";

            byte[] bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (bytes[0] == 10)
                    return "частный (локальный)";
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return "частный (локальный)";
                if (bytes[0] == 192 && bytes[1] == 168)
                    return "частный (локальный)";
                return "публичный";
            }
            return "неизвестно";
        }

        private void HistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryListBox.SelectedItem is string url)
            {
                UrlTextBox.Text = url;
            }
        }
    }
}