using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;

namespace RossvyazClient
{
    public partial class MainWindow : Window
    {
        private HubConnection _hubConnection;
        private string _myConnectionId = null;
        private WaveInEvent _waveIn;
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _playbackProvider;
        private bool _isRecording = false;

        public ObservableCollection<string> Channels { get; set; } = new ObservableCollection<string>();
        private string _currentChannel = null;
        private readonly string _userName = "Командир";

        public MainWindow()
        {
            InitializeComponent();
            ChannelsList.ItemsSource = Channels;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ConnectToHubAsync();
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_waveIn != null) { _waveIn.StopRecording(); _waveIn.Dispose(); _waveIn = null; }
            if (_waveOut != null) { _waveOut.Stop(); _waveOut.Dispose(); _waveOut = null; }
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !_isRecording && !MessageInput.IsFocused && !NewChannelNameInput.IsFocused)
                StartTalking();
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && _isRecording)
                StopTalking();
        }

        private void StartTalking()
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected || string.IsNullOrEmpty(_currentChannel))
            {
                AddSystemMessage("Нельзя говорить: нет подключения или вы не в канале.");
                return;
            }

            if (_waveIn == null)
            {
                _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 1), BufferMilliseconds = 60 };
                _waveIn.DataAvailable += WaveIn_DataAvailable;
            }

            _waveIn?.StartRecording();
            _isRecording = true;
            StatusLabel.Text = "В ЭФИРЕ";
            StatusLabel.Foreground = Brushes.LightGreen;
            StatusDot.Fill = Brushes.LightGreen;
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(50, 144, 238, 144));
            ((Storyboard)this.Resources["OnAirPulse"]).Begin();
        }

        private void StopTalking()
        {
            _isRecording = false;
            StatusLabel.Text = "ОЖИДАНИЕ";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(230, 57, 70));
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(230, 57, 70));
            StatusBadge.Background = new SolidColorBrush(Color.FromArgb(26, 230, 57, 70));
            ((Storyboard)this.Resources["OnAirPulse"]).Stop();

            if (_waveIn != null) { _waveIn.StopRecording(); }
        }

        private async void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected && !string.IsNullOrEmpty(_currentChannel))
            {
                try
                {
                    var chunk = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, 0, chunk, 0, e.BytesRecorded);
                    await _hubConnection.SendAsync("SendAudio", _currentChannel, chunk);
                }
                catch { }
            }
        }

        private void OpenCreateChannelModal(object sender, RoutedEventArgs e) => CreateChannelModal.Visibility = Visibility.Visible;
        private void CloseCreateChannelModal(object sender, RoutedEventArgs e) => CreateChannelModal.Visibility = Visibility.Collapsed;

        private void ConfirmCreateChannel(object sender, RoutedEventArgs e)
        {
            string name = NewChannelNameInput.Text.Trim().ToUpper();
            if (!string.IsNullOrEmpty(name))
            {
                if (!Channels.Contains(name))
                {
                    Channels.Add(name);
                    AddSystemMessage($"Развернута новая линия связи: {name}");
                    NewChannelNameInput.Clear();
                    CreateChannelModal.Visibility = Visibility.Collapsed;
                }
                else MessageBox.Show("Такой канал уже существует.");
            }
        }

        private async void Channel_Click(object sender, RoutedEventArgs e)
        {
            string name = (sender as Button).Tag.ToString();
            CurrentChannelTitle.Text = name;
            AddSystemMessage($"Переключение на частоту: {name}");
            _currentChannel = name;

            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                try { await _hubConnection.InvokeAsync("JoinChannel", name, _userName); }
                catch (Exception ex) { AddSystemMessage("Ошибка при попытке присоединиться к каналу: " + ex.Message); }
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e) => _ = SendMessageAsync();
        private void MessageInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) _ = SendMessageAsync(); }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(MessageInput.Text)) return;
            if (string.IsNullOrEmpty(_currentChannel)) { AddSystemMessage("Сначала выберите канал."); return; }

            string text = MessageInput.Text.Trim();
            MessageInput.Clear();

            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                try { await _hubConnection.InvokeAsync("SendMessage", _currentChannel, _userName, text); }
                catch { AddSystemMessage("Не удалось отправить сообщение на сервер."); }
            }
            else AddSystemMessage("Нет подключения к серверу.");
        }

        private void AddIncomingMessage(string user, string text, bool system = false)
        {
            if (system)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, 74, 222, 128)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                border.Child = new TextBlock { Text = $"[СИСТЕМА]: {text}", Foreground = Brushes.LightGreen, FontSize = 11, FontWeight = FontWeights.Bold };
                ChatPanel.Children.Add(border);
            }
            else
            {
                var bubble = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(31, 34, 38)),
                    CornerRadius = new CornerRadius(15, 15, 2, 15),
                    Padding = new Thickness(15, 12, 15, 12),
                    Margin = new Thickness(0, 0, 0, 15),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    BorderThickness = new Thickness(1)
                };

                bubble.Child = new TextBlock
                {
                    Text = $"{user}: {text}",
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                };

                ChatPanel.Children.Add(bubble);
            }
            ChatScroll.ScrollToEnd();
        }

        private void AddSystemMessage(string text) => AddIncomingMessage("СИСТЕМА", text, system: true);

        private void Invite_Click(object sender, RoutedEventArgs e)
        {
            string code = "RS-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
            Clipboard.SetText(code);
            AddSystemMessage($"Код доступа {code} скопирован в буфер обмена.");
        }

        private async Task ConnectToHubAsync()
        {
            var url = "http://localhost:5000/chatHub"; // поменяйте при необходимости
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<string, string, string>("ReceiveMessage", (channel, user, text) =>
            {
                if (channel == _currentChannel)
                {
                    Dispatcher.Invoke(() => AddIncomingMessage(user, text));
                }
            });

            _hubConnection.On<string>("SystemMessage", msg => Dispatcher.Invoke(() => AddSystemMessage(msg)));

            _hubConnection.On<string, byte[]>("ReceiveAudio", (senderConnectionId, audioBytes) =>
            {
                if (senderConnectionId == _myConnectionId) return;
                if (_playbackProvider != null && audioBytes != null && audioBytes.Length > 0)
                {
                    _playbackProvider.AddSamples(audioBytes, 0, audioBytes.Length);
                }
            });

            try
            {
                await _hubConnection.StartAsync();
                _myConnectionId = await _hubConnection.InvokeAsync<string>("GetConnectionId");
                AddSystemMessage("Подключение к серверу установлено.");

                if (_playbackProvider == null)
                {
                    var waveFormat = new WaveFormat(16000, 1);
                    _playbackProvider = new BufferedWaveProvider(waveFormat)
                    {
                        DiscardOnBufferOverflow = true,
                        BufferDuration = TimeSpan.FromSeconds(5)
                    };
                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(_playbackProvider);
                    _waveOut.Play();
                }
            }
            catch (Exception ex)
            {
                AddSystemMessage("Не удалось подключиться к серверу: " + ex.Message);
            }
        }
    }
}
