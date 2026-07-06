using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using yoshuwa.Security;

namespace yoshuwa.Handlers
{
    public class SessionStatus : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            if (context.User == null || context.User.Identity == null || !context.User.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = 401;
                context.Response.Write("{\"authenticated\":false}");
                return;
            }

            if (LiveSessionManager.IsCurrentSessionForcedOut())
            {
                FormsAuthentication.SignOut();
                context.Session.Abandon();
                context.Response.StatusCode = 401;
                context.Response.Write("{\"forced\":true}");
                return;
            }

            context.Response.Write("{\"authenticated\":true}");
        }
    }
}
