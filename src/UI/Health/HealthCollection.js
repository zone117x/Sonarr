var Backbone = require('backbone');
var HealthModel = require('./HealthModel');
var AsSignalRCollection = require('../Mixins/AsSignalrCollection');


var Collection = Backbone.Collection.extend({
    url   : window.NzbDrone.ApiRoot + '/health',
    model : HealthModel
});

AsSignalRCollection.call(Collection);

var collection = new Collection();
collection.bindSignalR();
collection.fetch();

module.exports = collection;