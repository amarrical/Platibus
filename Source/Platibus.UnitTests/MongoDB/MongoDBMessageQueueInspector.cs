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
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Platibus.MongoDB;
using Platibus.Queueing;
using Platibus.Security;

namespace Platibus.UnitTests.MongoDB
{
    internal class MongoDBMessageQueueInspector : MongoDBMessageQueue
    {
        public MongoDBMessageQueueInspector(IMongoDatabase database, QueueName queueName, ISecurityTokenService securityTokenService, QueueOptions options = null, string collectionName = null) 
            : base(database, queueName, new NoopQueueListener(), securityTokenService, options, collectionName)
        {
        }

        public override Task Init(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult(0);
        }

        public Task InsertMessage(QueuedMessage queuedMessage)
        {
            return InsertQueuedMessage(queuedMessage);
        }

        public Task<IEnumerable<QueuedMessage>> EnumerateMessages()
        {
            return GetPendingMessages();
        }

        public Task<IEnumerable<QueuedMessage>> EnumerateAbandonedMessages(DateTime startDate, DateTime endDate)
        {
            return GetDeadMessages(startDate, endDate);
        }

        private class NoopQueueListener : IQueueListener
        {
            public Task MessageReceived(Message message, IQueuedMessageContext context,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return Task.FromResult(false);
            }
        }
    }
}
