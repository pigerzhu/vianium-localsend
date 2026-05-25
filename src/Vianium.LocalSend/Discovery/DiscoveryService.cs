// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Vianium.LocalSend.Json;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Protocol;

namespace Vianium.LocalSend.Discovery
{
    internal sealed class DiscoveryService : IDisposable
    {
        private const string TAG = "Discovery";
        private DatagramSocket _socket;
        private DeviceInfo _localDevice;
        private CancellationTokenSource _announceCts;
        private bool _multicastFailed;
        private bool _running;
        private bool _networkStatusSubscribed;

        public DeviceTracker Tracker { get; private set; }

        public DiscoveryService(DeviceInfo localDevice)
        {
            _localDevice = localDevice;
            Tracker = new DeviceTracker();
        }

        public async Task StartAsync()
        {
            if (_running) return;
            _running = true;

            EnsureNetworkStatusSubscribed();

            try
            {
                _socket = new DatagramSocket();
                _socket.MessageReceived += OnMessageReceived;

                await _socket.BindServiceNameAsync(ProtocolConstants.Port.ToString());
                _socket.JoinMulticastGroup(new HostName(ProtocolConstants.MulticastAddress));

                AppLog.I(TAG, "Discovery started on port " + ProtocolConstants.Port);
            }
            catch (Exception ex)
            {
                AppLog.I(TAG, "Multicast bind failed: " + ex.Message + " — will use HTTP scan only");
                _multicastFailed = true;

                // Dispose failed socket
                SafeDisposeSocket();
            }

            _announceCts = new CancellationTokenSource();
            var ignore = StartPeriodicDiscoveryAsync(_announceCts.Token);
        }

        public void Stop()
        {
            if (!_running && _socket == null && _announceCts == null) return;
            _running = false;

            SafeUnsubscribeNetworkStatus();

            if (_announceCts != null)
            {
                try { _announceCts.Cancel(); } catch { }
                try { _announceCts.Dispose(); } catch { }
                _announceCts = null;
            }

            SafeDisposeSocket();

            AppLog.I(TAG, "Discovery stopped");
        }

        private void EnsureNetworkStatusSubscribed()
        {
            if (_networkStatusSubscribed) return;

            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
            _networkStatusSubscribed = true;
        }

        private void SafeUnsubscribeNetworkStatus()
        {
            if (!_networkStatusSubscribed) return;

            try
            {
                NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Ignoring network event detach error: " + ex.Message);
            }
            _networkStatusSubscribed = false;
        }

        private void SafeDisposeSocket()
        {
            var socket = _socket;
            _socket = null;
            if (socket == null) return;

            try
            {
                socket.MessageReceived -= OnMessageReceived;
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Ignoring UDP event detach error: " + ex.Message);
            }

            try
            {
                socket.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Ignoring UDP dispose error: " + ex.Message);
            }
        }

        public async Task SendAnnouncementAsync()
        {
            if (_socket == null) return;

            try
            {
                _localDevice.Announce = true;
                string json = SimpleJsonParser.Serialize(_localDevice.ToMulticastJson());
                byte[] data = Encoding.UTF8.GetBytes(json);

                var outputStream = await _socket.GetOutputStreamAsync(
                    new HostName(ProtocolConstants.MulticastAddress),
                    ProtocolConstants.Port.ToString());

                using (var writer = new DataWriter(outputStream))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                }
                AppLog.V(TAG, "Multicast sent (" + data.Length + " bytes)");
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Multicast send failed: " + ex.Message);
                _multicastFailed = true;
            }
        }

        private async Task SendResponseAsync(string targetIp)
        {
            if (_socket == null) return;

            try
            {
                _localDevice.Announce = false;
                string json = SimpleJsonParser.Serialize(_localDevice.ToMulticastJson());
                byte[] data = Encoding.UTF8.GetBytes(json);

                var outputStream = await _socket.GetOutputStreamAsync(
                    new HostName(targetIp),
                    ProtocolConstants.Port.ToString());

                using (var writer = new DataWriter(outputStream))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                AppLog.I(TAG, "Failed to send response to " + targetIp + ": " + ex.Message);
            }
        }

        private async Task StartPeriodicDiscoveryAsync(CancellationToken ct)
        {
            // Send first multicast announcement immediately
            if (!_multicastFailed)
            {
                AppLog.I(TAG, "Sending initial multicast announcement");
                await SendAnnouncementAsync();
            }

            // NO initial HTTP scan — it freezes the phone for 20+ seconds.
            // Devices are found via:
            // 1. Multicast (every 5s)
            // 2. Reverse discovery (when they connect to us)
            // 3. Manual refresh button (user triggers scan)

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, ct);
                    if (ct.IsCancellationRequested) break;

                    if (!_multicastFailed)
                        await SendAnnouncementAsync();

