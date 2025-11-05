namespace SportsData.Provider.Application.Jobs;

public enum ResourceShape
{
    Auto = 0,   // Detect at runtime
    Index = 1,  // Force traversal
    Leaf = 2    // Force as a single document
}