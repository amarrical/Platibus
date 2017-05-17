﻿using System;
using System.IO;
using Platibus.SQLite;

namespace Platibus.UnitTests.SQLite
{
    public class SQLiteFixture : IDisposable
    {
        private readonly DirectoryInfo _baseDirectory;
        private readonly SQLiteMessageJournalingService _messageJournalingService;
        private readonly SQLiteMessageQueueingService _messageQueueingService;
        private readonly SQLiteSubscriptionTrackingService _subscriptionTrackingService;

        private readonly SQLiteMessageJournal _messageJournal;

        private bool _disposed;

        public DirectoryInfo BaseDirectory
        {
            get { return _baseDirectory; }
        }

        public SQLiteMessageJournalingService MessageJournalingService
        {
            get { return _messageJournalingService; }
        }

        public SQLiteMessageQueueingService MessageQueueingService
        {
            get { return _messageQueueingService; }
        }

        public SQLiteSubscriptionTrackingService SubscriptionTrackingService
        {
            get { return _subscriptionTrackingService; }
        }

        public SQLiteMessageJournal MessageJournal
        {
            get { return _messageJournal; }
        }

        public SQLiteFixture()
        {
            _baseDirectory = GetTempDirectory();

            _messageJournalingService = new SQLiteMessageJournalingService(_baseDirectory);
            _messageJournalingService.Init();

            _messageQueueingService = new SQLiteMessageQueueingService(_baseDirectory);
            _messageQueueingService.Init();

            _subscriptionTrackingService = new SQLiteSubscriptionTrackingService(_baseDirectory);
            _subscriptionTrackingService.Init();

            _messageJournal = new SQLiteMessageJournal(_baseDirectory);
            _messageJournal.Init();
        }

        protected DirectoryInfo GetTempDirectory()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "Platibus.UnitTests", DateTime.Now.ToString("yyyyMMddHHmmss"));
            var tempDir = new DirectoryInfo(tempPath);
            if (!tempDir.Exists)
            {
                tempDir.Create();
            }
            return tempDir;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Dispose(true);
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_subscriptionTrackingService")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_messageQueueingService")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_messageJournalingService")]
        protected virtual void Dispose(bool disposing)
        {
            _messageQueueingService.TryDispose();
            _subscriptionTrackingService.TryDispose();
            _messageJournalingService.TryDispose();
            _messageJournal.TryDispose();
        }
    }
}