                    Tracker.RemoveExpired(TimeSpan.FromSeconds(45));
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLog.W(TAG, "Discovery cycle error: " + ex.Message);
                }
            }
        }

        #region HTTP Scan Fallback

        /// <summary>
        /// Public entry point for manual scan (refresh button).
        /// </summary>
        public async Task RunHttpScanAsync()
        {
            await HttpScanAsync(CancellationToken.None);
        }

        /// <summary>
        /// Lightweight subnet scan: only common IPs (1, 2, 100-110, ±15 around our IP),
        /// 3 concurrent, sequential to avoid flooding the device.
        /// </summary>
        private async Task HttpScanAsync(CancellationToken ct)
        {
            string localIp = GetLocalIpAddress();
            if (localIp == "Unknown") return;

            int lastDot = localIp.LastIndexOf('.');
            if (lastDot < 0) return;
            string subnet = localIp.Substring(0, lastDot + 1);

            int localOctet;
            if (!int.TryParse(localIp.Substring(lastDot + 1), out localOctet))
                return;

            AppLog.I(TAG, "HTTP scan on " + subnet + "x (light)");

            // Build a small set of candidate IPs to scan
            var candidates = new HashSet<int>();

            // Common gateway/server IPs
            candidates.Add(1);
            candidates.Add(2);

            // Common DHCP range
            for (int i = 100; i <= 115; i++)
                candidates.Add(i);

            // IPs near our own (likely same DHCP range)
            for (int i = Math.Max(1, localOctet - 15); i <= Math.Min(254, localOctet + 15); i++)
                candidates.Add(i);

            candidates.Remove(localOctet); // skip self

            AppLog.V(TAG, "Scan candidates: " + candidates.Count + " IPs (local=" + localOctet + ")");

            // Scan sequentially in batches of 3 to avoid flooding
            var batch = new List<Task>();
            foreach (int octet in candidates)
            {
                if (ct.IsCancellationRequested) break;

                batch.Add(ScanSingleHostAsync(subnet + octet));

                if (batch.Count >= 3)
                {
                    await Task.WhenAll(batch);
                    batch.Clear();

                    // Yield to UI thread between batches
                    await Task.Delay(10);
                }
            }

            if (batch.Count > 0)
                await Task.WhenAll(batch);
        }

        /// <summary>
        /// Try to connect to a single host. Tries HTTPS first (most LocalSend devices),
        /// then falls back to HTTP. Uses socket-level timeout to avoid exceptions.
        /// </summary>
        private async Task ScanSingleHostAsync(string host)
        {
            // Try HTTPS first (most modern LocalSend clients use HTTPS)
            var info = await TryScanWithProtocolAsync(host, true);
            if (info == null)
            {
                // Fallback to plain HTTP
                info = await TryScanWithProtocolAsync(host, false);
            }

            if (info != null && info.Fingerprint != _localDevice.Fingerprint)
            {
                AppLog.I(TAG, "Scan found: " + info.Alias + " at " + host +
                    " proto=" + info.Protocol);
                Tracker.AddOrUpdate(info, host);
            }
        }

        private async Task<DeviceInfo> TryScanWithProtocolAsync(string host, bool useSsl)
        {
            try
            {
                using (var socket = new StreamSocket())
                {
                    var protection = useSsl
                        ? SocketProtectionLevel.Tls12
                        : SocketProtectionLevel.PlainSocket;

                    if (useSsl)
                    {
                        socket.Control.IgnorableServerCertificateErrors.Add(
                            Windows.Security.Cryptography.Certificates.ChainValidationResult.Untrusted);
                        socket.Control.IgnorableServerCertificateErrors.Add(
                            Windows.Security.Cryptography.Certificates.ChainValidationResult.InvalidName);
                        socket.Control.IgnorableServerCertificateErrors.Add(
                            Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                    }

                    var connectTask = socket.ConnectAsync(
                        new HostName(host),
                        ProtocolConstants.Port.ToString(),
                        protection).AsTask();

                    if (await Task.WhenAny(connectTask, Task.Delay(600)) != connectTask)
                        return null;

                    // Ensure connect didn't throw
                    await connectTask;

                    using (var writer = new DataWriter(socket.OutputStream))
                    using (var reader = new DataReader(socket.InputStream))
                    {
                        reader.InputStreamOptions = InputStreamOptions.Partial;

                        string path = ProtocolConstants.ApiPrefix + "/info?fingerprint=" +
                                      Uri.EscapeDataString(_localDevice.Fingerprint);

                        string req = "GET " + path + " HTTP/1.1\r\n" +
                                     "Host: " + host + ":" + ProtocolConstants.Port + "\r\n" +
                                     "Connection: close\r\n\r\n";

                        writer.WriteBytes(Encoding.UTF8.GetBytes(req));
                        await writer.StoreAsync();
                        await writer.FlushAsync();

                        var responseBytes = new List<byte>();
                        var readTask = reader.LoadAsync(4096).AsTask();
                        if (await Task.WhenAny(readTask, Task.Delay(600)) == readTask)
                        {
                            uint loaded = readTask.Result;
                            for (uint j = 0; j < loaded; j++)
                                responseBytes.Add(reader.ReadByte());
                        }

                        if (responseBytes.Count == 0) return null;

                        string raw = Encoding.UTF8.GetString(responseBytes.ToArray(), 0, responseBytes.Count);

                        int bodyStart = raw.IndexOf("\r\n\r\n");
                        if (bodyStart < 0) return null;
                        string jsonBody = raw.Substring(bodyStart + 4);

                        if (!raw.StartsWith("HTTP/1.1 200") && !raw.StartsWith("HTTP/1.0 200"))
                            return null;

                        JsonValue json;
                        if (SimpleJsonParser.TryParse(jsonBody, out json))
                        {
                            return DeviceInfo.FromJson(json);
                        }
                    }
                }
            }
            catch
            {
                // Not responding on this protocol
            }
            return null;
        }

        #endregion

        #region Network Change Detection

        private async void OnNetworkStatusChanged(object sender)
        {
            if (!_running) return;

            AppLog.I(TAG, "Network status changed — restarting discovery");

            try
            {
                // Restart socket on new network
                SafeDisposeSocket();

                _multicastFailed = false;

                try
                {
                    _socket = new DatagramSocket();
                    _socket.MessageReceived += OnMessageReceived;
                    await _socket.BindServiceNameAsync(ProtocolConstants.Port.ToString());
                    _socket.JoinMulticastGroup(new HostName(ProtocolConstants.MulticastAddress));
                    AppLog.I(TAG, "Multicast re-joined after network change");
                }
                catch (Exception ex)
                {
                    AppLog.I(TAG, "Multicast re-join failed: " + ex.Message);
                    _multicastFailed = true;
                    SafeDisposeSocket();
                }

                // Clear stale devices from previous network
                Tracker.RemoveExpired(TimeSpan.Zero);

                // Immediate announcement on new network
                if (!_multicastFailed)
                    await SendAnnouncementAsync();
            }
            catch (Exception ex)
            {
                AppLog.I(TAG, "Network change handler error: " + ex.Message);
            }
        }

        #endregion

        #region Multicast Receive

        private void OnMessageReceived(DatagramSocket sender,
            DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                string remoteAddress = args.RemoteAddress.DisplayName;

                using (var reader = args.GetDataReader())
                {
                    uint length = reader.UnconsumedBufferLength;
                    if (length == 0)
                    {
                        AppLog.V(TAG, "UDP: empty message from " + remoteAddress);
                        return;
                    }

                    byte[] data = new byte[length];
                    reader.ReadBytes(data);

                    string json = Encoding.UTF8.GetString(data, 0, data.Length);
                    AppLog.V(TAG, "UDP received (" + length + "B) from " + remoteAddress +
                        ": " + (json.Length > 120 ? json.Substring(0, 120) + "..." : json));
                    ProcessMessage(json, remoteAddress);
                }
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "UDP receive error: " + ex.Message);
            }
        }

        private async void ProcessMessage(string json, string remoteAddress)
        {
            JsonValue value;
            if (!SimpleJsonParser.TryParse(json, out value))
            {
                AppLog.I(TAG, "Invalid JSON from " + remoteAddress);
                return;
            }

            var deviceInfo = DeviceInfo.FromJson(value);
            if (deviceInfo == null)
            {
                AppLog.I(TAG, "Invalid device info from " + remoteAddress);
                return;
            }

            if (deviceInfo.Fingerprint == _localDevice.Fingerprint)
                return;

            AppLog.I(TAG, "Discovered: " + deviceInfo.Alias + " at " + remoteAddress);

            Tracker.AddOrUpdate(deviceInfo, remoteAddress);

            if (deviceInfo.Announce)
            {
                await SendResponseAsync(remoteAddress);
            }
        }

        #endregion

        public static string GetLocalIpAddress()
        {
            try
            {
                string loopback = null;
                var hostNames = NetworkInformation.GetHostNames();
                foreach (var host in hostNames)
                {
                    if (host.Type == HostNameType.Ipv4 &&
                        host.IPInformation != null &&
                        host.IPInformation.NetworkAdapter != null)
                    {
                        string ip = host.DisplayName;
                        if (ip != "127.0.0.1" && !ip.StartsWith("169.254."))
                            return ip;
                        if (ip == "127.0.0.1")
                            loopback = ip;
                    }
                }

                if (loopback != null)
                    return loopback;
            }
            catch { }
            return "Unknown";
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
