using Relego.Server.Data;
using Relego.Server.Models;

namespace Relego.Server.Services;

public sealed class HighlightSelectionService(RecapRepository recapRepository)
{
    public async Task<IReadOnlyList<SelectionCandidate>> SelectAsync(
        int userId,
        Settings settings,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var candidates = await recapRepository.SelectCandidatesAsync(userId, cancellationToken);

        return candidates
            .Select(c => c with { Score = ComputeScore(c, now) })
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.CreatedAt)
            .Take(settings.Count)
            .ToList();
    }

    internal static int ComputeScore(SelectionCandidate candidate, DateTimeOffset now)
    {
        var reference = candidate.LastSeen ?? DateTimeOffset.MinValue;
        var ageInDays = (int)(now - reference).TotalDays;
        return ageInDays + candidate.Weight;
    }
}
