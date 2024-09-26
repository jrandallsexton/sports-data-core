using Hangfire.Dashboard;

namespace SportsData.Provider
{
    public class DashboardAuthFilter : IDashboardAuthorizationFilter
    {
        /// <summary>
        /// https://docs.hangfire.io/en/latest/configuration/using-dashboard.html#configuring-authorization
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool Authorize(DashboardContext context)
        {
            // TODO: Implement

            //var httpContext = context.GetHttpContext();

            return true;
        }
    }
}
