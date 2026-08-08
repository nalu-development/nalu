; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NALU0001 | NaluNavigation | Info | Page registered without a page model (view-only)
NALU0002 | NaluNavigation | Warning | Ambiguous page model (multiple BindingContext constructor parameters)
NALU0003 | NaluNavigation | Warning | Cannot resolve page model interface implementation
NALU0004 | NaluNavigation | Warning | Page model does not implement INotifyPropertyChanged
NALU0005 | NaluNavigation | Error | Intent restore type id collision
NALU0006 | NaluNavigation | Warning | Ambiguous page model by naming convention
