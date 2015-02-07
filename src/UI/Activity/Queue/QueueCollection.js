var _ = require('underscore');
var PageableCollection = require('backbone.pageable');
var QueueModel = require('./QueueModel');
var AsSignalRCollection = require('../../Mixins/AsSignalrCollection');

var QueueCollection = PageableCollection.extend({
    url         : window.NzbDrone.ApiRoot + '/queue',
    model       : QueueModel,
    state       : {pageSize : 15},
    mode        : 'client',
    findEpisode : function(episodeId){
        return _.find(this.fullCollection.models, function(queueModel){
            return queueModel.get('episode').id === episodeId;
        });
    }
});

AsSignalRCollection.call(QueueCollection);
var collection = new QueueCollection();
collection.bindSignalR();
collection.fetch();

module.exports = collection;