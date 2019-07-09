using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Xamarin.HotReload
{
	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = false)]
	public class HotReloadAgentAttribute : Attribute
	{
		public Type AgentType { get; }

		public HotReloadAgentAttribute (Type agentType)
		{
			AgentType = agentType;
		}
	}

	/// <summary>
	/// Implemented by an agent to participate in the hot reload process.
	///  The implementing class must have a public constructor that takes no arguments.
	///  The assembly must be attributed with <see cref="HotReloadAgentAttribute"/>.
	/// </summary>
	public interface IHotReloadAgent
	{
		/// <summary>
		/// Called early in the process life to initialize this <see cref="IHotReloadAgent"/>.
		/// </summary>
		/// <param name="provider">Provides access to agent services.
		///  The only service that is guaranteed to be available is <see cref="ILogger"/>.</param>
		void InitializeAgent (IServiceProvider provider);

		/// <summary>
		/// Called when changes have been made that need to be reloaded.
		/// </summary>
		Task ReloadAsync (IEnumerable<ReloadTransaction> requests);
	}

	public static class HotReloadAgentEx
	{
		public static string GetName (this IHotReloadAgent agent) => agent.GetType ().Name;
		public static Task ReloadAsync (this IHotReloadAgent agent, params ReloadTransaction [] txns)
			=> agent.ReloadAsync ((IEnumerable<ReloadTransaction>)txns);
	}
}
