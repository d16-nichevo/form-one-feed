using Microsoft.Extensions.Configuration;

namespace FormOneFeed
{
    internal static class Extensions
    {
        /// <summary>
        /// Checks if a config field is valid.
        /// </summary>
        /// <param name="config">
        /// The IConfiguration object to query.
        /// </param>
        /// <param name="path">
        /// The path to the value. Example: "CombinedFeed:Title".
        /// </param>
        /// <param name="type">
        /// Check if the value can be converted to this Type. If null, no check is performed.
        /// </param>
        /// <returns>True if the config field appears to be valid. False otherwise.</returns>
        internal static bool CheckConfigVal(this IConfiguration config, string path, Type? type)
        {
            var valid = true;

            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (path == null)
            {
                throw new ArgumentNullException("reference");
            }

            if (config.GetSection(path) == null)
            {
                valid = false;
            }
            else if (type != null)
            {
                if (type == typeof(string) || type == typeof(int))
                {
                    valid = !String.IsNullOrEmpty(config.GetSection(path).Value);
                }
                else if (type == typeof(bool))
                {
                    valid = config.GetSection(path).Value!.Equals("true", StringComparison.OrdinalIgnoreCase)
                         || config.GetSection(path).Value!.Equals("fasle", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            return valid;
        }
    }
}
