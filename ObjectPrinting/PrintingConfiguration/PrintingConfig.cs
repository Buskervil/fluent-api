﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ObjectPrinting.Extensions;

namespace ObjectPrinting.PrintingConfiguration
{
    public class PrintingConfig<TOwner>
    {
        private readonly Dictionary<MemberInfo, Delegate> alternativeMemberSerializers = new();
        private readonly Dictionary<Type, Delegate> alternativeTypeSerializers = new();
        private readonly HashSet<MemberInfo> excludingMembers = new();
        private readonly HashSet<Type> excludingTypes = new();

        private readonly HashSet<Type> finalTypes = new()
        {
            typeof(int),
            typeof(double),
            typeof(float),
            typeof(string),
            typeof(DateTime),
            typeof(TimeSpan),
            typeof(Guid)
        };

        private readonly HashSet<object> visitedMembers = new();

        public void AddAlternativeTypeSerializer<TPropType>(Type type, Func<TPropType, string> serializer)
        {
            alternativeTypeSerializers.TryAdd(type, serializer);
        }

        public void AddAlternativeMemberSerializer<TPropType>(MemberInfo memberInfo, Func<TPropType, string> serializer)
        {
            alternativeMemberSerializers.TryAdd(memberInfo, serializer);
        }

        public TypePrintingConfig<TOwner, TPropType> Printing<TPropType>()
        {
            return new TypePrintingConfig<TOwner, TPropType>(this);
        }

        public PropertyPrintingConfig<TOwner, TPropType> Printing<TPropType>(
            Expression<Func<TOwner, TPropType>> memberSelector)
        {
            return new PropertyPrintingConfig<TOwner, TPropType>(this, GetMemberInfo(memberSelector));
        }

        public PrintingConfig<TOwner> Excluding<TPropType>()
        {
            excludingTypes.Add(typeof(TPropType));
            return this;
        }

        public PrintingConfig<TOwner> Excluding<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
        {
            var memberInfo = GetMemberInfo(memberSelector);
            excludingMembers.Add(memberInfo);
            return this;
        }

        public string PrintToString(TOwner obj)
        {
            return PrintToString(obj, 0);
        }

        private string PrintToString(object obj, int nestingLevel)
        {
            if (obj == null)
                return "null" + Environment.NewLine;

            if (visitedMembers.Any(m => ReferenceEquals(m, obj)))
                return $"Cyclic reference detected{Environment.NewLine}";

            visitedMembers.Add(obj);

            if (finalTypes.Contains(obj.GetType()))
                return obj + Environment.NewLine;

            if (obj is IEnumerable collection)
                return PrintCollection(collection);

            return PrintMember(obj, nestingLevel);
        }

        private string PrintMember(object obj, int nestingLevel)
        {
            var indentation = new string('\t', nestingLevel + 1);
            var sb = new StringBuilder();

            var type = obj.GetType();
            sb.AppendLine(type.Name);

            var members = type.GetProperties().Concat<MemberInfo>(type.GetFields());
            foreach (var memberInfo in members
                .Where(x => !excludingTypes.Contains(x.GetMemberType()))
                .Where(x => !excludingMembers.Contains(x)))
            {
                var serialized = TryGetAlternativeSerializer(memberInfo, out var serializer)
                    ? serializer.DynamicInvoke(memberInfo.GetValue(obj)) + Environment.NewLine
                    : PrintToString(memberInfo.GetValue(obj), nestingLevel + 1);

                sb.Append(indentation + memberInfo.Name + " = " + serialized);
            }

            return sb.ToString();
        }

        private static string PrintCollection(IEnumerable collection)
        {
            if (collection is IDictionary dict)
                return PrintDictionary(dict);
            
            var builder = new StringBuilder($"{collection.GetType().Name}{Environment.NewLine}");
            foreach (var member in collection)
                builder.Append(member.PrintToString());
            
            return builder.ToString();
        }

        private static string PrintDictionary(IDictionary dict)
        {
            var builder = new StringBuilder($"{dict.GetType().Name}{Environment.NewLine}");
            foreach (var key in dict.Keys)
            {
                builder.Append($"{key.PrintToString().Trim()} : ");
                builder.Append($"{dict[key].PrintToString()}");
            }
            return builder.ToString();
        }

        private static MemberInfo GetMemberInfo<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
        {
            var memberExpression = memberSelector.Body as MemberExpression;
            return memberExpression?.Member;
        }

        private bool TryGetAlternativeSerializer(MemberInfo memberInfo, out Delegate serializer)
        {
            if (alternativeMemberSerializers.TryGetValue(memberInfo, out serializer))
                return true;

            var memberType = memberInfo.GetMemberType();
            if (alternativeTypeSerializers.TryGetValue(memberType, out serializer))
                return true;

            serializer = null;
            return false;
        }
    }
}