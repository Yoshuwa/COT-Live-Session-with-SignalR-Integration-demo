using Microsoft.AspNet.SignalR;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;
using yoshuwa.Security;

namespace yoshuwa.SignalR
{
    public class LiveSessionsHub : Hub
    {
        public override Task OnConnected()
        {
            var sessionId = GetSessionId();
            if (!string.IsNullOrEmpty(sessionId))
                return Groups.Add(Context.ConnectionId, LiveSessionBroadcaster.GetSessionGroupName(sessionId));
            return base.OnConnected();
        }

        public void Refresh()
        {
            Clients.Caller.liveSessionsChanged(LiveSessionBroadcaster.Snapshot());
        }

        private string GetSessionId()
        {
            var cookie = Context.RequestCookies["ASP.NET_SessionId"];
            return cookie == null ? null : cookie.Value;
        }
    }

    public static class LiveSessionBroadcaster
    {
        public static string GetSessionGroupName(string sessionId)
        {
            return "live-session-" + sessionId;
        }

        public static void Broadcast()
        {
            GlobalHost.ConnectionManager.GetHubContext<LiveSessionsHub>().Clients.All.liveSessionsChanged(Snapshot());
        }

        public static void ForceLogout(string[] sessionIds)
        {
            if (sessionIds == null || sessionIds.Length == 0)
                return;

            var hub = GlobalHost.ConnectionManager.GetHubContext<LiveSessionsHub>();
            foreach (var sessionId in sessionIds.Where(s => !string.IsNullOrEmpty(s)).Distinct(System.StringComparer.OrdinalIgnoreCase))
                hub.Clients.Group(GetSessionGroupName(sessionId)).forceLogout();
        }

        public static JObject Snapshot()
        {
            var sessions = LiveSessionManager.GetActiveSessions();
            var users = sessions.Select(s => s.UserName).Distinct(System.StringComparer.OrdinalIgnoreCase).Count();
            var result = new JObject();
            result["users"] = users;
            result["sessions"] = sessions.Length;
            return result;
        }
    }
}
