﻿// The MIT License (MIT)
// 
// Copyright (c) 2017 Jesse Sweetland
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Common.Logging;

namespace Platibus.Queueing
{
    /// <summary>
    /// An abstract base class for implementing message queues
    /// </summary>
    public abstract class AbstractMessageQueue : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(LoggingCategories.Queueing);

        /// <summary>
        /// The name of the queue
        /// </summary>
        protected readonly QueueName QueueName;

        private readonly IQueueListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly bool _autoAcknowledge;
        private readonly int _maxAttempts;
        private readonly ActionBlock<QueuedMessage> _queuedMessages;
        private readonly TimeSpan _retryDelay;

        private bool _disposed;
        private int _initialized;

        /// <summary>
        /// Initializes a new <see cref="AbstractMessageQueue"/> with the specified values
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="listener">The object that will be notified when messages are
        ///     added to the queue</param>
        /// <param name="options">(Optional) Settings that influence how the queue behaves</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queueName"/>, or 
        /// <paramref name="listener"/> are <c>null</c></exception>
        protected AbstractMessageQueue(QueueName queueName, IQueueListener listener, QueueOptions options = null)
        {
            if (queueName == null) throw new ArgumentNullException("queueName");
            if (listener == null) throw new ArgumentNullException("listener");

            QueueName = queueName;
            _listener = listener;

            var myOptions = options ?? new QueueOptions();

            _autoAcknowledge = myOptions.AutoAcknowledge;
            _maxAttempts = myOptions.MaxAttempts;
            _retryDelay = myOptions.RetryDelay;

            var concurrencyLimit = myOptions.ConcurrencyLimit;
            _cancellationTokenSource = new CancellationTokenSource();
            _queuedMessages = new ActionBlock<QueuedMessage>(
                async msg => await ProcessQueuedMessage(msg, _cancellationTokenSource.Token),
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = _cancellationTokenSource.Token,
                    MaxDegreeOfParallelism = concurrencyLimit
                });
        }

        /// <summary>
        /// Reads previously queued messages from the database and initiates message processing
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by the caller
        /// to cancel initialization</param>
        /// <returns>Returns a task that completes when initialization is complete</returns>
        public virtual async Task Init(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 0)
            {
                await EnqueueExistingMessages(cancellationToken);
            }
        }

        /// <summary>
        /// Read existing messages from the persistent store and 
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by the caller
        /// to cancel the enqueueing operation</param>
        /// <returns></returns>
        protected async Task EnqueueExistingMessages(CancellationToken cancellationToken = default(CancellationToken))
        {
            var pendingMessages = await SelectPendingMessages(cancellationToken);
            foreach (var pendingMessage in pendingMessages)
            {
                Log.DebugFormat("Enqueueing existing message ID {0}...", pendingMessage.Message.Headers.MessageId);
                await _queuedMessages.SendAsync(pendingMessage, cancellationToken);
            }
        }

        /// <summary>
        /// Selects pending messages from the persistence store
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by the caller
        /// to cancel the fetch operation</param>
        /// <returns>Returns a task whose result is the set of pending messages from the
        /// persistent store</returns>
        protected abstract Task<IEnumerable<QueuedMessage>> SelectPendingMessages(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Inserts a message into the persistent store
        /// </summary>
        /// <param name="message">The message to enqueue</param>
        /// <param name="principal">The principal that sent the message or from whom the
        ///     message was received</param>
        /// <param name="cancellationToken">A cancellation token that can be used by the caller
        /// to cancel the message insertion</param>
        /// <returns>Returns a task that completes when the message has been inserted into the
        /// persistent store and whose result is a copy of the inserted record</returns>
        protected abstract Task<QueuedMessage> InsertQueuedMessage(Message message, IPrincipal principal, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Updates an existing queued message in the persistent store database
        /// </summary>
        /// <param name="queuedMessage">The queued message to update</param>
        /// <param name="acknowledged">The date and time the message was acknowledged, if applicable</param>
        /// <param name="abandoned">The date and time the message was abandoned, if applicable</param>
        /// <param name="attempts">The number of attempts to process the message so far</param>
        /// <param name="cancellationToken">A cancellation token that can be used by the caller
        /// to cancel the message update</param>
        /// <returns>Returns a task that completes when the record has been updated</returns>
        protected abstract Task UpdateQueuedMessage(QueuedMessage queuedMessage, DateTime? acknowledged, DateTime? abandoned, int attempts, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Adds a message to the queue
        /// </summary>
        /// <param name="message">The message to add</param>
        /// <param name="senderPrincipal">The principal that sent the message or from whom
        ///     the message was received</param>
        /// <param name="cancellationToken">A cancellation token that can be used by the caller
        /// to cancel the enqueueing operation</param>
        /// <returns>Returns a task that completes when the message has been added to the queue</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is
        /// <c>null</c></exception>
        /// <exception cref="ObjectDisposedException">Thrown if this SQL message queue instance
        /// has already been disposed</exception>
        public virtual async Task Enqueue(Message message, IPrincipal senderPrincipal, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (message == null) throw new ArgumentNullException("message");
            CheckDisposed();

            var queuedMessage = await InsertQueuedMessage(message, senderPrincipal, cancellationToken);

            await _queuedMessages.SendAsync(queuedMessage, cancellationToken);
            // TODO: handle accepted == false
        }

        /// <summary>
        /// Called by the message processing loop to process an individual message
        /// </summary>
        /// <param name="queuedMessage">The queued message to process</param>
        /// <param name="cancellationToken">A cancellation token that can be used by the caller
        /// to cancel message processing operation</param>
        /// <returns>Returns a task that completes when the queued message is processed</returns>
        protected virtual async Task ProcessQueuedMessage(QueuedMessage queuedMessage, CancellationToken cancellationToken)
        {
            var messageId = queuedMessage.Message.Headers.MessageId;
            var attemptCount = queuedMessage.Attempts;
            var abandoned = false;
            var message = queuedMessage.Message;
            var principal = queuedMessage.Principal;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            while (!abandoned && attemptCount < _maxAttempts)
            {
                attemptCount++;

                Log.DebugFormat("Processing queued message {0} (attempt {1} of {2})...", messageId, attemptCount,
                    _maxAttempts);

                var context = new QueuedMessageContext(message, principal);
                Thread.CurrentPrincipal = context.Principal;
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _listener.MessageReceived(message, context, cancellationToken);
                    if (_autoAcknowledge && !context.Acknowledged)
                    {
                        await context.Acknowledge();
                    }
                }
                catch (Exception ex)
                {
                    Log.WarnFormat("Unhandled exception handling queued message {0}", ex, messageId);
                }

                if (context.Acknowledged)
                {
                    Log.DebugFormat("Message acknowledged.  Marking message {0} as acknowledged...", messageId);
                    await UpdateQueuedMessage(queuedMessage, DateTime.UtcNow, null, attemptCount, cancellationToken);
                    Log.DebugFormat("Message {0} acknowledged successfully", messageId);
                    return;
                }

                if (attemptCount >= _maxAttempts)
                {
                    Log.WarnFormat("Maximum attempts to process message {0} exceeded", messageId);
                    abandoned = true;
                }

                if (abandoned)
                {
                    await UpdateQueuedMessage(queuedMessage, null, DateTime.UtcNow, attemptCount, cancellationToken);
                    return;
                }

                await UpdateQueuedMessage(queuedMessage, null, null, attemptCount, cancellationToken);

                Log.DebugFormat("Message not acknowledged.  Retrying in {0}...", _retryDelay);
                await Task.Delay(_retryDelay, cancellationToken);
            }
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if this object has been disposed
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if this object has been disposed</exception>
        protected void CheckDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// Finalizer to ensure that all resources are released
        /// </summary>
        ~AbstractMessageQueue()
        {
            Dispose(false);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (_disposed) return;
            Dispose(true);
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Called by the <see cref="Dispose()"/> method or by the finalizer to free held resources
        /// </summary>
        /// <param name="disposing">Indicates whether this method is called from the 
        /// <see cref="Dispose()"/> method (<c>true</c>) or from the finalizer (<c>false</c>)</param>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_cancellationTokenSource")]
        protected virtual void Dispose(bool disposing)
        {
            _cancellationTokenSource.Cancel();
            if (disposing)
            {
                _cancellationTokenSource.TryDispose();
            }
        }
    }
}
