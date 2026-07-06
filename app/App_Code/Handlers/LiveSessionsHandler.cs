using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using Newtonsoft.Json.Linq;
using yoshuwa.Security;

namespace yoshuwa.Handlers
{
    public class LiveSessions : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            EnsureAdministrator(context);
            var message = HandleAction(context);
            if (IsPartialRequest(context))
            {
                RenderPartial(context, message);
                return;
            }
            if (context.Request.HttpMethod == "GET")
            {
                context.Response.Redirect(VirtualPathUtility.ToAbsolute("~/pages/live-sessions"), true);
                return;
            }
            Render(context, message);
        }

        private string HandleAction(HttpContext context)
        {
            if (context.Request.HttpMethod != "POST")
                return null;

            var action = context.Request.Form["action"];
            if (action == "force-all")
            {
                var count = LiveSessionManager.ForceLogoutAll();
                SignOutIfCurrentSessionWasForced(context);
                return string.Format("Force logout was requested for {0} live session{1}.", count, count == 1 ? string.Empty : "s");
            }
            if (action == "force-user")
            {
                var count = LiveSessionManager.ForceLogoutUser(context.Request.Form["user"]);
                SignOutIfCurrentSessionWasForced(context);
                return count > 0
                    ? string.Format("Force logout was requested for {0} live session{1} belonging to the selected user.", count, count == 1 ? string.Empty : "s")
                    : "The selected user has no active tracked sessions.";
            }
            return null;
        }

        private void Render(HttpContext context, string message)
        {
            var sessions = LiveSessionManager.GetActiveSessions();
            var userCount = sessions.Select(s => s.UserName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            context.Response.ContentType = "text/html; charset=utf-8";
            var html = new StringBuilder();
            html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Live Sessions</title>");
            html.Append("<style>body{font-family:'Segoe UI',Arial,sans-serif;margin:0;color:#202124;background:#f6f8fb}.page{max-width:1180px;margin:0 auto;padding:32px 24px}.header{display:flex;align-items:center;justify-content:space-between;gap:20px;margin-bottom:20px}h1{font-size:28px;font-weight:600;margin:0}.count{font-size:16px;margin-top:8px;color:#5f6368}.toolbar{display:flex;gap:10px;flex-wrap:wrap}.button{border:1px solid #c8d1dc;background:#fff;color:#1f2937;border-radius:6px;padding:8px 12px;font:inherit;cursor:pointer}.danger{background:#b3261e;border-color:#b3261e;color:#fff}.message{margin:16px 0;color:#0b57d0}.grid{width:100%;border-collapse:collapse;background:#fff;border:1px solid #d9e0e8}.grid th,.grid td{padding:10px 12px;border-bottom:1px solid #e6ebf1;text-align:left;vertical-align:top;font-size:14px}.grid th{background:#eef2f7;font-weight:600}.muted{color:#687281;max-width:420px;word-break:break-word}@media(max-width:760px){.header{align-items:flex-start;flex-direction:column}.grid{display:block;overflow-x:auto}}</style>");
            html.Append("</head><body><div class=\"page\"><div class=\"header\"><div><h1>Live Sessions</h1><div class=\"count\">");
            html.Append(GetCountText(userCount, sessions.Length));
            html.Append("</div></div><div class=\"toolbar\"><form method=\"get\"><button class=\"button\" type=\"submit\">Refresh</button></form><form method=\"post\"><input type=\"hidden\" name=\"action\" value=\"force-all\"><button class=\"button danger\" type=\"submit\" onclick=\"return confirm('Force logout all live sessions?');\">Force Logout All</button></form></div></div>");
            if (!string.IsNullOrEmpty(message))
                html.Append("<div class=\"message\">" + HttpUtility.HtmlEncode(message) + "</div>");
            html.Append(BuildTableHtml(sessions, true));
            html.Append("</div></body></html>");
            context.Response.Write(html.ToString());
        }

        private void RenderPartial(HttpContext context, string message)
        {
            var sessions = LiveSessionManager.GetActiveSessions();
            var userCount = sessions.Select(s => s.UserName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var json = new JObject();
            json["countText"] = GetCountText(userCount, sessions.Length);
            json["message"] = string.IsNullOrEmpty(message) ? string.Empty : HttpUtility.HtmlEncode(message);
            json["tableHtml"] = BuildTableHtml(sessions, false);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Write(json.ToString(Newtonsoft.Json.Formatting.None));
        }

        private string BuildTableHtml(LiveSessionInfo[] sessions, bool includeForms)
        {
            var html = new StringBuilder();
            html.Append("<table class=\"grid\"><thead><tr><th>User</th><th>IP Address</th><th>Login Time</th><th>Last Activity</th><th>Browser</th><th>Action</th></tr></thead><tbody>");
            if (sessions.Length == 0)
                html.Append("<tr><td colspan=\"6\">No live sessions are currently tracked.</td></tr>");
            foreach (var session in sessions)
            {
                html.Append("<tr>");
                html.AppendCell(session.UserName);
                html.AppendCell(session.UserHostAddress);
                html.AppendCell(session.LoginTime.ToString("g"));
                html.AppendCell(session.LastActivity.ToString("g"));
                html.AppendCell(session.UserAgent, "muted");
                html.Append("<td>");
                if (includeForms)
                {
                    html.Append("<form method=\"post\"><input type=\"hidden\" name=\"action\" value=\"force-user\"><input type=\"hidden\" name=\"user\" value=\"");
                    html.Append(HttpUtility.HtmlAttributeEncode(session.UserName));
                    html.Append("\"><button class=\"button danger\" type=\"submit\" onclick=\"return confirm('Force logout this user from all tracked live sessions?');\">Force Logout User</button></form>");
                }
                else
                {
                    html.Append("<button class=\"danger\" type=\"button\" data-force-user=\"");
                    html.Append(HttpUtility.HtmlAttributeEncode(session.UserName));
                    html.Append("\">Force Logout User</button>");
                }
                html.Append("</td></tr>");
            }
            html.Append("</tbody></table>");
            return html.ToString();
        }

        private string GetCountText(int userCount, int sessionCount)
        {
            return string.Format("{0} user{1}, {2} live session{3} logged in", userCount, userCount == 1 ? string.Empty : "s", sessionCount, sessionCount == 1 ? string.Empty : "s");
        }

        private bool IsPartialRequest(HttpContext context)
        {
            return string.Equals(context.Request.QueryString["partial"], "true", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureAdministrator(HttpContext context)
        {
            if (context.User == null || context.User.Identity == null || !context.User.Identity.IsAuthenticated)
                FormsAuthentication.RedirectToLoginPage();
            if (!context.User.IsInRole("Administrators"))
                throw new HttpException(403, "Only administrators can manage live sessions.");
        }

        private void SignOutIfCurrentSessionWasForced(HttpContext context)
        {
            if (!LiveSessionManager.IsCurrentSessionForcedOut())
                return;
            FormsAuthentication.SignOut();
            context.Session.Abandon();
            context.Response.Redirect(FormsAuthentication.LoginUrl, true);
        }
    }

    internal static class HtmlTableExtensions
    {
        public static void AppendCell(this StringBuilder builder, string value, string cssClass = null)
        {
            builder.Append("<td");
            if (!string.IsNullOrEmpty(cssClass))
                builder.Append(" class=\"").Append(HttpUtility.HtmlAttributeEncode(cssClass)).Append("\"");
            builder.Append(">").Append(HttpUtility.HtmlEncode(value)).Append("</td>");
        }
    }
}
