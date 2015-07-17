using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using System.Linq;
using NzbDrone.Common.TPL;

namespace NzbDrone.Core.IndexerSearch
{
    public interface ISearchForNzb
    {
        List<DownloadDecision> EpisodeSearch(int episodeId);
        List<DownloadDecision> EpisodeSearch(Episode episode);
        List<DownloadDecision> SeasonSearch(int seriesId, int seasonNumber, bool missingOnly);
    }

    public class NzbSearchService : ISearchForNzb
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly ISceneMappingService _sceneMapping;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IMakeDownloadDecision _makeDownloadDecision;
        private readonly Logger _logger;

        public NzbSearchService(IIndexerFactory indexerFactory,
                                IIndexerStatusService indexerStatusService,
                                ISceneMappingService sceneMapping,
                                ISeriesService seriesService,
                                IEpisodeService episodeService,
                                IMakeDownloadDecision makeDownloadDecision,
                                Logger logger)
        {
            _indexerFactory = indexerFactory;
            _indexerStatusService = indexerStatusService;
            _sceneMapping = sceneMapping;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _makeDownloadDecision = makeDownloadDecision;
            _logger = logger;
        }

        public List<DownloadDecision> EpisodeSearch(int episodeId)
        {
            var episode = _episodeService.GetEpisode(episodeId);

            return EpisodeSearch(episode);
        }

        public List<DownloadDecision> EpisodeSearch(Episode episode)
        {
            var series = _seriesService.GetSeries(episode.SeriesId);

            if (series.SeriesType == SeriesTypes.Daily)
            {
                if (string.IsNullOrWhiteSpace(episode.AirDate))
                {
                    throw new InvalidOperationException("Daily episode is missing AirDate. Try to refresh series info.");
                }

                return SearchDaily(series, episode);
            }
            if (series.SeriesType == SeriesTypes.Anime)
            {
                return SearchAnime(series, episode);
            }

            if (episode.SeasonNumber == 0)
            {
                // search for special episodes in season 0 
                return SearchSpecial(series, new List<Episode> { episode });
            }

            return SearchSingle(series, episode);
        }

        public List<DownloadDecision> SeasonSearch(int seriesId, int seasonNumber, bool missingOnly)
        {
            var series = _seriesService.GetSeries(seriesId);
            var episodes = _episodeService.GetEpisodesBySeason(seriesId, seasonNumber);

            if (missingOnly)
            {
                episodes = episodes.Where(e => e.Monitored && !e.HasFile).ToList();
            }

            if (series.SeriesType == SeriesTypes.Anime)
            {
                return SearchAnimeSeason(series, episodes);
            }

            if (seasonNumber == 0)
            {
                // search for special episodes in season 0 
                return SearchSpecial(series, episodes);
            }

            var downloadDecisions = new List<DownloadDecision>();

            if (series.UseSceneNumbering)
            {
                var sceneSeasonGroups = episodes.GroupBy(v =>
                {
                    if (v.SceneSeasonNumber.HasValue && v.SceneEpisodeNumber.HasValue)
                    {
                        return v.SceneSeasonNumber.Value;
                    }
                    return v.SeasonNumber;
                }).Distinct();

                foreach (var sceneSeasonEpisodes in sceneSeasonGroups)
                {
                    if (sceneSeasonEpisodes.Count() == 1)
                    {
                        var episode = sceneSeasonEpisodes.First();
                        var searchSpec = Get<SingleEpisodeSearchCriteria>(series, sceneSeasonEpisodes.ToList());
                        
                        searchSpec.SeasonNumber = sceneSeasonEpisodes.Key;
                        searchSpec.MonitoredEpisodesOnly = true;

                        if (episode.SceneSeasonNumber.HasValue && episode.SceneEpisodeNumber.HasValue)
                        {
                            searchSpec.EpisodeNumber = episode.SceneEpisodeNumber.Value;
                        }
                        else
                        {
                            searchSpec.EpisodeNumber = episode.EpisodeNumber;
                        }

                        var decisions = Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
                        downloadDecisions.AddRange(decisions);
                    }
                    else
                    {
                        var searchSpec = Get<SeasonSearchCriteria>(series, sceneSeasonEpisodes.ToList());
                        searchSpec.SeasonNumber = sceneSeasonEpisodes.Key;

                        var decisions = Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
                        downloadDecisions.AddRange(decisions);
                    }
                }
            }
            else
            {
                var searchSpec = Get<SeasonSearchCriteria>(series, episodes);
                searchSpec.SeasonNumber = seasonNumber;

                var decisions = Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
                downloadDecisions.AddRange(decisions);
            }

            return downloadDecisions;
        }

