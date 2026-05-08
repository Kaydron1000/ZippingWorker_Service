namespace ZippingWorker_Service
{
    public static class Extensions
    {
        /// <summary>
        /// Generates a 6-character alphanumeric identifier (uppercase letters and digits).
        /// Provides ~2.2 billion unique combinations (36^6).
        /// </summary>
        public static string GenerateShortId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new char[6];

            for (int i = 0; i < 6; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }

        /// <summary>
        /// Generates a new file path by appending "_New" and a number to the original file name if a file with the same name already exists.
        /// </summary>
        /// <param name="originalPath">The original file path.</param>
        /// <returns>A new file path that does not conflict with existing files.</returns>
        public static string GetNextAvailableFilePath(this string originalPath)
        {
            string directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            for (int i = 1; i <= 999; i++)
            {
                string candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension}_New{i}{extension}");
                if (!File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            // Fallback if all _New1 through _New999 exist (unlikely scenario)
            return Path.Combine(directory, $"{fileNameWithoutExtension}_New{GenerateShortId()}{extension}");
        }
    }
}
