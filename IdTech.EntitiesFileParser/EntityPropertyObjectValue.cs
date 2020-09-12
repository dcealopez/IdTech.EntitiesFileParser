using System.Collections.Generic;

namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entity property object value
    /// </summary>
    public class EntityPropertyObjectValue : EntityPropertyValue
    {
        /// <summary>
        /// List of entity properties this object contains
        /// </summary>
        public List<EntityProperty> Value;
    }
}
