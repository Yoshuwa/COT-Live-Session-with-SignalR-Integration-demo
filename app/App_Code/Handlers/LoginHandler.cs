using System;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using yoshuwa.Security;
using yoshuwa.Services;

namespace yoshuwa.Handlers
{
    public class Login : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (context.Request.HttpMethod == "POST")
                SignIn(context);
            else
                Render(context, null);
        }

        private void SignIn(HttpContext context)
        {
            var userName = (context.Request.Form["username"] ?? string.Empty).Trim();
            var password = context.Request.Form["password"] ?? string.Empty;
            var remember = string.Equals(context.Request.Form["remember"], "on", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                Render(context, "Enter a user name and password.");
                return;
            }

            var result = ApplicationServices.Login(userName, password, remember);
            if (result is bool && !(bool)result)
            {
                Render(context, "The user name or password is incorrect.");
                return;
            }

            LiveSessionManager.RemoveCurrentSession();

            var returnUrl = context.Request.QueryString["ReturnUrl"];
            if (string.IsNullOrEmpty(returnUrl) || !(UrlIsLocal(returnUrl)))
                returnUrl = VirtualPathUtility.ToAbsolute("~/");
            context.Response.Redirect(returnUrl, true);
        }

        private void Render(HttpContext context, string error)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Write("<!doctype html><html><head><meta charset=\"utf-8\"><title>Sign In</title>");
            context.Response.Write("<style>body{font-family:'Segoe UI',Arial,sans-serif;margin:0;color:#202124;background:#f6f8fb}.shell{min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px}.panel{width:100%;max-width:380px;background:#fff;border:1px solid #d9e0e8;border-radius:8px;padding:28px;box-shadow:0 12px 28px rgba(31,41,55,.08)}h1{margin:0 0 20px;font-size:26px;font-weight:600}label{display:block;margin:14px 0 6px;font-weight:600}input[type=text],input[type=password]{width:100%;box-sizing:border-box;border:1px solid #c8d1dc;border-radius:6px;padding:10px 12px;font:inherit}.remember{display:flex;align-items:center;gap:8px;margin:16px 0}.button{width:100%;border:1px solid #1a73e8;background:#1a73e8;color:#fff;border-radius:6px;padding:10px 12px;font:inherit;cursor:pointer}.error{color:#b3261e;margin-top:14px}</style>");
            context.Response.Write("</head><body><form method=\"post\"><div class=\"shell\"><div class=\"panel\"><h1>Sign In</h1>");
            context.Response.Write("<label for=\"username\">User name</label><input id=\"username\" name=\"username\" type=\"text\" autofocus>");
            context.Response.Write("<label for=\"password\">Password</label><input id=\"password\" name=\"password\" type=\"password\">");
            context.Response.Write("<label class=\"remember\"><input name=\"remember\" type=\"checkbox\"><span>Keep me signed in</span></label>");
            context.Response.Write("<button class=\"button\" type=\"submit\">Sign In</button>");
            if (!string.IsNullOrEmpty(error))
                context.Response.Write("<div class=\"error\">" + HttpUtility.HtmlEncode(error) + "</div>");
            context.Response.Write("</div></div></form></body></html>");
        }

        private bool UrlIsLocal(string url)
        {
            return !string.IsNullOrEmpty(url) && (url[0] == '/' || url.StartsWith("~/", StringComparison.Ordinal));
        }
    }
}
