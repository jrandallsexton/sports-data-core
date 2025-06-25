using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SportsData.Core.Common
{
    public enum ContestStatus
    {
        Undefined = 0,
        Canceled = 1,
        Completed = 2,
        Delayed = 3,
        Ongoing = 4,
        Postponed = 5,
        Scheduled = 6,
        Suspended = 7
    }
}
