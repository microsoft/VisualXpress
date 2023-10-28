// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Microsoft.VisualXpress
{
	public static class Converters
	{
		public static Int32 ToInt32(object value, Int32 defaultValue = 0)
		{
			return ParseType(value, defaultValue, Int32.TryParse);
		}

		public static Int64 ToInt64(object value, Int64 defaultValue = 0)
		{
			return ParseType(value, defaultValue, Int64.TryParse);
		}

		public static Single ToFloat(object value, Single defaultValue = 0)
		{
			return ParseType(value, defaultValue, Single.TryParse);
		}

		public static Double ToDouble(object value, Double defaultValue = 0)
		{
			return ParseType(value, defaultValue, Double.TryParse);
		}

		public static string ToString(object value, string defaultValue = null)
		{
			return ParseType(value, defaultValue, (string input, out string result) => { result = input; return true; });
		}

		public static TEnum ToEnum<TEnum>(object value, TEnum defaultValue = default(TEnum)) where TEnum : struct
		{
			return ParseType(value, defaultValue, Enum.TryParse<TEnum>);
		}

		public delegate bool TryParseType<T>(string value, out T result);

		public static T ParseType<T>(object value, T defaultValue, TryParseType<T> tryParse)
		{
			if (value == null)
				return defaultValue;
			if (value is T)
				return (T)value;
			
			string valueText = value as string;
			if (valueText == null)
				valueText = value.ToString();
			
			if (String.IsNullOrWhiteSpace(valueText) == false)
			{
				T result;
				if (tryParse(valueText, out result))
					return result;
			}
			return defaultValue;
		}

		public static T ParseType<T>(object value, T defaultValue)
		{
			switch (Type.GetTypeCode(typeof(T)))
			{
				case TypeCode.Int32:
					return (T)(object)ToInt32(value, (Int32)(object)defaultValue);
				case TypeCode.Int64:
					return (T)(object)ToInt64(value, (Int64)(object)defaultValue);
				case TypeCode.Single:
					return (T)(object)ToFloat(value, (Single)(object)defaultValue);
				case TypeCode.Double:
					return (T)(object)ToDouble(value, (Double)(object)defaultValue);
				case TypeCode.String:
					return (T)(object)ToString(value, (string)(object)defaultValue);
			}
			return defaultValue;
		}
	}
}

