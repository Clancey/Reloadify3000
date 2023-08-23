using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reloadify
{
	public static class DictionaryHelper
	{
		public static T GetValue<T>(object obj, string key, Dictionary<object, Dictionary<string, object>> values, Dictionary<string, object> defaults)
		{

			if (!values.TryGetValue(obj, out var data))
			{
				data = values[obj] = new();
			}
			if (data.TryGetValue(key, out var value) && value is T t)
			{
				return t;
			}
			if (defaults.TryGetValue(key, out  value) && value is T dt)
			{
				return dt;
			}
			return default;
		}
		public static void SetValue(object obj, string key, object value, Dictionary<object, Dictionary<string, object>> values, Dictionary<string, object> defaults)
		{

			if (!values.TryGetValue(obj, out var data))
			{
				data = values[obj] = new(defaults);
			}
			data[key] = value;
		}
	}
}
