using System;
using System.Collections.Generic;

namespace SilentNotes.Workers;

public static class DictionaryExtensions
{
	public static bool TryGetKey<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TValue value, out TKey key, IEqualityComparer<TValue> comparer = null)
	{
		if (dictionary == null)
		{
			throw new ArgumentNullException("dictionary");
		}
		IEqualityComparer<TValue> equalityComparer = comparer ?? EqualityComparer<TValue>.Default;
		foreach (KeyValuePair<TKey, TValue> item in dictionary)
		{
			if (equalityComparer.Equals(value, item.Value))
			{
				key = item.Key;
				return true;
			}
		}
		key = default(TKey);
		return false;
	}
}
