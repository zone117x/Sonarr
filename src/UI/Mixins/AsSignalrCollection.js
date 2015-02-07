var vent = require('vent');
require('signalR');


module.exports = function(){

    this.prototype.bindSignalR = function(bindOptions){
        var collection = this;
        bindOptions = bindOptions || {};
        var processMessage = function(options){
            if(options.action === 'sync') {
                console.log('sync received, re-fetching collection');
                collection.fetch();
                return;
            }
            if(options.action === 'deleted') {
                collection.remove(new collection.model(options.resource, {parse : true}));
                return;
            }
            var model = new collection.model(options.resource, {parse : true});
            if(bindOptions.updateOnly && !collection.get(model.get('id'))) {
                return;
            }
            collection.add(model, {
                merge        : true,
                changeSource : 'signalr'
            });

            console.log(options.action + ': {0}}'.format(options.resource));
        };

        collection.listenTo(vent, 'server:' + collection.url.split('/api/')[1], processMessage);

        return this;
    };

    return this;
};
