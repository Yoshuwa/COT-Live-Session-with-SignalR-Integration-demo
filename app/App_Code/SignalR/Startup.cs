using Microsoft.AspNet.SignalR;
using Owin;

[assembly: Microsoft.Owin.OwinStartup(typeof(yoshuwa.SignalR.Startup))]

namespace yoshuwa.SignalR
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var configuration = new HubConfiguration
            {
                EnableDetailedErrors = true
            };
            app.MapSignalR(configuration);
        }
    }
}
