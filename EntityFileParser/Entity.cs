using System.Collections.Generic;

namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entity class
    /// </summary>
    public class Entity
    {
        /// <summary>
        /// Entity layer
        /// </summary>
        public List<EntityPropertyStringValue> Layers;

        /// <summary>
        /// Entity originalName
        /// </summary>
        public EntityPropertyStringValue OriginalName;

        /// <summary>
        /// Entity instanceId
        /// </summary>
        public EntityPropertyLongValue InstanceId;

        /// <summary>
        /// The EntityDef of the entity
        /// </summary>
        public EntityDef EntityDef;
    }
}