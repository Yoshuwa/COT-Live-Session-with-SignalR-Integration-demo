<%@ Application Language="C#" %>

<script runat="server">
void Application_Start(object sender, EventArgs e)
{
    // *********************************************************************************************
    // You may get a compilation error message if you change the namespace of the project.
    // This file will not be re-generated. Namespace "yoshuwa" must be changed manually.
    // *********************************************************************************************
    // Fires on application startup
    yoshuwa.Services.ApplicationServices.Start();
}

void Application_End(object sender, EventArgs e)
{
    // Fires on application shutdown
    yoshuwa.Services.ApplicationServices.Stop();
}

void Application_Error(object sender, EventArgs e)
{
    // Fires when an unhandled error occurs
    yoshuwa.Services.ApplicationServices.Error();
}

void Application_AuthenticateRequest(object sender, EventArgs e)
{
    yoshuwa.Services.ApplicationServices.Current.AuthenticateRequest(Context);
}

void Application_AuthorizeRequest(object sender, EventArgs e)
{
    if (RequiresLoginRedirect())
    {
        var returnUrl = Server.UrlEncode(Request.RawUrl);
        Response.Redirect(System.Web.VirtualPathUtility.ToAbsolute("~/Login.ashx") + "?ReturnUrl=" + returnUrl, true);
    }
}

void Application_AcquireRequestState(object sender, EventArgs e)
{
    yoshuwa.Services.ApplicationServices.TrackLiveSession();
}

void Session_Start(object sender, EventArgs e)
{
    // Fires when a new session is started
    yoshuwa.Services.ApplicationServices.SessionStart();
}

void Session_End(object sender, EventArgs e)
{
    // Fires when a session ends.
    // Note: The Session_End event is raised only when the sessionstate mode
    // is set to InProc in the Web.config file. If session mode is set to StateServer
    // or SQLServer, the event is not raised.
    yoshuwa.Services.ApplicationServices.SessionStop();
}

bool RequiresLoginRedirect()
{
    if (!yoshuwa.Services.ApplicationServicesBase.AuthorizationIsSupported || Request.IsAuthenticated)
        return false;
    var path = Request.AppRelativeCurrentExecutionFilePath.ToLowerInvariant();
    if (path == "~/login.ashx" || path == "~/sessionstatus.ashx" || path.StartsWith("~/appservices/") || path.StartsWith("~/_invoke/") || path.StartsWith("~/signalr/"))
        return false;
    var extension = System.IO.Path.GetExtension(path);
    if (extension == ".css" || extension == ".js" || extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".gif" || extension == ".ico" || extension == ".woff" || extension == ".ttf")
        return false;
    return true;
}
</script>
