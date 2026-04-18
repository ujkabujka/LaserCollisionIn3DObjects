namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class ProjectionMethodRegistry
{
    private readonly Dictionary<string, IProjectionMethod> _methods;

    public ProjectionMethodRegistry(IEnumerable<IProjectionMethod> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);
        _methods = methods.ToDictionary(method => method.Metadata.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IProjectionMethod> Methods => _methods.Values.ToList();

    public IProjectionMethod GetRequired(string methodId)
    {
        if (!_methods.TryGetValue(methodId, out var method))
        {
            throw new KeyNotFoundException($"Projection method '{methodId}' is not registered.");
        }

        return method;
    }
}
