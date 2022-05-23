using System;

namespace SIL.Transcriber.Utility
{
    public static class EnvironmentHelpers
    {
        public static string GetVarOrDefault(string name, string defaultValue)
        {
            string? variable = Environment.GetEnvironmentVariable(name);

            return string.IsNullOrEmpty(variable) ? defaultValue : variable;
        }

        public static string GetVarOrThrow(string name)
        {
            string? variable = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrEmpty(variable))
            {
                throw new System.Exception("Env var: " + name + " is not defined");
            }

            return variable;
        }

        public static int GetIntVarOrDefault(string name, int defaultValue)
        {
            string? envString = GetVarOrDefault(name, defaultValue.ToString());
            if (!int.TryParse(envString, out int varValue))
            {
                varValue = defaultValue;
            }
            return varValue;
        }
    }
}

