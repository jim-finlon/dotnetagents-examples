using DotNetAgents.Agents.BehaviorTrees;

namespace SalesArena.Orchestrator.Orchestration;

public sealed class SupervisorBehaviorTree
{
    private readonly BehaviorTree<ArenaOrchestratorContext> _tree;
    private readonly BehaviorTreeExecutor<ArenaOrchestratorContext> _executor;

    public SupervisorBehaviorTree()
    {
        var root = new ActionNode<ArenaOrchestratorContext>(
            "TickPersonaPods",
            TickPersonaPodsAsync);

        _tree = new BehaviorTree<ArenaOrchestratorContext>("SalesArenaSupervisor", root);
        _executor = new BehaviorTreeExecutor<ArenaOrchestratorContext>();
    }

    public Task<BehaviorTreeNodeResult<ArenaOrchestratorContext>> ExecuteAsync(
        ArenaOrchestratorContext context,
        CancellationToken cancellationToken = default) =>
        _executor.ExecuteAsync(_tree, context, cancellationToken);

    private static async Task<BehaviorTreeNodeStatus> TickPersonaPodsAsync(
        ArenaOrchestratorContext context,
        CancellationToken cancellationToken)
    {
        foreach (var pod in context.Pods)
        {
            foreach (var agent in pod.Agents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await agent.TickAsync(pod, context, cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    continue;
                }

                context.Activities.Add(new PersonaActivity(
                    pod.PodId,
                    pod.Persona,
                    result.AgentRole,
                    result.Activity,
                    result.LeadId,
                    DateTimeOffset.UtcNow));
            }
        }

        return BehaviorTreeNodeStatus.Success;
    }
}
