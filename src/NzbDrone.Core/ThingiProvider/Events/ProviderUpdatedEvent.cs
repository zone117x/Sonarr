﻿using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.ThingiProvider.Events
{
    public class ProviderUpdatedEvent<TProvider> : IEvent
    {
        public int ProviderId { get; private set; }

        public ProviderUpdatedEvent(int id)
        {
            ProviderId = id;
        }
    }
}