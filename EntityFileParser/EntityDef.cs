using System.Collections.Generic;

namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// EntityDef class
    /// </summary>
    public class EntityDef
    {
        // The name of the EntityDef
        public string Name;

        /// <summary>
        /// The properties the EntityDef has
        /// </summary>
        public List<EntityProperty> Properties;
    }
}
