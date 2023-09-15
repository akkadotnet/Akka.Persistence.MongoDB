//-----------------------------------------------------------------------
// <copyright file="FullTypeNameObjectSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// Represents a serializer for objects.
    /// </summary>
    class FullTypeNameObjectSerializer : ObjectSerializer
    {
        protected readonly IDiscriminatorConvention DiscriminatorConvention = FullTypeNameDiscriminatorConvention.Instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTypeNameObjectSerializer"/> class.
        /// </summary>
        public FullTypeNameObjectSerializer() : base(FullTypeNameDiscriminatorConvention.Instance) { }

        /// <summary>
        /// Deserializes a value.
        /// </summary>
        public override object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;

            if (BsonType.Document == bsonReader.GetCurrentBsonType())
            {
                RegisterNewTypesToDiscriminator(DiscriminatorConvention.GetActualType(bsonReader, typeof(object)));
            }

            return base.Deserialize(context, args);
        }

        /// <summary>
        /// Serializes a value.
        /// </summary>
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            if (value != null)
            {
                // auto-register new types with MongoDB on serialization, using their full assembly name
                RegisterNewTypesToDiscriminator(value.GetType());
            }

            base.Serialize(context, args, value);
        }

        /// <summary>
        /// If the type is not registered, attach it to our discriminator
        /// </summary>
        /// <param name="actualType">the type to examine</param>
        protected void RegisterNewTypesToDiscriminator(Type actualType)
        {
            // we've detected a new concrete type that isn't registered in MongoDB's serializer
            if (actualType != typeof(object) && !actualType.GetTypeInfo().IsInterface && !BsonSerializer.IsTypeDiscriminated(actualType))
            {
                try
                {
                    BsonSerializer.RegisterDiscriminatorConvention(actualType, DiscriminatorConvention);
                    BsonSerializer.RegisterDiscriminator(actualType, DiscriminatorConvention.GetDiscriminator(typeof(object), actualType));
                }
                catch (BsonSerializationException)
                {
                    // the MongoDB driver library has no nice mechanism for checking if a discriminator convention is registerd.
                    // The "Lookup" logic tends to define a default if it doesn't exist.
                    // So we're forced to eat the "duplicate registration" exception.
                }
            }
        }
    }
}
