﻿using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Platibus.SQL;

namespace Platibus.UnitTests.LocalDB
{
    [Trait("Category", "UnitTests")]
    [Collection(LocalDBCollection.Name)]
    public class LocalDBMessageJournalingServiceTests : MessageJournalingServiceTests
    {
        private readonly SQLMessageJournalInspector _inspector;
        
        public LocalDBMessageJournalingServiceTests(LocalDBFixture fixture)
            : this(fixture.MessageJournalingService)
        {
        }

        private LocalDBMessageJournalingServiceTests(SQLMessageJournalingService messageJournalingService)
            : base(messageJournalingService)
        {
            _inspector = new SQLMessageJournalInspector(messageJournalingService);
        }
        
        protected override async Task AssertSentMessageIsWrittenToJournal()
        {
            var journaledMessages = await _inspector.EnumerateMessages();
            var messageIsJournaled = journaledMessages
                .Any(m => m.Message.Headers.MessageId == Message.Headers.MessageId && m.Category == "Sent");
            Assert.True(messageIsJournaled);
        }

        protected override async Task AssertReceivedMessageIsWrittenToJournal()
        {
            var journaledMessages = await _inspector.EnumerateMessages();
            var messageIsJournaled = journaledMessages
                .Any(m => m.Message.Headers.MessageId == Message.Headers.MessageId && m.Category == "Received");
            Assert.True(messageIsJournaled);
        }

        protected override async Task AssertPublishedMessageIsWrittenToJournal()
        {
            var journaledMessages = await _inspector.EnumerateMessages();
            var messageIsJournaled = journaledMessages
                .Any(m => m.Message.Headers.MessageId == Message.Headers.MessageId && m.Category == "Published");
            Assert.True(messageIsJournaled);
        }
    }
}
