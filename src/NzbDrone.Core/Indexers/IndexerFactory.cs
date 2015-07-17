﻿using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Composition;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerFactory : IProviderFactory<IIndexer, IndexerDefinition>
    {
        List<IIndexer> RssEnabled();
        List<IIndexer> SearchEnabled();
    }

    public class IndexerFactory : ProviderFactory<IIndexer, IndexerDefinition>, IIndexerFactory
    {
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IIndexerRepository _providerRepository;
        private readonly Logger _logger;

        public IndexerFactory(IIndexerStatusService indexerStatusService,
                              IIndexerRepository providerRepository,
                              IEnumerable<IIndexer> providers,
                              IContainer container, 
                              IEventAggregator eventAggregator,
                              Logger logger)
            : base(providerRepository, providers, container, eventAggregator, logger)
        {
            _indexerStatusService = indexerStatusService;
            _providerRepository = providerRepository;
            _logger = logger;
        }

        protected override List<IndexerDefinition> Active()
        {
            return base.Active().Where(c => c.Enable).ToList();
        }

        public override IndexerDefinition GetProviderCharacteristics(IIndexer provider, IndexerDefinition definition)
        {
            definition = base.GetProviderCharacteristics(provider, definition);

            definition.Protocol = provider.Protocol;
            definition.SupportsRss = provider.SupportsRss;
            definition.SupportsSearch = provider.SupportsSearch;

            return definition;
        }

        public List<IIndexer> RssEnabled()
        {
            var enabledIndexers = GetAvailableProviders().Where(n => ((IndexerDefinition)n.Definition).EnableRss);

            var indexers = FilterBlockedIndexers(enabledIndexers);

            return indexers.ToList();
        }

        public List<IIndexer> SearchEnabled()
        {
            var enabledIndexers = GetAvailableProviders().Where(n => ((IndexerDefinition)n.Definition).EnableSearch);

            var indexers = FilterBlockedIndexers(enabledIndexers);

            return indexers.ToList();
        }

        private IEnumerable<IIndexer> FilterBlockedIndexers(IEnumerable<IIndexer> indexers)
        {
            foreach (var indexer in indexers)
            {
                var indexerStatus = _indexerStatusService.GetIndexerStatus(indexer.Definition.Id);
                if (indexerStatus != null && indexerStatus.IsDisabled())
                {
                    _logger.Debug("Temporarily ignoring indexer {0} till {1} due to recent failures.", indexer.Definition.Name, indexerStatus.DisabledTill.Value);
                    continue;
                }

                yield return indexer;
            }
        }
    }
}