var Backbone = require('backbone');
var EpisodeModel = require('../Series/EpisodeModel');
var AsSignalRCollection = require('../Mixins/AsSignalrCollection');

var collection = Backbone.Collection.extend({
    url        : window.NzbDrone.ApiRoot + '/calendar',
    model      : EpisodeModel,
    comparator : function(model){
        var date = new Date(model.get('airDateUtc'));
        var time = date.getTime();
        return time;
    }
});

AsSignalRCollection.call(collection);

module.exports = collection;