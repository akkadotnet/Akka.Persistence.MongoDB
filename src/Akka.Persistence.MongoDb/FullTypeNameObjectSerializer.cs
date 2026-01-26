//-----------------------------------------------------------------------
// <copyright file="FullTypeNameObjectSerializer.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
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
    public class FullTypeNameObjectSerializer : ObjectSerializer
    {
        private static readonly ConcurrentDictionary<Type, bool> s_registeredTypes = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTypeNameObjectSerializer"/> class.
        /// </summary>
        public FullTypeNameObjectSerializer() : base(FullTypeNameDiscriminatorConvention.Instance, AllAllowedTypes) { }

        /// <summary>
        /// Deserializes a value.
        /// </summary>
        public override object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;

            if (BsonType.Document == bsonReader.GetCurrentBsonType())
            {
                RegisterNewTypesToDiscriminator(FullTypeNameDiscriminatorConvention.Instance.GetActualType(bsonReader, typeof(object)));
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
        /// If the type is not registered, attach it to our discriminator.
        /// This method can be called to pre-register types at startup to avoid
        /// lock contention during concurrent serialization operations.
        /// </summary>
        /// <param name="actualType">the type to examine</param>
        public static void RegisterNewTypesToDiscriminator(Type actualType)
        {
            if (actualType == typeof(object) || actualType.GetTypeInfo().IsInterface || BsonSerializer.IsTypeDiscriminated(actualType)
                || s_registeredTypes.ContainsKey(actualType))
            {
                return;
            }

            try
            {
                // we've likely detected a new concrete type that isn't registered in MongoDB's serializer
                BsonSerializer.RegisterDiscriminatorConvention(actualType, FullTypeNameDiscriminatorConvention.Instance);
                BsonSerializer.RegisterDiscriminator(actualType, FullTypeNameDiscriminatorConvention.Instance.GetDiscriminator(typeof(object), actualType));
                s_registeredTypes.TryAdd(actualType, true);
            }
            catch (BsonSerializationException)
            {
                // Ignore re-registration errors that may occur due to multiple concurrent registrations or
                // if library user has registered their own IDiscriminatorConvention for actualType.
                s_registeredTypes.TryAdd(actualType, true);
            }
        }
    }
}
