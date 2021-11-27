﻿#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.HotReload;
using Esp.Resources;
using System.Reflection;
using Microsoft.Maui.Hosting;

[assembly: AssemblyMetadata("IsTrimmable", "True")]
namespace Comet
{
	//public static class Reload
	//{
	//    public static Task<bool> Init(string ideIP = null, int idePort = Constants.DEFAULT_PORT)
	//    {
	//        Reloadify.Reload.Instance.ReplaceType = (d) => MauiHotReloadHelper.RegisterReplacedView(d.ClassName, d.Type);
	//        Reloadify.Reload.Instance.FinishedReload = () => MauiHotReloadHelper.TriggerReload();
	//        return Reloadify.Reload.Init(ideIP, idePort);
	//    }
	//}
	public static partial class Reload
	{
		public static MauiAppBuilder EnableHotReload(this MauiAppBuilder builder, string? ideIp = null, int idePort = Constants.DEFAULT_PORT)
		{
			builder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IMauiInitializeService, HotReloadBuilder>(_ => new HotReloadBuilder
			{
				IdeIp = ideIp,
				IdePort = idePort,
			}));
			return builder;
		}

		class HotReloadBuilder : IMauiInitializeService
		{
			public string? IdeIp { get; set; }

			public int IdePort { get; set; } = 9988;

			public async void Initialize(IServiceProvider services)
			{
				var handlers = services.GetRequiredService<IMauiHandlersServiceProvider>();

				MauiHotReloadHelper.Init(handlers.GetCollection());

				Reloadify.Reload.Instance.ReplaceType = (d) =>
				{
					MauiHotReloadHelper.RegisterReplacedView(d.ClassName, d.Type);
				};

				Reloadify.Reload.Instance.FinishedReload = () =>
				{
					MauiHotReloadHelper.TriggerReload();
				};

				await Task.Run(async () =>
				{
					try
					{
						var success = await Reloadify.Reload.Init(IdeIp, IdePort);

						Console.WriteLine($"HotReload Initialize: {success}");
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
				});
			}
		}
	}
}
