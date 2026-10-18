namespace WorkflowEngine.Infrastructure.Persistence.Seed;

// One seeded PortfolioTrade workflow definition version.
internal sealed record PortfolioTradeSeedDefinition(
    int VersionNumber,
    ParticipantDefinitionSeed[] ParticipantDefinitions,
    StateSeed[] States,
    RoleSeed[] Roles,
    ActionSeed[] Actions,
    SlaPolicySeed[] SlaPolicies,
    TaskSeed[] Tasks,
    NotificationRouteSeed[] NotificationRoutes,
    NotificationRecipientRuleSeed[] NotificationRecipientRules,
    TaskAssignmentRoleSeed[] TaskAssignmentRoles,
    TransitionSeed[] Transitions);
