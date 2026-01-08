namespace SIL.Transcriber.Utility.Extensions
{
    public static class IdMapExtensions
    {
        /// <summary>
        /// Gets the value associated with the specified nullable integer key, converting it to a string.
        /// Returns 0 if the key is null or not found in the dictionary.
        /// </summary>
        /// <param name="map">The dictionary to search.</param>
        /// <param name="key">The nullable integer key to locate.</param>
        /// <returns>The value associated with the key, or 0 if the key is null or not found.</returns>
        public static int GetValueOrDefault(this Dictionary<string, int> map, int? key)
        {
            return key == null ? 0 : map.GetValueOrDefault(key.ToString() ?? "");
        }

        /// <summary>
        /// Adds the specified integer key and value to the dictionary, converting the key to a string.
        /// </summary>
        /// <param name="map">The dictionary to add to.</param>
        /// <param name="key">The integer key to add.</param>
        /// <param name="value">The value to add.</param>
        public static void Add(this Dictionary<string, int> map, int key, int value)
        {
            map.Add(key.ToString(), value);
        }

        /// <summary>
        /// Attempts to add the specified integer key and value to the dictionary, converting the key to a string.
        /// </summary>
        /// <param name="map">The dictionary to add to.</param>
        /// <param name="key">The integer key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>true if the key/value pair was added successfully; otherwise, false.</returns>
        public static bool TryAdd(this Dictionary<string, int> map, int key, int value)
        {
            return map.TryAdd(key.ToString(), value);
        }
    }
}
