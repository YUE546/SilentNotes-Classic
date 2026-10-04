using System;
using System.Collections.Generic;

namespace SilentNotes.Services;

public class ServiceFactory<TKey, TServiceInterface> where TServiceInterface : class
{
	private Dictionary<TKey, Func<TServiceInterface>> _factoryFunctions;

	private Dictionary<TKey, TServiceInterface> _cachedSingletons;

	public bool CreateAsSingletons { get; }

	public ServiceFactory(bool createAsSingletons)
	{
		CreateAsSingletons = createAsSingletons;
		IEqualityComparer<TKey> equalityComparer;
		IEqualityComparer<TKey> equalityComparer2;
		if (typeof(TKey) == typeof(string))
		{
			equalityComparer = (equalityComparer2 = (IEqualityComparer<TKey>)StringComparer.InvariantCultureIgnoreCase);
		}
		else
		{
			IEqualityComparer<TKey> equalityComparer3 = EqualityComparer<TKey>.Default;
			equalityComparer = equalityComparer3;
		}
		equalityComparer2 = equalityComparer;
		_factoryFunctions = new Dictionary<TKey, Func<TServiceInterface>>(equalityComparer2);
		_cachedSingletons = (CreateAsSingletons ? new Dictionary<TKey, TServiceInterface>() : null);
	}

	public void Add(TKey key, Func<TServiceInterface> factoryFunction)
	{
		_factoryFunctions.Add(key, factoryFunction);
	}

	public TServiceInterface GetByKey(TKey key)
	{
		TServiceInterface value = null;
		if (CreateAsSingletons && _cachedSingletons.TryGetValue(key, out value))
		{
			return value;
		}
		try
		{
			Func<TServiceInterface> func = _factoryFunctions[key];
			value = func();
			if (CreateAsSingletons)
			{
				_cachedSingletons.Add(key, value);
			}
			return value;
		}
		catch (Exception innerException)
		{
			throw new ArgumentOutOfRangeException($"An instance of the interface [{typeof(TServiceInterface).Name}] for key [{key.ToString()}] could not be created.", innerException);
		}
	}
}
