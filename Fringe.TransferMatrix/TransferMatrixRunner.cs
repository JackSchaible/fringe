using Fringe.Data;
using FringeTransferMatrix.Services;

namespace FringeTransferMatrix;

/// <summary>Orchestrates the transfer-matrix generation pipeline.</summary>
internal static class TransferMatrixRunner
{
    /// <summary>
    /// Runs the pipeline. Generation is skipped entirely — logged, not treated as an error —
    /// when <paramref name="matrixProvider"/> is <see langword="null"/> (no provider configured).
    /// </summary>
    public static async Task RunAsync(FringeRepository repository, IMatrixProvider? matrixProvider)
    {
        TransferMatrixLogger.Log("Beginning Fringe transfer matrix generation...");

        if (matrixProvider == null)
        {
            TransferMatrixLogger.Log("No matrix provider configured — skipping transfer matrix generation.");
            return;
        }

        TransferMatrixGenerator generator = new(repository, matrixProvider);
        await generator.GenerateAsync().ConfigureAwait(false);

        TransferMatrixLogger.Log("Transfer matrix generation run complete.");
    }
}
