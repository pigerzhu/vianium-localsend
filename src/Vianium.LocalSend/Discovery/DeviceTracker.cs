// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Linq;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Protocol;

namespace Vianium.LocalSend.Discovery
{
    internal sealed class DiscoveredDevice
    {
        public DeviceInfo Info { get; set; }
        public string IpAddress { get; set; }
        public DateTime LastSeen { get; set; }

        // UI binding properties
        public string Alias { get { return Info != null ? Info.Alias : "Unknown"; } }
        public string DeviceModel { get { return Info != null ? (Info.DeviceModel ?? Info.DeviceType) : ""; } }

        public string ProtocolLabel
        {
            get
            {
                if (Info == null) return "HTTP";
                return Info.Protocol == "https" ? "HTTPS" : "HTTP";
            }
        }

        public string DeviceIcon
        {
            get
            {
                if (Info == null) return "\uE11A"; // wifi
                switch (Info.DeviceType)
                {
                    case "mobile": return "\uE1C9";   // phone
                    case "desktop": return "\uE211";   // monitor
                    case "web": return "\uE128";       // globe
                    case "server": return "\uE1A0";    // server-like
                    case "headless": return "\uE1A0";
                    default: return "\uE11A";          // wifi
                }
            }
        }
    }

    internal sealed class DeviceTracker
    {
        private const string TAG = "Tracker";
        private readonly Dictionary<string, DiscoveredDevice> _devices;
        private readonly object _lock = new object();

        public event Action DevicesChanged;

        public DeviceTracker()
        {
            _devices = new Dictionary<string, DiscoveredDevice>();
        }

        public void AddOrUpdate(DeviceInfo info, string ipAddress)
        {
            if (info == null || string.IsNullOrEmpty(info.Fingerprint))
            {
                AppLog.V(TAG, "AddOrUpdate ignored — null info or empty fingerprint");
                return;
            }

            bool isNew = false;
            bool ipChanged = false;
            string oldIp = null;
            int knownCount;
            lock (_lock)
            {
                DiscoveredDevice existing;
                if (_devices.TryGetValue(info.Fingerprint, out existing))
                {
                    oldIp = existing.IpAddress;
                    ipChanged = oldIp != ipAddress;
                    existing.Info = info;
                    existing.IpAddress = ipAddress;
                    existing.LastSeen = DateTime.UtcNow;
                }
                else
                {
                    _devices[info.Fingerprint] = new DiscoveredDevice
                    {
                        Info = info,
                        IpAddress = ipAddress,
                        LastSeen = DateTime.UtcNow
                    };
                    isNew = true;
                }
                knownCount = _devices.Count;
            }

            if (isNew)
            {
                AppLog.I(TAG, "Device added: " + info.Alias + " (" + info.DeviceModel +
                    ") at " + ipAddress + " — " + knownCount + " device(s) known");
                OnDevicesChanged();
            }
            else if (ipChanged)
            {
                AppLog.I(TAG, "Device IP changed: " + info.Alias + " " + oldIp +
                    " -> " + ipAddress);
            }
            else
            {
                AppLog.V(TAG, "Device refreshed: " + info.Alias + " at " + ipAddress);
            }
        }

        public void RemoveExpired(TimeSpan timeout)
        {
            bool changed = false;
            int removed = 0;
            int remaining = 0;
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var expired = new List<string>();

                foreach (var kvp in _devices)
                {
                    if (now - kvp.Value.LastSeen > timeout)
                        expired.Add(kvp.Key);
                }

                for (int i = 0; i < expired.Count; i++)
                {
                    _devices.Remove(expired[i]);
                    changed = true;
                }
                removed = expired.Count;
                remaining = _devices.Count;
            }

            if (changed)
            {
                AppLog.I(TAG, "Pruned " + removed + " expired device(s) — " +
                    remaining + " remaining");
                OnDevicesChanged();
            }
        }

        public List<DiscoveredDevice> GetAll()
        {
            lock (_lock)
            {
                return _devices.Values.ToList();
            }
        }

        public DiscoveredDevice GetByFingerprint(string fingerprint)
        {
            lock (_lock)
            {
                DiscoveredDevice device;
                _devices.TryGetValue(fingerprint, out device);
                return device;
            }
        }

        public int Count
        {
            get { lock (_lock) { return _devices.Count; } }
        }

        private void OnDevicesChanged()
        {
            var handler = DevicesChanged;
            if (handler != null)
                handler();
        }
    }
}
