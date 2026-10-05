// Compat copy of SilentNotes.AllPlatforms\Ioc.cs for .NET 4.0.
// Differences to the frozen original:
// - The class is public (the Windows clients use it).
// - The unusable "using Microsoft.Extensions.DependencyInjection;" is removed instead of
//   being satisfied by the empty namespace shim (kept only for the frozen original).
using System;
using System.Collections.Generic;

namespace SilentNotes
{
    /// <summary>
    /// Global Ioc service provider.
    /// <example><code>
    /// var myService = Ioc.Instance.GetService&lt;IMyService&gt;();
    /// </code></example>
    /// </summary>
    public class Ioc : IServiceProvider
    {
        private IServiceProvider _serviceProvider;
        private Dictionary<Type, object> _injectedServices = new Dictionary<Type, object>();

        /// <summary>
        /// Gets a static instance of the Ioc service provider.
        /// </summary>
        public static Ioc Instance { get; } = new Ioc();

        /// <summary>
        /// The application can register its service provider to make it globally available.
        /// </summary>
        /// <param name="serviceProvider">The apps service provider.</param>
        public void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public object GetService(Type serviceType)
        {
            // Check if there is an injected service first
            object instance;
            if (_injectedServices.TryGetValue(serviceType, out instance))
                return instance;

            if (_serviceProvider == null)
                throw new Exception("Ioc is not initialized.");
            return _serviceProvider.GetService(serviceType);
        }

        /// <summary>
        /// Generic version of <see cref="GetService(Type)"/>.
        /// </summary>
        /// <typeparam name="T">The requested service type.</typeparam>
        /// <returns>A service object of type serviceType.</returns>
        public T GetService<T>() where T : class
        {
            return (T)GetService(typeof(T));
        }

        /// <summary>
        /// The "MainLayout.razor" can register the scoped services, which it got by injection.
        /// This guarantees that the correct instances are available in the service provider.
        /// </summary>
        /// <remarks>
        /// This is a workaround for scoped services, which are otherwise recreated and are
        /// different from the injected ones.
        /// </remarks>
        /// <typeparam name="T">The type of the service to register.</typeparam>
        /// <param name="instance">The instance gotten as injected service.</param>
        /// <returns>Returns itself for a fluent declaration of services.</returns>
        public Ioc AddInjected<T>(T instance) where T : class
        {
            AddInjected(typeof(T), instance);
            return this;
        }

        private void AddInjected(Type serviceType, object instance)
        {
            _injectedServices.Add(serviceType, instance);
        }

        /// <summary>
        /// The "MainLayout.razor" can clear all previously registeres scoped services.
        /// </summary>
        /// <returns>Returns itself for a fluent declaration of services.</returns>
        public Ioc ClearInjected()
        {
            _injectedServices.Clear();
            return this;
        }
    }
}
