using System.Collections.Generic;

namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entities file class
    /// </summary>
    public class EntitiesFile
    {
        /// <summary>
        /// Entities file version string
        /// </summary>
        public string Version;

        /// <summary>
        /// Entities file hierarchy version string
        /// </summary>
        public string HierarchyVersion;

        /// <summary>
        /// Entities file entities
        /// </summary>
        public List<Entity> Entities;
    }
}
