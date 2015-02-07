var Backbone = require('backbone');
var RootFolderModel = require('./RootFolderModel');


module.exports = (function(){
    var RootFolderCollection = Backbone.Collection.extend({
        url   : window.NzbDrone.ApiRoot + '/rootfolder',
        model : RootFolderModel
    });
    return new RootFolderCollection();
}).call(this);