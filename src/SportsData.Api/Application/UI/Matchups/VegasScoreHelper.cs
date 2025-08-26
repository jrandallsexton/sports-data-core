namespace SportsData.Api.Application.UI.Matchups;

public static class VegasScoreHelper
{
    public static string CalculateImpliedScore(double homeSpread, double overUnder)
    {
        // From home perspective: H - A = spread
        // A + H = overUnder

        double awayImplied = (overUnder + homeSpread) / 2.0;
        double homeImplied = (overUnder - homeSpread) / 2.0;

        return $"{awayImplied:F1} | {homeImplied:F1}";
    }
}