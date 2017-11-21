using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PortTerm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool isConnected = false;
        bool isServer = false;
        static int bufferSize = 1024;
        byte[] inBuffer = new byte[bufferSize];
        private string _ipAddress;
        private int _port;
        private string _ackString;
        private CancellationTokenSource _cancelServerTokenSource;
        private CancellationToken _cancelServerToken;
        private bool _ack;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                if (isServer)
                {
                    ShutdownServer();
                }
                else
                {
                    ShutdownClient();
                }
            }
            else
            {
                if (rbHost.IsChecked.Value == true)
                {
                    isServer = true;
                    StartServer();
                }
                else
                {
                    isServer = false;
                    ConnectClient();
                }
            }
            btnConnect.Content = (isConnected) ? "Disconnect" : "Connect";            
        }

        private void ConnectClient()
        {
        }

        private async void StartServer()
        {
            _cancelServerTokenSource = new CancellationTokenSource();
            _cancelServerToken = _cancelServerTokenSource.Token;
            if (rbTcp.IsChecked.Value)
            {
                await Task.Factory.StartNew(() => {
                    StartTCPServer();
                }, _cancelServerToken);
            }
            else
            {
                StartUDPServer();
            }
        }

        private void StartUDPServer()
        {
            if (_port == 0)
            {
                MessageBox.Show("Port not set");
                isConnected = false;
                return;
            }
            isConnected = true;
        }

        private async void StartTCPServer()
        {
            if (_port == 0)
            {
                MessageBox.Show("Port not set");
                isConnected = false;
                return;
            }
            TcpListener server = null;
            try
            {
                // Set the TcpListener on port 13000.
                IPAddress localAddr = IPAddress.Parse(_ipAddress);

                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, _port);

                // Start listening for client requests.
                server.Start();
                Dispatcher.Invoke(() => {
                    isConnected = true;
                    btnConnect.Content = "Disconnect";
                });
                String data = null;

                // Enter the listening loop.
                while (true)
                {
                    ShowMessage("Waiting for a connection... ");

                    // await call to accept requests.
                    var client = await Task.Run(
                        () => server.AcceptTcpClientAsync(),
                            _cancelServerToken);

                    ShowMessage("Connected!");

                    data = null;

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();
                    stream.Read(inBuffer, 0, inBuffer.Length);
                    data = Encoding.ASCII.GetString(inBuffer);
                    if (data != null)
                    {
                        int returnidx = data.IndexOf('\r');
                        if (returnidx > 0)
                        {
                            data = data.Substring(0, returnidx);
                        }
                        else
                        {
                            int nullidx = data.IndexOf('\0');
                            if (nullidx > 0)
                            {
                                data = data.Substring(0, nullidx);
                            }
                        }
                        SendToTerminal(data);
                    }

                    if (_cancelServerToken.IsCancellationRequested)
                    {
                        client.Close();
                        server.Stop();
                        ShowMessage("Server stopped.");
                        Dispatcher.Invoke(() => {
                            btnConnect.Content = "Connect";
                            isConnected = false;
                        });
                        break;
                    }

                    if (_ack)
                    {
                        if (string.IsNullOrEmpty(_ackString))
                        {
                            _ackString = "ACK";
                        }

                        // Send ACK
                        byte[] msg = System.Text.Encoding.ASCII.GetBytes(_ackString);
                        // Send back a response.
                        stream.Write(msg, 0, msg.Length);
                    }
                    // Shutdown and end connection
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                ShowMessage($"SocketException: {e}");
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }

        }
    
        private void ShowMessage(string msg)
        {
            Dispatcher.Invoke(() => {
                txtMessage.AppendText(msg);
                txtMessage.AppendText("\n");
                txtMessage.ScrollToEnd();
            });
        }

        private void SendToTerminal(string msg)
        {
            Dispatcher.Invoke(() => {
                txtTerminal.AppendText(msg);
                txtTerminal.AppendText("\n");
                txtTerminal.ScrollToEnd();
            });
        }

        private void ShutdownClient()
        {
        }

        private void ShutdownServer()
        {
            if (rbTcp.IsChecked.Value)
                _cancelServerTokenSource.Cancel();
            else
                isConnected = false;
        }

        private void txtIP_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            _ipAddress = tb.Text;
        }

        private void txtPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            int port = 0;
            if (!int.TryParse(tb.Text,out port))
            {
                _port = 0;
            }
            else
            {
                _port = port;
            }
        }

        private void txtACK_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            _ackString = tb.Text;
        }

        private void chkSendACK_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            _ack = cb.IsChecked.Value;
        }
    }
}
