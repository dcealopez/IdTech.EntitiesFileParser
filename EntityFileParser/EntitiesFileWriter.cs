using System.Globalization;
using System.IO;
using System.Text;

namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entities file writer static class
    /// </summary>
    public static class EntitiesFileWriter
    {
        /// <summary>
        /// Writes an EntitiesFile object to a file
        /// </summary>
        /// <param name="entitiesFile">EntitiesFile object</param>
        /// <param name="path">destination path</param>
        public static void WriteEntitiesFile(EntitiesFile entitiesFile, string path)
        {
            using (var streamWriter = new StreamWriter(path, false))
            {
                streamWriter.NewLine = "";
                streamWriter.WriteLine(GetEntitiesFileHeaderString(entitiesFile));

                for (int i = 0; i < entitiesFile.Entities.Count; i++)
                {
                    streamWriter.WriteLine(GetEntityString(entitiesFile.Entities[i]));

                    if (i != entitiesFile.Entities.Count - 1)
                    {
                        streamWriter.WriteLine("\n");
                    }
                }

                streamWriter.Close();
            }
        }

        /// <summary>
        /// Builds the header string of the entities file
        /// </summary>
        /// <param name="entitiesFile">EntitiesFile object</param>
        /// <returns>the header string of the entities file</returns>
        internal static string GetEntitiesFileHeaderString(EntitiesFile entitiesFile)
        {
            StringBuilder header = new StringBuilder();

            if (!string.IsNullOrEmpty(entitiesFile.Version))
            {
                header.Append("Version ").Append(entitiesFile.Version).Append("\n");
            }

            if (!string.IsNullOrEmpty(entitiesFile.HierarchyVersion))
            {
                header.Append("HierarchyVersion ").Append(entitiesFile.HierarchyVersion).Append("\n");
            }

            return header.ToString();
        }

        /// <summary>
        /// Builds the full string for an entity
        /// </summary>
        /// <param name="entity">Entity object</param>
        /// <returns>the full string for the entity</returns>
        internal static string GetEntityString(Entity entity)
        {
            StringBuilder entityString = new StringBuilder();
            uint indentationLevel = 1;

            entityString.Append("entity {").Append("\n");

            // Layers
            if (entity.Layers != null)
            {
                entityString.Append(IndentText("layers {", indentationLevel++)).Append("\n");

                foreach (var layer in entity.Layers)
                {
                    if (layer == null)
                    {
                        continue;
                    }

                    entityString.Append(IndentText("\"", indentationLevel)).Append(layer.Value).Append("\"").Append("\n");
                }

                indentationLevel--;
                entityString.Append(IndentText("}", indentationLevel)).Append("\n");
            }

            // InstanceId and OriginalName
            if (entity.InstanceId != null)
            {
                entityString.Append(IndentText("instanceId = ", indentationLevel)).Append(entity.InstanceId.Value).Append(";").Append("\n");
            }

            if (entity.OriginalName != null)
            {
                entityString.Append(IndentText("originalName = \"", indentationLevel)).Append(entity.OriginalName.Value).Append("\";").Append("\n");
            }

            // EntityDef and its properties
            entityString.Append(IndentText("entityDef ", indentationLevel)).Append(entity.EntityDef.Name).Append(" {").Append("\n");

            foreach (var property in entity.EntityDef.Properties)
            {
                if (property == null)
                {
                    continue;
                }

                if (property.Value.GetType() == typeof(EntityPropertyArrayValue))
                {
                    entityString.Append(GetArrayString(property, ref indentationLevel)).Append("\n");
                }
                else if (property.Value.GetType() == typeof(EntityPropertyObjectValue))
                {
                    entityString.Append(GetObjectString(property, ref indentationLevel)).Append("\n");
                }
                else
                {
                    entityString.Append(GetPropertyString(property, indentationLevel)).Append("\n");
                }
            }

            entityString.Append("}").Append("\n");
            entityString.Append("}");

            return entityString.ToString();
        }

        /// <summary>
        /// Builds the string for an array
        /// </summary>
        /// <param name="arrayProperty">EntityProperty object of the array</param>
        /// <param name="indentationLevel">current indentation level</param>
        /// <returns>the string for the array</returns>
        internal static string GetArrayString(EntityProperty arrayProperty, ref uint indentationLevel)
        {
            StringBuilder arrayString = new StringBuilder();
            arrayString.Append(IndentText(arrayProperty.Name, indentationLevel)).Append(" = ");

            if (arrayProperty.Important)
            {
                arrayString.Append("! ");
            }

            arrayString.Append("{").Append("\n");

            indentationLevel++;
            arrayString.Append(IndentText("num = ", indentationLevel)).Append(((EntityPropertyArrayValue)arrayProperty.Value).Values.Count).Append(";").Append("\n");

            for (int i = 0; i < ((EntityPropertyArrayValue)arrayProperty.Value).Values.Count; i++)
            {
                if (((EntityPropertyArrayValue)arrayProperty.Value).Values[i].GetType() == typeof(EntityPropertyObjectValue))
                {
                    arrayString.Append(GetArrayObjectValueString(((EntityPropertyArrayValue)arrayProperty.Value).Values[i] as EntityPropertyObjectValue, i, indentationLevel)).Append("\n");
                }
                else
                {
                    arrayString.Append(GetArrayPropertyValueString(((EntityPropertyArrayValue)arrayProperty.Value).Values[i], i, indentationLevel)).Append("\n");
                }
            }

            indentationLevel--;
            arrayString.Append(IndentText("}", indentationLevel));

            return arrayString.ToString();
        }

        /// <summary>
        /// Builds the string for an object
        /// </summary>
        /// <param name="objectProperty">EntityProperty of the object</param>
        /// <param name="indentationLevel">current indentation level</param>
        /// <returns>the string for the object</returns>
        public static string GetObjectString(EntityProperty objectProperty, ref uint indentationLevel)
        {
            StringBuilder objectString = new StringBuilder();
            objectString.Append(IndentText(objectProperty.Name, indentationLevel)).Append(" = ");

            if (objectProperty.Important)
            {
                objectString.Append("! ");
            }

            objectString.Append("{").Append("\n");
            indentationLevel++;

            foreach (var property in ((EntityPropertyObjectValue)objectProperty.Value).Value)
            {
                if (property.Value == null)
                {
                    continue;
                }

                if (property.Value.GetType() == typeof(EntityPropertyArrayValue))
                {
                    objectString.Append(GetArrayString(property, ref indentationLevel)).Append("\n");
                }
                else if (property.Value.GetType() == typeof(EntityPropertyObjectValue))
                {
                    objectString.Append(GetObjectString(property, ref indentationLevel)).Append("\n");
                }
                else
                {
                    objectString.Append(GetPropertyString(property, indentationLevel)).Append("\n");
                }
            }

            indentationLevel--;
            objectString.Append(IndentText("}", indentationLevel));

            return objectString.ToString();
        }

        /// <summary>
        /// Builds the string for an array object value
        /// </summary>
        /// <param name="arrayObjectValue">EntityPropertyObjectValue object of the value of the array</param>
        /// <param name="arrayItemIndex">index of this item in the array</param>
        /// <param name="indentationLevel">current indentation level</param>
        /// <returns>the string for the array object value></returns>
        public static string GetArrayObjectValueString(EntityPropertyObjectValue arrayObjectValue, int arrayItemIndex, uint indentationLevel)
        {
            StringBuilder objectString = new StringBuilder();
            objectString.Append(IndentText("item[", indentationLevel)).Append(arrayItemIndex).Append("] = {").Append("\n");
            indentationLevel++;

            foreach (var value in arrayObjectValue.Value)
            {
                if (value == null)
                {
                    continue;
                }

                if (value.Value.GetType() == typeof(EntityPropertyArrayValue))
                {
                    objectString.Append(GetArrayString(value, ref indentationLevel)).Append("\n");
                }
                else if (value.Value.GetType() == typeof(EntityPropertyObjectValue))
                {
                    objectString.Append(GetObjectString(value, ref indentationLevel)).Append("\n");
                }
                else
                {
                    objectString.Append(GetPropertyString(value, indentationLevel)).Append("\n");
                }
            }

            indentationLevel--;
            objectString.Append(IndentText("}", indentationLevel));

            return objectString.ToString();
        }

        /// <summary>
        /// Builds the string for a "single-line" property
        /// </summary>
        /// <param name="property">EntityProperty of the "single-line" property</param>
        /// <param name="indentationLevel">current indentation level</param>
        /// <returns>the string for the "single-line" property</returns>
        public static string GetPropertyString(EntityProperty property, uint indentationLevel)
        {
            StringBuilder propertyString = new StringBuilder();
            propertyString.Append(IndentText(property.Name, indentationLevel)).Append(" = ");

            if (property.Value.GetType() == typeof(EntityPropertyNullValue))
            {
                propertyString.Append("NULL;");
            }
            else if (property.Value.GetType() == typeof(EntityPropertyStringValue))
            {
                propertyString.Append("\"").Append(((EntityPropertyStringValue)property.Value).Value).Append("\";");
            }
            else if (property.Value.GetType() == typeof(EntityPropertyBooleanValue))
            {
                propertyString.Append(((EntityPropertyBooleanValue)property.Value).Value.ToString().ToLower()).Append(";");
            }
            else if (property.Value.GetType() == typeof(EntityPropertyDoubleValue))
            {
                propertyString.Append(((EntityPropertyDoubleValue)property.Value).Value.ToString(CultureInfo.InvariantCulture).ToLower()).Append(";");
            }
            else
            {
                propertyString.Append(((EntityPropertyLongValue)property.Value).Value.ToString(CultureInfo.InvariantCulture).ToLower()).Append(";");
            }

            return propertyString.ToString();
        }

        /// <summary>
        /// Builds the string for an array "single-line" property value
        /// </summary>
        /// <param name="arrayValue">EntityProperty of the value of the array</param>
        /// <param name="arrayItemIndex">index of this item in the array</param>
        /// <param name="indentationLevel">current indentation level</param>
        /// <returns>the string for the array "single-line" property value</returns>
        public static string GetArrayPropertyValueString(EntityPropertyValue arrayValue, int arrayItemIndex, uint indentationLevel)
        {
            StringBuilder arrayPropertyValueString = new StringBuilder();
            arrayPropertyValueString.Append(IndentText("item[", indentationLevel)).Append(arrayItemIndex).Append("] = ");

            if (arrayValue.GetType() == typeof(EntityPropertyStringValue))
            {
                arrayPropertyValueString.Append("\"").Append(((EntityPropertyStringValue)arrayValue).Value).Append("\";");
            }
            else if (arrayValue.GetType() == typeof(EntityPropertyBooleanValue))
            {
                arrayPropertyValueString.Append(((EntityPropertyBooleanValue)arrayValue).Value.ToString().ToLower()).Append(";");
            }
            else if (arrayValue.GetType() == typeof(EntityPropertyDoubleValue))
            {
                arrayPropertyValueString.Append(((EntityPropertyDoubleValue)arrayValue).Value.ToString(CultureInfo.InvariantCulture).ToLower()).Append(";");
            }
            else
            {
                arrayPropertyValueString.Append(((EntityPropertyLongValue)arrayValue).Value.ToString(CultureInfo.InvariantCulture).ToLower()).Append(";");
            }

            return arrayPropertyValueString.ToString();
        }

        /// <summary>
        /// Adds tabs to the given text
        /// </summary>
        /// <param name="text">text to indent</param>
        /// <param name="indentationLevel">tabs to add</param>
        /// <returns>the indented text</returns>
        public static string IndentText(string text, uint indentationLevel)
        {
            if (indentationLevel == 0)
            {
                return text;
            }

            StringBuilder indentedText = new StringBuilder();

            for (uint i = 0; i < indentationLevel; i++)
            {
                indentedText.Append("\t");
            }

            indentedText.Append(text);

            return indentedText.ToString();
        }
    }
}
