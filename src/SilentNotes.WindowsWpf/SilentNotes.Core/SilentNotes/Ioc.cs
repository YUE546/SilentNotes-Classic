using System;
using System.Collections.Generic;

namespace SilentNotes;

public class Ioc : IServiceProvider
{
	private IServiceProvider _serviceProvider;

	private Dictionary<Type, object> _injectedServices = new Dictionary<Type, object>();

	public static Ioc Instance { get; } = new Ioc();

	public void Initialize(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public object GetService(Type serviceType)
	{
		if (_injectedServices.TryGetValue(serviceType, out var value))
		{
			return value;
		}
		if (_serviceProvider == null)
		{
			throw new Exception("Ioc is not initialized.");
		}
		return _serviceProvider.GetService(serviceType);
	}

	public T GetService<T>() where T : class
	{
		return (T)GetService(typeof(T));
	}

	public Ioc AddInjected<T>(T instance) where T : class
	{
		AddInjected(typeof(T), instance);
		return this;
	}

	private void AddInjected(Type serviceType, object instance)
	{
		_injectedServices.Add(serviceType, instance);
	}

	public Ioc ClearInjected()
	{
		_injectedServices.Clear();
		return this;
	}
}
