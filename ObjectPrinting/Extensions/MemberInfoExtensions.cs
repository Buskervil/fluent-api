﻿using System;
using System.Reflection;

namespace ObjectPrinting.Extensions
{
    internal static class MemberInfoExtensions
    {
        public static object GetValue(this MemberInfo memberInfo, object obj)
        {
            return memberInfo switch
            {
                PropertyInfo propertyInfo => propertyInfo.GetValue(obj),
                FieldInfo fieldInfo => fieldInfo.GetValue(obj),
                _ => throw new InvalidOperationException()
            };
        }

        public static Type GetMemberType(this MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                PropertyInfo propertyInfo => propertyInfo.PropertyType,
                FieldInfo fieldInfo => fieldInfo.FieldType,
                _ => throw new InvalidOperationException()
            };
        }
    }
}