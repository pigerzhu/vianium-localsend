// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using Vianium.LocalSend.Logging;

namespace Vianium.LocalSend.Protocol
{
    internal enum TransferState
    {
        Pending,
        Accepted,
        Transferring,
        Completed,
        Cancelled,
        Failed
    }

    internal enum FileTransferStatus
    {
        Queued,
        Skipped,
        Transferring,
        Completed,
        Failed
    }

    internal sealed class FileTransferInfo
    {
        private const string TAG = "Transfer";
        private FileTransferStatus _status;

        public FileDto File { get; set; }
        public string Token { get; set; }
        public long BytesTransferred { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedPath { get; set; }

        /// <summary>
        /// Per-file transfer status. Assigning a new value logs the
        /// transition at Debug level so the file state machine is
        /// traceable in the on-device log.
        /// </summary>
        public FileTransferStatus Status
        {
            get { return _status; }
            set
            {
                if (_status == value) return;
                FileTransferStatus prev = _status;
                _status = value;
                string name = File != null ? File.FileName : "(file)";
                AppLog.D(TAG, "File '" + name + "' " + prev + " -> " + value);
            }
        }

        public FileTransferInfo(FileDto file, string token)
        {
            File = file;
            Token = token;
            Status = FileTransferStatus.Queued;
        }
    }

    internal sealed class TransferSession
    {
        private const string TAG = "Session";
        private TransferState _state;

        public string SessionId { get; set; }
        public DeviceInfo RemoteDevice { get; set; }
        public Dictionary<string, FileTransferInfo> Files { get; private set; }
        public DateTime CreatedAt { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Session-level state. Assigning a new value logs the transition
        /// at Info level — this is the spine of the transfer state machine.
        /// </summary>
        public TransferState State
        {
            get { return _state; }
            set
            {
                if (_state == value) return;
                TransferState prev = _state;
                _state = value;
                AppLog.I(TAG, "Session " + ShortId + " " + prev + " -> " + value +
                    " (" + CompletedFiles + "/" + TotalFiles + " files)");
            }
        }

        /// <summary>First 8 chars of <see cref="SessionId"/> for compact log lines.</summary>
        public string ShortId
        {
            get
            {
                if (string.IsNullOrEmpty(SessionId)) return "?";
                return SessionId.Length > 8 ? SessionId.Substring(0, 8) : SessionId;
            }
        }

        public TransferSession()
        {
            SessionId = TokenGenerator.GenerateSessionId();
            Files = new Dictionary<string, FileTransferInfo>();
            State = TransferState.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        public long TotalBytes
        {
            get
            {
                long total = 0;
                foreach (var f in Files.Values)
                    total += f.File.Size;
                return total;
            }
        }

        public long TransferredBytes
        {
            get
            {
                long total = 0;
                foreach (var f in Files.Values)
                    total += f.BytesTransferred;
                return total;
            }
        }

        public int TotalFiles { get { return Files.Count; } }

        public int CompletedFiles
        {
            get
            {
                int count = 0;
                foreach (var f in Files.Values)
                {
                    if (f.Status == FileTransferStatus.Completed)
                        count++;
                }
                return count;
            }
        }

        public bool AllFilesCompleted
        {
            get
            {
                foreach (var f in Files.Values)
                {
                    if (f.Status != FileTransferStatus.Completed && f.Status != FileTransferStatus.Skipped)
                        return false;
                }
                return Files.Count > 0;
            }
        }

        public bool HasErrors
        {
            get
            {
                foreach (var f in Files.Values)
                {
                    if (f.Status == FileTransferStatus.Failed)
                        return true;
                }
                return false;
            }
        }
    }
}
