namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entity property class
    /// </summary>
    public class EntityProperty
    {
        /// <summary>
        /// The name of the property
        /// </summary>
        public string Name;

        /// <summary>
        /// Indicates wether or not the name of the property is quoted
        /// </summary>
        public bool IsQuoted;

        /// <summary>
        /// The value of the property
        /// </summary>
        public EntityPropertyValue Value;

        /// <summary>
        /// If this property is overriding another property in an inherited
        /// entityDef, this will mark it as important to have preference
        /// over it (entities file Version 5 only)
        /// </summary>
        public bool Important;
    }
}
