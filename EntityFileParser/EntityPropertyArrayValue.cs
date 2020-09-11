using System.Collections.Generic;

namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entity property array value
    /// </summary>
    public class EntityPropertyArrayValue : EntityPropertyValue
    {
        /// <summary>
        /// The values of the array
        /// </summary>
        public List<EntityPropertyValue> Values;
    }
}
