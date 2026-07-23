# Built-in light-source formats

To add a built-in format, create a public, concrete class in the Domain assembly with a public
parameterless constructor that implements `ILightSourceFormat`. Give it a unique ID, display name,
and one or more extensions, then implement `CanExport`, `CanRead`, `Write`, and `Read` against
`LightSourceTransferData` only.

```csharp
public sealed class Format2 : ILightSourceFormat
{
    public string Id => "format-2";
    public string DisplayName => "Format 2";
    public IReadOnlyList<string> FileExtensions => new[] { ".src2" };
    // Implement CanExport, CanRead, Write, and Read.
}
```

After rebuilding, `LightSourceFormatRegistry` discovers the class automatically. Do not edit the
main window, XAML, transfer service, or a central registration list. Format implementations do not
own input/output streams and must not dispose them.
