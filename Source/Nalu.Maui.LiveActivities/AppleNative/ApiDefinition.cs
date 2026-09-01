using System;
using Foundation;
using ObjCRuntime;

namespace Nalu
{
	/// <summary>
	/// Binding of the Swift ActivityKit bridge (see AppleNative/LiveActivitiesInterop).
	/// Results and errors travel through completion blocks; the activity list is one
	/// JSON string to keep the binding surface tiny.
	/// </summary>
	[BaseType(typeof(NSObject), Name = "NaluLiveActivitiesBridge")]
	[DisableDefaultCtor]
	[Internal]
	interface NaluLiveActivitiesBridge
	{
		/// <summary>Whether the OS supports Live Activities at all (iOS 16.2+).</summary>
		[Static]
		[Export("isSupported")]
		bool IsSupported();

		/// <summary>Whether the user allows this app to start Live Activities.</summary>
		[Static]
		[Export("areActivitiesEnabled")]
		bool AreActivitiesEnabled();

		/// <summary>Starts a Live Activity; completes with (activityId, null) or (null, errorMessage).</summary>
		[Static]
		[Export("startActivity:payload:staleDateEpochMs:completion:")]
		void StartActivity(string kind, string payload, double staleDateEpochMs, Action<NSString, NSString> completion);

		/// <summary>Updates a running Live Activity; completes when ActivityKit has taken the update.</summary>
		[Static]
		[Export("updateActivity:payload:staleDateEpochMs:alertTitle:alertBody:completion:")]
		void UpdateActivity(string id, string payload, double staleDateEpochMs, [NullAllowed] string alertTitle, [NullAllowed] string alertBody, Action completion);

		/// <summary>Ends a Live Activity with the final content; completes when it settles.</summary>
		[Static]
		[Export("endActivity:payload:immediate:completion:")]
		void EndActivity(string id, string payload, bool immediate, Action completion);

		/// <summary>The running activities as a JSON array of {id, kind, payload, state}.</summary>
		[Static]
		[Export("activitiesJson")]
		string ActivitiesJson();

		/// <summary>
		/// Reports every activity state transition as (activityId, state) for the life of the
		/// process — "active", "stale", "dismissed" (the user removed it) or "ended".
		/// </summary>
		[Static]
		[Export("observeActivityStates:")]
		void ObserveActivityStates(Action<NSString, NSString> callback);
	}
}
