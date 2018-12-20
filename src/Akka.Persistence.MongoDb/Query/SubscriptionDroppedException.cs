using Akka.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Akka.Persistence.MongoDb.Query
{
    public class SubscriptionDroppedException : Exception, IDeadLetterSuppression
    {

        public SubscriptionDroppedException() : this("Unknown error", null)
        {

        }

        public SubscriptionDroppedException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}
