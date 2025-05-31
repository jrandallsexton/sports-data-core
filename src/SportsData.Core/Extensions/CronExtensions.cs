using NCrontab;

namespace SportsData.Core.Extensions
{
    public static class CronExtensions
    {
        public static bool IsValidCron(this string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return false;

            try
            {
                CrontabSchedule.Parse(expression);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
