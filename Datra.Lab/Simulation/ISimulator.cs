#nullable enable
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// The one class a game writes: how a single bot plays the game from start to finish.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Play"/> is called once per bot, from several threads at once, against
    /// one shared <typeparamref name="TContext"/>. Keep the simulator stateless and treat the
    /// data as read-only.</para>
    /// <para>All randomness must come from <see cref="SimRun.Random"/>: two calls with the same
    /// <see cref="SimRun.Seed"/> and the same data must write the same records.</para>
    /// </remarks>
    public interface ISimulator<TContext> where TContext : class, IDataContext
    {
        void Play(TContext data, SimRun run);
    }
}
