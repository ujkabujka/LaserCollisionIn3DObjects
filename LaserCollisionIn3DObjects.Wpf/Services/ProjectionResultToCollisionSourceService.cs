using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Services;

public sealed class ProjectionResultToCollisionSourceService
{
    public ProjectedLightSourceItemViewModel CreateProjectedLightSource(
        NamedProjectionResultState selectedResult,
        AxisymmetricSourceProfileDefinition fallbackProfileDefinition)
    {
        ArgumentNullException.ThrowIfNull(selectedResult);

        var exactRays = selectedResult.Result.GetEffectiveRays().ToList();
        if (exactRays.Count == 0)
        {
            throw new InvalidOperationException("Selected projection result does not contain rays.");
        }

        // Projected light sources represent reconstructed source solutions.
        // Their rays must be copied exactly from projection results and must not be regenerated from geometry.
        var sourceFrame = selectedResult.Result.AxisymmetricSource?.SourceFrame ?? selectedResult.Result.SourceFrame;
        // AxisymmetricProjectionState currently does not store the original profile definition.
        // The Projection Workspace provides the current geometry definition as fallback.
        var profileDefinition = fallbackProfileDefinition;

        var source = new ProjectedLightSourceItemViewModel
        {
            Name = $"Projected Source - {selectedResult.DisplayName}",
            SourceFrame = sourceFrame,
            ProfileDefinition = profileDefinition,
            OriginKind = ProjectedLightSourceOriginKind.ProjectionResult,
        };

        foreach (var ray in exactRays)
        {
            source.Rays.Add(ray);
        }

        return source;
    }
}
