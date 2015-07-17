var _ = require('underscore');
var PageableCollection = require('backbone.pageable');

var SonarrPageableCollection = PageableCollection.extend({
    _makeCollectionEventHandler : function (pageCollection, fullCollection) {
        this.pageCollection = pageCollection;
        this.fullCollection = fullCollection;
        var eventHandler = PageableCollection.prototype._makeCollectionEventHandler.apply(this, arguments);

        return _.wrap(eventHandler, _.bind(this._eventHandler, this));
    },

    _eventHandler : function (originalEventHandler, event, model, collection, options) {
        if (event === 'reset') {
            var currentPage = this.state.currentPage;
            var pageSize = this.state.pageSize;
            var pageStart = currentPage * pageSize;
            var pageEnd = pageStart + pageSize;

            originalEventHandler.apply(this, [].slice.call(arguments, 1));

            var totalPages = Math.ceil(this.state.totalRecords / pageSize);

            if (currentPage > totalPages) {
                this.pageCollection.reset(this.fullCollection.models.slice(0, pageSize),
                    _.extend({}, options, {parse: false}));
            } else if (currentPage !== this.state.currentPage) {
                this.state.currentPage = currentPage;

                // If backbone pageable fixes their reset bug
                // (they reset the page number, but not the range),
                // we'll need to do this
//                this.pageCollection.reset(this.fullCollection.models.slice(pageStart, pageEnd),
//                    _.extend({}, options, {parse: false}));
            }
        } else {
            originalEventHandler.call(this, [].slice.call(arguments, 1));
        }
    }
});

module.exports = SonarrPageableCollection;
