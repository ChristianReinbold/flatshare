using System;

namespace de.creinbold.FlatShare.Tests
{
    class DateProviderMock : DateUtils.IDateProvider
    {
        public DateTime Now { get; private set; }

        public DateProviderMock(DateTime now)
        {
            Now = now;
        }

        public DateProviderMock(string now)
        {
            Now = DateUtils.DateFromString(now);
        }
    }
}
