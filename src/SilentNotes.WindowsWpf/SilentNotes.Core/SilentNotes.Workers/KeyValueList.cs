using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

namespace SilentNotes.Workers;

public class KeyValueList<TKey, TValue> : List<KeyValueList<TKey, TValue>.Pair>
{
	[DebuggerDisplay("{Key} = {Value}")]
	public class Pair
	{
		[XmlElement(ElementName = "key")]
		public TKey Key { get; set; }

		[XmlElement(ElementName = "value")]
		public TValue Value { get; set; }
	}

	private readonly IEqualityComparer<TKey> _keyComparer;

	private readonly IEqualityComparer<TValue> _valueComparer;

	public TValue this[TKey key]
	{
		get
		{
			return GetValueOrDefault(key);
		}
		set
		{
			AddOrReplace(key, value);
		}
	}

	public KeyValueList()
		: this((IEqualityComparer<TKey>)null, (IEqualityComparer<TValue>)null)
	{
	}

	public KeyValueList(IEqualityComparer<TKey> keyComparer)
		: this(keyComparer, (IEqualityComparer<TValue>)null)
	{
	}

	public KeyValueList(IEqualityComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
	{
		_keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
		_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
	}

	public Pair GetByIndex(int index)
	{
		return base[index];
	}

	public void AddOrReplace(TKey key, TValue value)
	{
		Pair pair = FindByKey(key);
		if (pair != null)
		{
			pair.Value = value;
			return;
		}
		Add(new Pair
		{
			Key = key,
			Value = value
		});
	}

	public TValue GetValueOrDefault(TKey key)
	{
		TryGetValue(key, out var value);
		return value;
	}

	public bool TryGetValue(TKey key, out TValue value)
	{
		Pair pair = FindByKey(key);
		if (pair != null)
		{
			value = pair.Value;
			return true;
		}
		value = default(TValue);
		return false;
	}

	public bool TryGetKey(TValue value, out TKey key)
	{
		Pair pair = FindByValue(value);
		if (pair != null)
		{
			key = pair.Key;
			return true;
		}
		key = default(TKey);
		return false;
	}

	public bool ContainsKey(TKey key)
	{
		return FindByKey(key) != null;
	}

	public void RemoveByKey(TKey key)
	{
		RemoveAll((Pair item) => _keyComparer.Equals(key, item.Key));
	}

	protected Pair FindByKey(TKey key)
	{
		return this.FirstOrDefault((Pair item) => _keyComparer.Equals(key, item.Key));
	}

	protected Pair FindByValue(TValue value)
	{
		return this.FirstOrDefault((Pair item) => _valueComparer.Equals(value, item.Value));
	}
}
