﻿// The MIT License (MIT)
// 
// Copyright (c) 2016 Jesse Sweetland
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
using System.Text;
using System.Threading.Tasks;
using Platibus.Config.Extensibility;
#if NET452
using Platibus.Config;
#endif
#if NETSTANDARD2_0
using Microsoft.Extensions.Configuration;
#endif

namespace Platibus.RabbitMQ
{
    /// <inheritdoc />
    /// <summary>
    /// Provider for services based on RabbitMQ
    /// </summary>
    [Provider("RabbitMQ")]
    public class RabbitMQServicesProvider : IMessageQueueingServiceProvider
    {
#if NET452
        /// <summary>
        /// Creates an initializes a <see cref="IMessageQueueingService"/>
        /// based on the provided <paramref name="configuration"/>
        /// </summary>
        /// <param name="configuration">The journaling configuration
        /// element</param>
        /// <returns>Returns a task whose result is an initialized
        /// <see cref="IMessageQueueingService"/></returns>
        public async Task<IMessageQueueingService> CreateMessageQueueingService(QueueingElement configuration)
        {
            var securityTokenServiceFactory = new SecurityTokenServiceFactory();
            var securitTokenConfig = configuration.SecurityTokens;
            var securityTokenService = await securityTokenServiceFactory.InitSecurityTokenService(securitTokenConfig);

            var uri = configuration.GetUri("uri") ?? new Uri("amqp://localhost:5672");

            var encodingName = configuration.GetString("encoding");
            if (string.IsNullOrWhiteSpace(encodingName))
            {
                encodingName = "UTF-8";
            }
            var encoding = ParseEncoding(encodingName);

            var messageEncryptionServiceFactory = new MessageEncryptionServiceFactory();
            var messageEncryptionConfig = configuration.Encryption;
            var messageEncryptionService = await messageEncryptionServiceFactory.InitMessageEncryptionService(messageEncryptionConfig);

            var queueingOptions = new RabbitMQMessageQueueingOptions(uri)
            {
                Encoding = encoding,
                SecurityTokenService = securityTokenService,
                MessageEncryptionService = messageEncryptionService
            };

            var messageQueueingService = new RabbitMQMessageQueueingService(queueingOptions);
            return messageQueueingService;
        }
#endif
#if NETSTANDARD2_0
        /// <summary>
        /// Creates an initializes a <see cref="IMessageQueueingService"/>
        /// based on the provided <paramref name="configuration"/>
        /// </summary>
        /// <param name="configuration">The journaling configuration
        /// element</param>
        /// <returns>Returns a task whose result is an initialized
        /// <see cref="IMessageQueueingService"/></returns>
        public async Task<IMessageQueueingService> CreateMessageQueueingService(IConfiguration configuration)
        {
            var securityTokenServiceFactory = new SecurityTokenServiceFactory();
            var securityTokensSection = configuration?.GetSection("securityTokens");
            var securityTokenService = await securityTokenServiceFactory.InitSecurityTokenService(securityTokensSection);

            var defaultUri = new Uri("amqp://localhost:5672");
            var uri = configuration?.GetValue("uri", defaultUri) ?? defaultUri;

            var encodingName = configuration?["encoding"];
            if (string.IsNullOrWhiteSpace(encodingName))
            {
                encodingName = "UTF-8";
            }
            var encoding = ParseEncoding(encodingName);

            var messageEncryptionServiceFactory = new MessageEncryptionServiceFactory();
            var messageEncryptionConfig = configuration?.GetSection("encryption");
            var messageEncryptionService = await messageEncryptionServiceFactory.InitMessageEncryptionService(messageEncryptionConfig);

            var queueingOptions = new RabbitMQMessageQueueingOptions(uri)
            {
                Encoding = encoding,
                SecurityTokenService = securityTokenService,
                MessageEncryptionService = messageEncryptionService
            };
            
            var messageQueueingService = new RabbitMQMessageQueueingService(queueingOptions);
            return messageQueueingService;
        }
#endif

        private static Encoding ParseEncoding(string encodingName)
        {
            switch (encodingName.ToUpper())
            {
                case "UTF7":
                case "UTF-7":
                    return Encoding.UTF7;
                case "UTF8":
                case "UTF-8":
                    return Encoding.UTF8;
                case "UTF32":
                case "UTF-32":
                    return Encoding.UTF32;
                case "UNICODE":
                    return Encoding.Unicode;
                case "ASCII":
                    return Encoding.ASCII;
                default:
                    return Encoding.UTF8;
            }
        }
    }
}