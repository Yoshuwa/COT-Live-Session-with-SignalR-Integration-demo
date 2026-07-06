using System.Security.Principal;
using System.Web;
using System.Web.Security;
using yoshuwa.Security;

namespace yoshuwa.Services
{
    public partial class ApplicationServices
    {
        public override bool AuthenticateRequest(HttpContext context)
        {
            var authCookie = context.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null && !string.IsNullOrEmpty(authCookie.Value))
                try
                {
                    var ticket = FormsAuthentication.Decrypt(authCookie.Value);
                    if (ValidateTicket(ticket))
                    {
                        context.User = new RolePrincipal(new FormsIdentity(ticket));
                        System.Threading.Thread.CurrentPrincipal = context.User;
                    }
                }
                catch
                {
                    FormsAuthentication.SignOut();
                }

            return LiveSessionManager.EnforceCurrentRequest(context);
        }

        protected override void UserSessionStart()
        {
            LiveSessionManager.RegisterCurrentRequest();
        }

        protected override void UserSessionStop()
        {
            LiveSessionManager.RemoveCurrentSession();
        }

        public override object AuthenticateUser(string username, string password, bool createPersistentCookie)
        {
            var result = base.AuthenticateUser(username, password, createPersistentCookie);
            if (!(result is bool) || (bool)result)
            {
                var identity = new FormsIdentity(new FormsAuthenticationTicket(username, createPersistentCookie, FormsAuthentication.Timeout.TotalMinutes > 0 ? (int)FormsAuthentication.Timeout.TotalMinutes : 30));
                HttpContext.Current.User = new RolePrincipal(identity);
                System.Threading.Thread.CurrentPrincipal = HttpContext.Current.User;
                LiveSessionManager.RegisterCurrentRequest();
            }
            return result;
        }

        public override void UserLogout()
        {
            LiveSessionManager.RemoveCurrentSession();
            base.UserLogout();
        }

        public override void CreateStandardMembershipAccounts()
        {
            RegisterStandardMembershipAccounts();
        }

        public static void TrackLiveSession()
        {
            LiveSessionManager.RegisterCurrentRequest();
        }
    }
}
