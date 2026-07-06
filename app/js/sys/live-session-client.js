(function (window, document, $) {
  if (window.LiveSessionClient)
    return;

  var started = false;
  var loginUrl = null;

  function getAppPath() {
    var scripts = document.getElementsByTagName('script');
    for (var i = scripts.length - 1; i >= 0; i--) {
      var src = scripts[i].getAttribute('src') || '';
      var marker = '/js/sys/live-session-client.js';
      var markerIndex = src.toLowerCase().indexOf(marker);
      if (markerIndex >= 0)
        return src.substring(0, markerIndex + 1);
    }
    return '/';
  }

  function redirectToLogin() {
    var appPath = getAppPath();
    loginUrl = loginUrl || appPath + 'Login.ashx';
    var returnUrl = window.location.pathname + window.location.search;
    window.location.href = loginUrl + '?ReturnUrl=' + encodeURIComponent(returnUrl);
  }

  function dispatch(name, detail) {
    var event;
    if (typeof window.CustomEvent === 'function')
      event = new CustomEvent(name, { detail: detail });
    else {
      event = document.createEvent('CustomEvent');
      event.initCustomEvent(name, true, true, detail);
    }
    document.dispatchEvent(event);
  }

  function start() {
    if (started || !$ || !$.connection || !$.connection.liveSessionsHub)
      return;

    started = true;
    $.connection.liveSessionsHub.client.forceLogout = redirectToLogin;
    $.connection.liveSessionsHub.client.liveSessionsChanged = function (data) {
      dispatch('liveSessionsChanged', data);
    };

    if ($.connection.hub.state === $.signalR.connectionState.disconnected)
      $.connection.hub.start();
  }

  window.LiveSessionClient = {
    start: start,
    redirectToLogin: redirectToLogin
  };

  start();
})(window, document, window.jQuery);
