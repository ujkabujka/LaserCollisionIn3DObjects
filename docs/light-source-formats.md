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

## SRC2 spherical-direction example

`SphericalDirectionLightSourceFormat` is a second built-in plugin (`spherical-direction-text-v1`) with
extension `.src2`. Its signature is `LASER_SOURCE_FORMAT: SRC2` followed by
`LASER_SOURCE_FORMAT_VERSION: 1`. Every ray has `Position N`, `Direction N`, and
`Direction_Spherical N` rows. Angles are radians: azimuth is `atan2(y, x)` in `[-π, π]` and elevation
is `atan2(z, sqrt(x²+y²))` in `[-π/2, π/2]`; poles use azimuth zero. Import reconstructs the spherical
vector and rejects it when it differs from the normalized Cartesian direction by more than `1e-5`.
