using BaseFramework.Core.Metadata;

namespace BaseFramework.Core.Access;

public sealed class DefaultMemberAccessEvaluator : IMemberAccessEvaluator
{
    public MemberAccessEvaluation Evaluate(
        InspectableMemberMetadata member,
        object target,
        InspectableAccessContext? accessContext = null)
    {
        var context = accessContext ?? InspectableAccessContext.Empty;
        var rules = member.AccessRules ?? InspectableAccessRules.Empty;

        var canView = Satisfies(rules.VisibleRoles, rules.VisiblePermissions, context);
        if (!canView)
        {
            return new MemberAccessEvaluation(false, false, false, "The current context does not satisfy the visibility rules.");
        }

        var canEdit = !member.ReadOnly && Satisfies(rules.EditableRoles, rules.EditablePermissions, context);
        var canInvoke = Satisfies(rules.InvokeRoles, rules.InvokePermissions, context);

        if (member.Kind == MemberKind.Method)
        {
            canEdit = false;
            if (!rules.InvokeRoles.Any() && !rules.InvokePermissions.Any())
            {
                canInvoke = true;
            }
        }
        else if (!rules.EditableRoles.Any() && !rules.EditablePermissions.Any())
        {
            canEdit = !member.ReadOnly;
        }

        return new MemberAccessEvaluation(canView, canEdit, canInvoke);
    }

    private static bool Satisfies(
        IReadOnlyList<string> requiredRoles,
        IReadOnlyList<string> requiredPermissions,
        InspectableAccessContext context)
    {
        var rolesSatisfied = requiredRoles.Count == 0 || requiredRoles.Any(context.Roles.Contains);
        var permissionsSatisfied = requiredPermissions.Count == 0 || requiredPermissions.Any(context.Permissions.Contains);
        return rolesSatisfied && permissionsSatisfied;
    }
}
