using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Text;

namespace Akka.Persistence.MongoDb.Tests
{
    public class JournalTestActor : UntypedPersistentActor
    {
        public static Props Props(string persistenceId) => Actor.Props.Create(() => new JournalTestActor(persistenceId));

        public sealed class DeleteCommand
        {
            public DeleteCommand(long toSequenceNr)
            {
                ToSequenceNr = toSequenceNr;
            }

            public long ToSequenceNr { get; }
        }

        public JournalTestActor(string persistenceId)
        {
            PersistenceId = persistenceId;
        }

        public override string PersistenceId { get; }

        protected override void OnRecover(object message)
        {
        }

        protected override void OnCommand(object message)
        {
            switch (message) {
                case DeleteCommand delete:
                    DeleteMessages(delete.ToSequenceNr);
                    Sender.Tell($"{delete.ToSequenceNr}-deleted");
                    break;
                case string cmd:
                    var sender = Sender;
                    Persist(cmd, e => sender.Tell($"{e}-done"));
                    break;
            }
        }
    }
}
