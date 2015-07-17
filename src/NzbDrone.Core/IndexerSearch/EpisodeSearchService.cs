﻿using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.IndexerSearch
{
    public class EpisodeSearchService : IExecute<EpisodeSearchCommand>, IExecute<MissingEpisodeSearchCommand>
    {
        private readonly ISearchForNzb _nzbSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly IEpisodeService _episodeService;
        private readonly IQueueService _queueService;
        private readonly Logger _logger;

        public EpisodeSearchService(ISearchForNzb nzbSearchService,
                                    IProcessDownloadDecisions processDownloadDecisions,
                                    IEpisodeService episodeService,
                                    IQueueService queueService,
                                    Logger logger)
        {
            _nzbSearchService = nzbSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _episodeService = episodeService;
            _queueService = queueService;
            _logger = logger;
        }

        private void SearchForMissingEpisodes(List<Episode> episodes)
        {
            _logger.ProgressInfo("Performing missing search for {0} episodes", episodes.Count);
            var downloadedCount = 0;

            foreach (var series in episodes.GroupBy(e => e.SeriesId))
            {
                foreach (var season in series.Select(e => e).GroupBy(e => e.SeasonNumber))
                {
                    List<DownloadDecision> decisions;

                    if (season.Count() > 1)
                    {
                        decisions = _nzbSearchService.SeasonSearch(series.Key, season.Key, true);
                    }

                    else
                    {
                        decisions = _nzbSearchService.EpisodeSearch(season.First());
                    }

                    var processed = _processDownloadDecisions.ProcessDecisions(decisions);

                    downloadedCount += processed.Grabbed.Count;
                }
            }

            _logger.ProgressInfo("Completed missing search for {0} episodes. {1} reports downloaded.", episodes.Count, downloadedCount);
        }
        
        public void Execute(EpisodeSearchCommand message)
        {
            foreach (var episodeId in message.EpisodeIds)
            {
                var decisions = _nzbSearchService.EpisodeSearch(episodeId);
                var processed = _processDownloadDecisions.ProcessDecisions(decisions);

                _logger.ProgressInfo("Episode search completed. {0} reports downloaded.", processed.Grabbed.Count);
            }
        }

        public void Execute(MissingEpisodeSearchCommand message)
        {
            List<Episode> episodes;

            if (message.SeriesId.HasValue)
            {
                episodes = _episodeService.GetEpisodeBySeries(message.SeriesId.Value)
                                          .Where(e => e.Monitored &&
                                                 !e.HasFile &&
                                                 e.AirDateUtc.HasValue &&
                                                 e.AirDateUtc.Value.Before(DateTime.UtcNow))
                                          .ToList();
            }

            else
            {
                episodes = _episodeService.EpisodesWithoutFiles(new PagingSpec<Episode>
                                                                    {
                                                                        Page = 1,
                                                                        PageSize = 100000,
                                                                        SortDirection = SortDirection.Ascending,
                                                                        SortKey = "Id",
                                                                        FilterExpression =
                                                                            v =>
                                                                            v.Monitored == true &&
                                                                            v.Series.Monitored == true
                                                                    }).Records.ToList();
            }

            var queue = _queueService.GetQueue().Select(q => q.Episode.Id);
            var missing = episodes.Where(e => !queue.Contains(e.Id)).ToList();

            SearchForMissingEpisodes(missing);
        }
    }
}
