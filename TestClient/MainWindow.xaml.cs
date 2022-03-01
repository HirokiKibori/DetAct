using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using TestClient.Source;

namespace TestClient
{
    public partial class MainWindow : Window
    {
        private static readonly string FILE_NAME = "StackExample";

        private DetActClient client = null;

        private AtomicAction action;

        private long outputCounter = 0;

        public MainWindow()
        {
            InitializeComponent();

            SetButtonsEnabled(false);

            action = new();
            action.Print = (value) => PrintToOutput(value);
            action.Send = (message) => client?.Send(message);
        }

        private void PrintToConsole(string text)
        {
            Dispatcher.Invoke(callback: () =>
            {
                consoleTextBox.AppendText($"> {text}{Environment.NewLine}");
                consoleTextBox.ScrollToEnd();
            });
        }

        private void PrintToOutput(string text)
        {
            Dispatcher.Invoke(callback: () =>
            {
                receivedTextBox.AppendText($"{outputCounter++}: {text}{Environment.NewLine}");
                receivedTextBox.ScrollToEnd();
            });
        }

        private void PrintErrorToOutput(string text)
            => PrintToConsole(text: $"ERR: {text}");

        private void SetButtonsEnabled(bool enabled)
        {
            Dispatcher.Invoke(callback: () =>
            {
                pushButton.IsEnabled = enabled;
                popButton.IsEnabled = enabled;
            });
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            connectButton.IsEnabled = false;

            if(client is null) {
                client = new();

                try {
                    connectButton.Content = "Close";

                    await client.Connect(new Uri(uriTextBox.Text), ReceiveClientClosed);
                    Dispatcher.Invoke(callback: () => client.StartListening((message) => ReceiveMessage(message)));
                    PrintToConsole(text: "connected");

                    connectButton.IsEnabled = true;

                    RunTree(FILE_NAME);
                }
                catch(Exception ex) {
                    PrintErrorToOutput(ex.Message);
                    CancelClient(false);
                }
            }
            else {
                await client.CloseAsync();
            }
        }

        private void ReceiveClientClosed()
            => CancelClient();

        private void ReceiveMessage(DetActMessage message)
        {
            switch(message.Type) {
                case MessageType.BLACKBOARD:
                    break;

                case MessageType.CONTROL:
                    HandleControlMessage(message);
                    break;

                case MessageType.BEHAVIOUR:
                    HandleBehaviourMessage(message);
                    break;

                case MessageType.ERROR:
                    PrintErrorToOutput(message.ToString());
                    break;

                default:
                    PrintToConsole(message.ToString());
                    break;
            }
        }

        private void HandleControlMessage(DetActMessage message)
        {
            var content = message.Content as Control;

            if(content.Name == "RootResult") {
                if(content.Items.ContainsKey("status")) {
                    PrintToConsole($"Tree finished tick with: {content.Items["status"]}");
                    SetButtonsEnabled(true);
                }

                return;
            }

            if(content.Name == "TreeState") {
                if(content.Items.ContainsKey("running")) {
                    PrintToConsole($"Tree changed running-state to: {content.Items["running"]}");

                    if(bool.TryParse(content.Items["running"], out bool running))
                        SetButtonsEnabled(running);
                }

                return;
            }

            PrintErrorToOutput($"Got not supported control-command '{content.Name}'.");
        }

        private async void HandleBehaviourMessage(DetActMessage message)
        {
            var atomicActionType = typeof(AtomicAction);

            var behaviour = message.Content as Behaviour;
            var result = BehaviourStatus.FAILURE;

            foreach(var command in behaviour.Commands) {
                if(string.IsNullOrWhiteSpace(command.Key) || command.Key.ToLower() == "null") {
                    result = BehaviourStatus.FAILURE;
                    break;
                }

                try {
                    var method = atomicActionType.GetMethod(command.Key);
                    result = (BehaviourStatus)method?.Invoke(action, command.Value.ToArray());
                }
                catch(Exception) {
                    result = BehaviourStatus.FAILURE;
                }

                if(result is BehaviourStatus.FAILURE)
                    break;
            }

            var isChecked = Dispatcher.Invoke(callback: () => withDelayCheckBox.IsChecked.Value);

            if(isChecked && behaviour.Type == BehaviourType.ACTION)
                await Task.Delay(Convert.ToInt32(delaySlider.Value));

            behaviour.Status = result;

            client?.Send(message);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var content = new Blackboard(name: "Default", new() { { "Item", String.IsNullOrWhiteSpace(inputTextBox.Text) ? "null" : inputTextBox.Text } });
            client?.Send(new DetActMessage(content));
        }

        private void RunTree(string fileName)
        {
            if(string.IsNullOrWhiteSpace(fileName))
                return;


            var run = new Control(name: "BT-Control", new() { { "load", fileName } });
            client?.Send(new DetActMessage(run));
        }

        private void SendTick(bool shouldPush)
        {
            SetButtonsEnabled(false);

            var push = new Blackboard(name: "Default", new() { { "Push", shouldPush.ToString().ToLower() } });
            var tick = new Control(name: "BT-Control", new() { { "tick", "true" } });

            client?.Send(new DetActMessage(push));
            client?.Send(new DetActMessage(tick));
        }

        private void PushButton_Click(object sender, RoutedEventArgs e)
            => SendTick(shouldPush: true);

        private void PopButton_Click(object sender, RoutedEventArgs e)
            => SendTick(shouldPush: false);

        private void CancelClient(bool printClosed = true)
        {
            client?.Dispose();
            client = null;

            if(printClosed)
                PrintToConsole(text: "closed");

            connectButton.Content = "Open";
            connectButton.IsEnabled = true;

            SetButtonsEnabled(false);
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(client is not null) {
                await client.CloseAsync();
                CancelClient();
            }
        }

        private void WithNullCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = Dispatcher.Invoke(callback: () => withNullCheckBox.IsChecked.Value);
            action.AcceptNull = isChecked;
        }
    }
}