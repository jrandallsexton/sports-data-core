using System;

namespace SportsData.Core.Common
{
    public static class CommandLineHelpers
    {
        public static T ParseFlag<T>(string[] args, string flag, T defaultValue) where T : struct, Enum
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag)
                {
                    if (!Enum.TryParse<T>(args[i + 1], ignoreCase: true, out var value))
                    {
                        var valid = string.Join(", ", Enum.GetNames<T>());
                        throw new ArgumentException(
                            $"Invalid value '{args[i + 1]}' for {flag}. Valid values: {valid}");
                    }
                    return value;
                }
            }

            return defaultValue;
        }
    }
}
