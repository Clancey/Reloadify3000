using System;

namespace Xamarin.HotReload.Telemetry
{
	public enum TelemetryResult
	{
		/// <summary>
		/// Used for unknown or unavailable result.
		/// </summary>
		None,
		/// <summary>
		/// A result without any failure from product or user.
		/// </summary>
		Success,
		/// <summary>
		/// A result to indicate the action/operation failed because of product issue (not user faults)
		/// Consider using FaultEvent to provide more details about the failure.
		/// </summary>
		Failure,
		/// <summary>
		/// A result to indicate the action/operation failed because of user fault (e.g., invalid input).
		/// Consider using FaultEvent to provide more details.
		/// </summary>
		UserFault,
		/// <summary>
		/// A result to indicate the action/operation is cancelled by user.
		/// </summary>
		UserCancel
	}
}
