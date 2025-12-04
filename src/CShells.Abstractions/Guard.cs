namespace CShells;

using System.Runtime.CompilerServices;

/// <summary>
/// Lightweight guard helper for fluent, low-noise argument validation.
/// </summary>
public static class Guard
{
    public static class Against
    {
        public static T Null<T>(T? input, [CallerArgumentExpression("input")] string? paramName = null)
            where T : class
        {
            if (input is null)
                throw new ArgumentNullException(paramName);

            return input;
        }

        public static T Null<T>(T? input, [CallerArgumentExpression("input")] string? paramName = null)
            where T : struct
        {
            if (!input.HasValue)
                throw new ArgumentNullException(paramName);

            return input.Value;
        }

        public static string NullOrWhiteSpace(string? input, [CallerArgumentExpression("input")] string? paramName = null)
        {
            if (input is null)
                throw new ArgumentNullException(paramName);
            
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Value cannot be null or whitespace.", paramName);

            return input;
        }

        public static string NullOrEmpty(string? input, [CallerArgumentExpression("input")] string? paramName = null)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Value cannot be null or empty.", paramName);
            return input;
        }

        public static IReadOnlyCollection<T> NullOrEmpty<T>(IReadOnlyCollection<T>? input, [CallerArgumentExpression("input")] string? paramName = null)
        {
            if (input is null)
                throw new ArgumentNullException(paramName);
            if (input.Count == 0)
                throw new ArgumentException("Collection cannot be empty.", paramName);
            return input;
        }

        public static T[] NullOrEmpty<T>(T[]? input, [CallerArgumentExpression("input")] string? paramName = null)
        {
            if (input is null)
                throw new ArgumentNullException(paramName);
            if (input.Length == 0)
                throw new ArgumentException("Array cannot be empty.", paramName);
            return input;
        }

        public static TStruct NotDefault<TStruct>(TStruct input, [CallerArgumentExpression("input")] string? paramName = null)
            where TStruct : struct
        {
            if (EqualityComparer<TStruct>.Default.Equals(input, default))
                throw new ArgumentException("Value cannot be the default value.", paramName);
            return input;
        }
    }
}
