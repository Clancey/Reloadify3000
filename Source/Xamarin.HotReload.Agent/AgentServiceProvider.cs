using System;
using System.Collections.Generic;

using Xamarin.HotReload.Telemetry;

namespace Xamarin.HotReload
{
	class AgentServiceProvider : IServiceProvider
	{
		readonly ILogger rootLogger;
		readonly ITelemetryService rootTelemetry;
		readonly IHotReloadAgent childAgent;
		readonly Dictionary<Type,object> services = new Dictionary<Type,object> (); // lock!

		public AgentServiceProvider (HotReloadAgent parentAgent, IHotReloadAgent childAgent)
		{
			this.rootLogger = parentAgent.RootLogger;
			this.rootTelemetry = parentAgent.Telemetry;
			this.childAgent = childAgent;
		}

		// for the tests
		public AgentServiceProvider (ILogger rootLogger, ITelemetryService rootTelemetry, IHotReloadAgent childAgent)
		{
			this.rootLogger = rootLogger;
			this.rootTelemetry = rootTelemetry;
			this.childAgent = childAgent;
		}

		public object GetService (Type serviceType)
		{
			lock (services) {
				if (!services.TryGetValue (serviceType, out var obj)) {
					obj = CreateService (serviceType);
					SetService (serviceType, obj);
				}
				return obj;
			}
		}

		// Used by the tests to add mocks
		internal void SetService (Type serviceType, object obj)
		{
			lock (services) {
				if (obj is null)
					services.Remove (serviceType);
				else
					services [serviceType] = obj;
			}
		}

		object CreateService (Type type)
		{
			if (type == typeof (ILogger))
				return rootLogger.WithTag (childAgent.GetName ());
			if (type == typeof (IFileContentProvider))
				return new DefaultFileContentProvider ();
			if (type == typeof (ITelemetryService)) {
				// FIXME: If we ever open this up, we may need to authenticate the agent
				return rootTelemetry.WithPrefix (childAgent.GetName ());
			}
			return null;
		}
	}
}
