using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.HotReload
{
	[DataContract (Name = "ReloadState")]
	public enum HotReloadState
	{
		[EnumMember]
		Disabled = 0,
		[EnumMember]
		Starting = 1,
		[EnumMember]
		Enabled = 2,
		[EnumMember]
		Failed = 99
	}
}
