namespace QLStats.Services;

public interface ICanApplyEvent<TEvent>
{
    void Apply(TEvent @event);
}
