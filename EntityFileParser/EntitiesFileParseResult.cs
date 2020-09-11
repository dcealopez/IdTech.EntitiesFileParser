namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entities file parse result class
    /// </summary>
    public class EntitiesFileParseResult
    {
        /// <summary>
        /// Warnings thrown while parsing
        /// </summary>
        public string[] Warnings;

        /// <summary>
        /// Errors found while parsing
        /// </summary>
        public string[] Errors;

        /// <summary>
        /// The parsed entities file, will be null if any errors or warnings were thrown
        /// </summary>
        public EntitiesFile EntitiesFile;
    }
}
