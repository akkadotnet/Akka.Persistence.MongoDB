//-----------------------------------------------------------------------
// <copyright file="FullTypeNameDiscriminatorConvention.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

namespace Akka.Persistence.MongoDb
{
    /// <summary>
    /// Our discriminator is the type's full name with the assembly name.
    /// Additional assembly information is excluded for forward compatibility.
    /// </summary>
    class FullTypeNameDiscriminatorConvention : StandardDiscriminatorConvention
    {
        public static readonly FullTypeNameDiscriminatorConvention Instance = new FullTypeNameDiscriminatorConvention("_t");

        public FullTypeNameDiscriminatorConvention(string element) : base(element) { }

        /// <summary>
        /// Our discriminator is the full type name with the assembly name.
        /// Additional assembly information is excluded for forward compatibility.
        /// </summary>
        /// <param name="nominalType"></param>
        /// <param name="actualType"></param>
        /// <returns>full type name with the simple assembly name</returns>
        public override BsonValue GetDiscriminator(Type nominalType, Type actualType)
        {
            var assemblyName = actualType.GetTypeInfo().Assembly.FullName.Split(',')[0];
            return $"{actualType.FullName}, {assemblyName}";
        }
    }
}
