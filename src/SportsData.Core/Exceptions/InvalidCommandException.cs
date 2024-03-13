using System;

namespace SportsData.Core.Exceptions
{
    public class InvalidCommandException : Exception
    {
        public InvalidCommandException() { }

        public InvalidCommandException(string message) :
            base(message)
        { }
    }
}