        private List<DownloadDecision> SearchSingle(Series series, Episode episode)
        {
            var searchSpec = Get<SingleEpisodeSearchCriteria>(series, new List<Episode>{episode});

            if (series.UseSceneNumbering && episode.SceneSeasonNumber.HasValue && episode.SceneEpisodeNumber.HasValue)
            {
                searchSpec.EpisodeNumber = episode.SceneEpisodeNumber.Value;
                searchSpec.SeasonNumber = episode.SceneSeasonNumber.Value;
            }
            else
            {
                searchSpec.EpisodeNumber = episode.EpisodeNumber;
                searchSpec.SeasonNumber = episode.SeasonNumber;
            }

            return Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        private List<DownloadDecision> SearchDaily(Series series, Episode episode)
        {
            var airDate = DateTime.ParseExact(episode.AirDate, Episode.AIR_DATE_FORMAT, CultureInfo.InvariantCulture);
            var searchSpec = Get<DailyEpisodeSearchCriteria>(series, new List<Episode>{ episode });
            searchSpec.AirDate = airDate;

            return Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        private List<DownloadDecision> SearchAnime(Series series, Episode episode)
        {
            var searchSpec = Get<AnimeEpisodeSearchCriteria>(series, new List<Episode> { episode });

            if (episode.SceneAbsoluteEpisodeNumber.HasValue)
            {
                searchSpec.AbsoluteEpisodeNumber = episode.SceneAbsoluteEpisodeNumber.Value;
            }
            else if (episode.AbsoluteEpisodeNumber.HasValue)
            {
                searchSpec.AbsoluteEpisodeNumber = episode.AbsoluteEpisodeNumber.Value;
            }
            else
            {
                throw new ArgumentOutOfRangeException("AbsoluteEpisodeNumber", "Can not search for an episode without an absolute episode number");
            }

            return Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        private List<DownloadDecision> SearchSpecial(Series series, List<Episode> episodes)
        {
            var searchSpec = Get<SpecialEpisodeSearchCriteria>(series, episodes);
            // build list of queries for each episode in the form: "<series> <episode-title>"
            searchSpec.EpisodeQueryTitles = episodes.Where(e => !String.IsNullOrWhiteSpace(e.Title))
                                                    .SelectMany(e => searchSpec.QueryTitles.Select(title => title + " " + SearchCriteriaBase.GetQueryTitle(e.Title)))
                                                    .ToArray();

            return Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        private List<DownloadDecision> SearchAnimeSeason(Series series, List<Episode> episodes)
        {
            var downloadDecisions = new List<DownloadDecision>();

            foreach (var episode in episodes)
            {
                downloadDecisions.AddRange(SearchAnime(series, episode));
            }

            return downloadDecisions;
        }

        private TSpec Get<TSpec>(Series series, List<Episode> episodes) where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();

            spec.Series = series;
            spec.SceneTitles = _sceneMapping.GetSceneNames(series.TvdbId,
                                                           episodes.Select(e => e.SeasonNumber)
                                                                   .Concat(episodes.Where(v => v.SceneSeasonNumber.HasValue).Select(e => e.SceneSeasonNumber.Value)
                                                                   .Distinct()));

            spec.Episodes = episodes;

            spec.SceneTitles.Add(series.Title);

            return spec;
        }

        private List<DownloadDecision> Dispatch(Func<IIndexer, IEnumerable<ReleaseInfo>> searchAction, SearchCriteriaBase criteriaBase)
        {
            var indexers = _indexerFactory.SearchEnabled();
            var reports = new List<ReleaseInfo>();

            _logger.ProgressInfo("Searching {0} indexers for {1}", indexers.Count, criteriaBase);

            var taskList = new List<Task>();
            var taskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

            foreach (var indexer in indexers)
            {
                var indexerLocal = indexer;

                taskList.Add(taskFactory.StartNew(() =>
                {
                    try
                    {
                        var indexerReports = searchAction(indexerLocal);

                        lock (reports)
                        {
                            reports.AddRange(indexerReports);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.ErrorException("Error while searching for " + criteriaBase, e);
                    }
                }).LogExceptions());
            }

            Task.WaitAll(taskList.ToArray());

            _logger.Debug("Total of {0} reports were found for {1} from {2} indexers", reports.Count, criteriaBase, indexers.Count);

            return _makeDownloadDecision.GetSearchDecision(reports, criteriaBase).ToList();
        }
    }
}
