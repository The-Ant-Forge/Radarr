using System;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Notifications.Slack
{
    public class SlackException : NzbDroneException
    {
        public SlackException(string message)
            : base(message)
        {
        }

        public SlackException(string message, Exception innerException, params object[] args)
            : base(message, innerException, args)
        {
        }
    }
}
