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
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Platibus.Security;

namespace Platibus.UnitTests
{
    [Trait("Category", "UnitTests")]
    public abstract class MessageQueueingServiceTests<TMessageQueueingService>
        where TMessageQueueingService : IMessageQueueingService
    {
        protected readonly TMessageQueueingService MessageQueueingService;

        protected ISecurityTokenService SecurityTokenService;
        protected IPrincipal Principal;
        protected QueueListenerStub Listener;
        protected Message HandledMessage;

        protected MessageQueueingServiceTests(TMessageQueueingService messageQueueingService)
        {
            MessageQueueingService = messageQueueingService;
            SecurityTokenService = new JwtSecurityTokenService();
        }

        [Fact]
        public async Task QueueListenerFiresWhenNewMessageEnqueued()
        {
            var listener = new QueueListenerStub();

            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            await MessageQueueingService.CreateQueue(queue, listener);
            await MessageQueueingService.EnqueueMessage(queue, message, null);
            await listener.Completed;
            listener.Dispose();
            AssertMessageHandled(message, listener.Message);
        }

        [Fact]
        public async Task PrincipalIsPreservedWhenListenerInvoked()
        {
            var listener = new QueueListenerStub();
            GivenClaimsPrincipal();
            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            await MessageQueueingService.CreateQueue(queue, listener);
            await MessageQueueingService.EnqueueMessage(queue, message, Principal);
            await listener.Completed;
            listener.Dispose();
            AssertPrincipalPreserved(listener.Context.Principal);
        }

        [Fact]
        public async Task QueueListenerFiresForExistingMessagesWhenQueueCreated()
        {
            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            await GivenExistingQueuedMessage(queue, message, null);
            var listener = new QueueListenerStub();
            await MessageQueueingService.CreateQueue(queue, listener);
            await listener.Completed;
            listener.Dispose();
            AssertMessageHandled(message, listener.Message);
        }

        [Fact]
        public async Task MessageIsRemovedFromQueueWhenAcknowledged()
        {
            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            var listener = new QueueListenerStub();
            await MessageQueueingService.CreateQueue(queue, listener);
            await MessageQueueingService.EnqueueMessage(queue, message, Principal);
            await listener.Completed;
            listener.Dispose();

            await QueueOperationCompletion();
            await AssertMessageNoLongerQueued(queue, message);
        }

        [Fact]
        public async Task MessageCanBeAutomaticallyAcknowledged()
        {
            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            var listener = new QueueListenerStub((m, c) => { });
            var autoAcknowledgeOptions = new QueueOptions
            {
                AutoAcknowledge = true,
                IsDurable = false
            };
            await MessageQueueingService.CreateQueue(queue, listener, autoAcknowledgeOptions);
            await MessageQueueingService.EnqueueMessage(queue, message, Principal);
            await listener.Completed;
            listener.Dispose();

            await QueueOperationCompletion();
            await AssertMessageNoLongerQueued(queue, message);
        }

        [Fact]
        public async Task MessageIsDeadWhenMaxAttemptsAreExceeded()
        {
            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            var listener = new QueueListenerStub((m, c) => { throw new Exception("Test"); });
            var maxAttemptOptions = new QueueOptions
            {
                MaxAttempts = 1,
                IsDurable = false
            };
            await MessageQueueingService.CreateQueue(queue, listener, maxAttemptOptions);
            await MessageQueueingService.EnqueueMessage(queue, message, Principal);
            await listener.Completed;
            listener.Dispose();

            await QueueOperationCompletion();
            await AssertMessageIsDead(queue, message);
            await AssertMessageNoLongerQueued(queue, message);
        }

        [Fact]
        public async Task MessageIsRetriedIfNotAcknowledged()
        {
            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            var listener = new CountdownListenerStub(2);
            var maxAttemptOptions = new QueueOptions
            {
                MaxAttempts = 2,
                RetryDelay = TimeSpan.FromMilliseconds(100),
                IsDurable = false
            };
            await MessageQueueingService.CreateQueue(queue, listener, maxAttemptOptions);
            await MessageQueueingService.EnqueueMessage(queue, message, Principal);
            await listener.Completed;
            listener.Dispose();
            Assert.Equal(2, listener.Count);

            await QueueOperationCompletion();
            await AssertMessageIsDead(queue, message);
            await AssertMessageNoLongerQueued(queue, message);
        }

        [Fact]
        public async Task MessageNotAutomaticallyAcknowledgedWhenListenerThrows()
        {
            var queue = GivenUniqueQueueName();
            var message = GivenSampleMessage();
            var listener = new QueueListenerStub((m, c) => { throw new Exception("Test"); });
            var autoAcknowledgeOptions = new QueueOptions
            {
                AutoAcknowledge = true,
                IsDurable = false
            };
            await MessageQueueingService.CreateQueue(queue, listener, autoAcknowledgeOptions);
            await MessageQueueingService.EnqueueMessage(queue, message, Principal);
            await listener.Completed;
            listener.Dispose();

            await QueueOperationCompletion();
            await AssertMessageStillQueuedForRetry(queue, message);
        }

        protected abstract Task GivenExistingQueuedMessage(QueueName queueName, Message message, IPrincipal principal);

        protected abstract Task<bool> MessageQueued(QueueName queueName, Message message);

        protected abstract Task<bool> MessageDead(QueueName queueName, Message message);

        protected QueueName GivenUniqueQueueName()
        {
            return Guid.NewGuid().ToString("N");
        }

        protected IPrincipal GivenNoPrincipal()
        {
            return Principal = null;
        }

        protected IPrincipal GivenClaimsPrincipal()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, "user"),
                new Claim(ClaimTypes.Role, "staff"),
            };
            return Principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        protected virtual Task QueueOperationCompletion()
        {
            return Task.Delay(100);
        }

        protected void AssertMessageHandled(Message originalMessage, Message handledMessage)
        {
            var comparer = new MessageEqualityComparer(HeaderName.SecurityToken);
            Assert.NotNull(handledMessage);
            Assert.Equal(originalMessage, handledMessage, comparer);
        }

        protected void AssertPrincipalPreserved(IPrincipal contextPrincipal)
        {
            if (Principal == null) Assert.Null(contextPrincipal);
            Assert.NotNull(contextPrincipal);

            var originalClaimsPrincipal = Principal as ClaimsPrincipal;
            Assert.NotNull(originalClaimsPrincipal);

            var contextClaimsPrincipal = contextPrincipal as ClaimsPrincipal;
            Assert.NotNull(contextClaimsPrincipal);
            var originalClaims = originalClaimsPrincipal.Claims;
            foreach (var originalClaim in originalClaims)
            {
                Assert.True(contextClaimsPrincipal.HasClaim(originalClaim.Type, originalClaim.Value));
            }
        }

        protected virtual async Task AssertMessageStillQueuedForRetry(QueueName queue, Message message)
        {
            Assert.True(await MessageQueued(queue, message));
        }

        protected virtual async Task AssertMessageNoLongerQueued(QueueName queue, Message message)
        {
            Assert.False(await MessageQueued(queue, message));
        }

        protected virtual async Task AssertMessageIsDead(QueueName queue, Message message)
        {
            Assert.True(await MessageDead(queue, message));
        }

        protected virtual async Task AssertMessageNotDead(QueueName queue, Message message)
        {
            Assert.False(await MessageDead(queue, message));
        }

        protected static Message GivenSampleMessage()
        {
            var headers = new MessageHeaders
            {
                MessageId = MessageId.Generate(),
                Origination = new Uri("http://localhost/platibus0"),
                Destination = new Uri("http://localhost/platibus1"),
                Importance = MessageImportance.High,
                ContentType = "text/plain",
                Expires = DateTime.UtcNow.AddMinutes(5),
                MessageName = "http://example.com/ns/test",
                Sent = DateTime.UtcNow
            };

            return new Message(headers, "Hello, world!");
        }

        protected class QueueListenerStub : IQueueListener, IDisposable
        {
            private readonly Action<Message, IQueuedMessageContext> _callback;
            private readonly TaskCompletionSource<Message> _taskCompletionSource;
            private readonly CancellationTokenSource _cancellationTokenSource;

            private bool _disposed;

            public Task Completed
            {
                get { return _taskCompletionSource.Task; }
            }

            public Message Message { get; private set; }
            public IQueuedMessageContext Context { get; private set; }

            public QueueListenerStub(Action<Message, IQueuedMessageContext> callback = null, TimeSpan timeout = default(TimeSpan))
            {
                _callback = callback ?? ((m, c) => c.Acknowledge());

                _taskCompletionSource = new TaskCompletionSource<Message>();
                _cancellationTokenSource = new CancellationTokenSource();
                var cancelAfter = timeout <= TimeSpan.Zero
                    ? TimeSpan.FromSeconds(3)
                    : timeout;

                _cancellationTokenSource.Token.Register(() => _taskCompletionSource.TrySetCanceled());
                _cancellationTokenSource.CancelAfter(cancelAfter);
            }

            public Task MessageReceived(Message message, IQueuedMessageContext context,
                CancellationToken cancellationToken = new CancellationToken())
            {
                Message = message;
                Context = context;
                try
                {
                    _callback(message, context);
                    return Task.FromResult(0);
                }
                finally
                {
                    _taskCompletionSource.TrySetResult(message);
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                Dispose(true);
                _disposed = true;
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _cancellationTokenSource.TryDispose();
                }
            }
        }

        protected class CountdownListenerStub : IQueueListener, IDisposable
        {
            private readonly TaskCompletionSource<Message> _taskCompletionSource;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly int _target;
            private int _count;
            private bool _disposed;

            public Task Completed
            {
                get { return _taskCompletionSource.Task; }
            }

            public int Count
            {
                get { return _count; }
            }

            public CountdownListenerStub(int target, TimeSpan timeout = default(TimeSpan))
            {
                _target = target;
                _count = 0;

                _taskCompletionSource = new TaskCompletionSource<Message>();
                _cancellationTokenSource = new CancellationTokenSource();
                var cancelAfter = timeout <= TimeSpan.Zero
                    ? TimeSpan.FromSeconds(3)
                    : timeout;

                _cancellationTokenSource.Token.Register(() => _taskCompletionSource.TrySetCanceled());
                _cancellationTokenSource.CancelAfter(cancelAfter);
            }

            public Task MessageReceived(Message message, IQueuedMessageContext context,
                CancellationToken cancellationToken = new CancellationToken())
            {
                if (Interlocked.Increment(ref _count) >= _target)
                {
                    _taskCompletionSource.TrySetResult(message);
                }
                return Task.FromResult(0);
            }

            public void Dispose()
            {
                if (_disposed) return;
                Dispose(true);
                _disposed = true;
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _cancellationTokenSource.TryDispose();
                }
            }
        }
    }
}