namespace SalesArena.Orchestrator.Orchestration;

public interface IPodManager
{
    IReadOnlyCollection<PersonaPod> ActivePods { get; }

    PersonaPod SpawnPod(string persona);

    bool Despawn(string podId);
}
