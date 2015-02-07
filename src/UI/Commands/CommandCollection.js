var Backbone = require('backbone');
var CommandModel = require('./CommandModel');
var AsSignalRCollection = require('../Mixins/AsSignalrCollection');

var CommandCollection = Backbone.Collection.extend({
    url         : window.NzbDrone.ApiRoot + '/command',
    model       : CommandModel,
    findCommand : function(command){
        return this.find(function(model){
            return model.isSameCommand(command);
        });
    }
});

AsSignalRCollection.call(CommandCollection);
var collection = new CommandCollection();

collection.bindSignalR();
collection.fetch();

module.exports = collection;