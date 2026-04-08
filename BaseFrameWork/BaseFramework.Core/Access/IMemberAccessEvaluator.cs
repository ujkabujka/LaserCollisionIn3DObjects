using BaseFramework.Core.Metadata;

namespace BaseFramework.Core.Access;

public interface IMemberAccessEvaluator
{
    MemberAccessEvaluation Evaluate(
        InspectableMemberMetadata member,
        object target,
        InspectableAccessContext? accessContext = null);
}
