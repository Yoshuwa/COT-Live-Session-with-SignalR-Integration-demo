using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using yoshuwa.SignalR;

namespace yoshuwa.Security
{
    public class LiveSessionInfo
    {
        public string SessionId { get; set; }
        public string UserName { get; set; }
        public string UserHostAddress { get; set; }
        public string UserAgent { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LastActivity { get; set; }
        public bool ForceLogout { get; set; }
    }

    public static class LiveSessionManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, LiveSessionInfo> Sessions = new Dictionary<string, LiveSessionInfo>(StringComparer.OrdinalIgnoreCase);

        public static int ActiveCount
        {
            get
            {
                return GetActiveSessions().Length;
            }
        }

        public static LiveSessionInfo[] GetActiveSessions()
        {
            lock (SyncRoot)
            {
                RemoveExpiredSessions();
                return Sessions.Values
                    .Where(s => !s.ForceLogout)
                    .OrderByDescending(s => s.LastActivity)
                    .Select(Clone)
                    .ToArray();
            }
        }

        public static void RegisterCurrentRequest()
        {
            var context = HttpContext.Current;
            if (context == null || context.Session == null || context.User == null || !context.User.Identity.IsAuthenticated)
                return;

            var sessionId = context.Session.SessionID;
            var userName = context.User.Identity.Name;
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(userName))
                return;

            lock (SyncRoot)
            {
                LiveSessionInfo session;
                var added = false;
                if (!Sessions.TryGetValue(sessionId, out session))
                {
                    session = new LiveSessionInfo
                    {
                        SessionId = sessionId,
                        UserName = userName,
                        UserHostAddress = context.Request.UserHostAddress,
                        UserAgent = context.Request.UserAgent,
                        LoginTime = DateTime.Now
                    };
                    Sessions[sessionId] = session;
                    added = true;
                }

                session.UserName = userName;
                session.LastActivity = DateTime.Now;
                if (added)
                    NotifyChanged();
            }
        }

        public static void RemoveCurrentSession()
        {
            var context = HttpContext.Current;
            if (context != null && context.Session != null)
                RemoveSession(context.Session.SessionID);
        }

        public static void RemoveSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return;

            lock (SyncRoot)
                if (Sessions.Remove(sessionId))
                    NotifyChanged();
        }

        public static bool IsCurrentSessionForcedOut()
        {
            var context = HttpContext.Current;
            if (context == null)
                return false;

            var sessionId = GetCurrentSessionId(context);
            if (string.IsNullOrEmpty(sessionId))
                return false;

            lock (SyncRoot)
            {
                LiveSessionInfo session;
                return Sessions.TryGetValue(sessionId, out session) && session.ForceLogout;
            }
        }

        public static bool ForceLogout(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return false;

            var forcedSessionIds = new List<string>();
            lock (SyncRoot)
            {
                LiveSessionInfo session;
                if (!Sessions.TryGetValue(sessionId, out session))
                    return false;
                session.ForceLogout = true;
                forcedSessionIds.Add(session.SessionId);
            }
            NotifyForcedLogout(forcedSessionIds);
            NotifyChanged();
            return true;
        }

        public static int ForceLogoutUser(string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return 0;

            var forcedSessionIds = new List<string>();
            lock (SyncRoot)
            {
                var count = 0;
                foreach (var session in Sessions.Values.Where(s => string.Equals(s.UserName, userName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!session.ForceLogout)
                    {
                        session.ForceLogout = true;
                        forcedSessionIds.Add(session.SessionId);
                        count++;
                    }
                }
            }
            if (forcedSessionIds.Count > 0)
            {
                NotifyForcedLogout(forcedSessionIds);
                NotifyChanged();
            }
            return forcedSessionIds.Count;
        }

        public static int ForceLogoutAll()
        {
            var forcedSessionIds = new List<string>();
            lock (SyncRoot)
            {
                var count = 0;
                foreach (var session in Sessions.Values)
                {
                    if (!session.ForceLogout)
                    {
                        session.ForceLogout = true;
                        forcedSessionIds.Add(session.SessionId);
                        count++;
                    }
                }
            }
            if (forcedSessionIds.Count > 0)
            {
                NotifyForcedLogout(forcedSessionIds);
                NotifyChanged();
            }
            return forcedSessionIds.Count;
        }

        public static bool EnforceCurrentRequest(HttpContext context)
        {
            if (context == null || context.User == null || !context.User.Identity.IsAuthenticated)
                return false;

            RegisterBySessionCookie(context);
            if (!IsCurrentSessionForcedOut())
                return false;

            FormsAuthentication.SignOut();
            context.User = null;
            return true;
        }

        private static void RegisterBySessionCookie(HttpContext context)
        {
            var sessionId = GetCurrentSessionId(context);
            var userName = context.User.Identity.Name;
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(userName))
                return;

            lock (SyncRoot)
            {
                LiveSessionInfo session;
                var added = false;
                if (!Sessions.TryGetValue(sessionId, out session))
                {
                    session = new LiveSessionInfo
                    {
                        SessionId = sessionId,
                        UserName = userName,
                        UserHostAddress = context.Request.UserHostAddress,
                        UserAgent = context.Request.UserAgent,
                        LoginTime = DateTime.Now
                    };
                    Sessions[sessionId] = session;
                    added = true;
                }
                session.LastActivity = DateTime.Now;
                if (added)
                    NotifyChanged();
            }
        }

        private static string GetCurrentSessionId(HttpContext context)
        {
            if (context.Session != null)
                return context.Session.SessionID;

            var cookie = context.Request.Cookies["ASP.NET_SessionId"];
            return cookie == null ? null : cookie.Value;
        }

        private static void RemoveExpiredSessions()
        {
            var timeout = HttpContext.Current == null || HttpContext.Current.Session == null ? 20 : HttpContext.Current.Session.Timeout;
            var cutoff = DateTime.Now.AddMinutes(-timeout);
            foreach (var sessionId in Sessions.Values.Where(s => s.LastActivity < cutoff).Select(s => s.SessionId).ToList())
                Sessions.Remove(sessionId);
        }

        private static void NotifyChanged()
        {
            try
            {
                LiveSessionBroadcaster.Broadcast();
            }
            catch
            {
            }
        }

        private static void NotifyForcedLogout(IEnumerable<string> sessionIds)
        {
            try
            {
                LiveSessionBroadcaster.ForceLogout(sessionIds.ToArray());
            }
            catch
            {
            }
        }

        private static LiveSessionInfo Clone(LiveSessionInfo session)
        {
            return new LiveSessionInfo
            {
                SessionId = session.SessionId,
                UserName = session.UserName,
                UserHostAddress = session.UserHostAddress,
                UserAgent = session.UserAgent,
                LoginTime = session.LoginTime,
                LastActivity = session.LastActivity,
                ForceLogout = session.ForceLogout
            };
        }
    }
}
