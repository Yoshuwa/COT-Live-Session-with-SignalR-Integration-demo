# COT Live Session with SignalR Integration demo

## Implemented features

- ASP.NET Forms Authentication backed by SQL Server membership and roles.
- Seeded administrator, standard user, and ten test users.
- Administrator-only Live Sessions page inside the Code On Time application shell.
- Live count of signed-in users and active sessions.
- Session details for user name, IP address, login time, last activity, and browser.
- Force logout for a selected user.
- Force logout for all tracked users.
- SignalR-powered live refresh for the administrator Live Sessions page.
- Forced users are redirected back to the login page.
- Stale forced-session records are cleared after a successful login.
