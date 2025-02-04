#if !NET9_0_OR_GREATER
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalu;

using System.ComponentModel;

[Obsolete("This is a .NET9 backport and it will be removed in version 9.0.0 where it has no effect.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class HandlerPropertiesBackport
{
    public static readonly BindableProperty DisconnectPolicyBackportProperty = BindableProperty.CreateAttached(
        "DisconnectPolicyBackport",
        typeof(HandlerDisconnectPolicyBackport),
        typeof(HandlerPropertiesBackport),
        HandlerDisconnectPolicyBackport.Automatic);

    public static void SetDisconnectPolicyBackport(BindableObject target, HandlerDisconnectPolicyBackport value)
        => target.SetValue(DisconnectPolicyBackportProperty, value);

    public static HandlerDisconnectPolicyBackport GetDisconnectPolicyBackport(BindableObject target)
        => (HandlerDisconnectPolicyBackport)target.GetValue(DisconnectPolicyBackportProperty);
}
#endif
