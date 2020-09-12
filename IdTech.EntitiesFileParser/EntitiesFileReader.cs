using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace IdTech.EntitiesFileParser
{
    /// <summary>
    /// Entities file parser class
    /// </summary>
    public class EntitiesFileReader
    {
        /// <summary>
        /// List of warnings thrown while parsing
        /// </summary>
        internal List<string> Warnings;

        /// <summary>
        /// List of errors found while parsing
        /// </summary>
        internal List<string> Errors;

        /// <summary>
        /// Parses the given entities file into an EntitiesFile object
        /// </summary>
        /// <param name="entitiesFilePath">path to the entities file</param>
        /// <returns>an EntitiesFile object with the parsed data from the entities file</returns>
        public EntitiesFileParseResult Parse(string entitiesFilePath)
        {
            if (!File.Exists(entitiesFilePath))
            {
                throw new FileNotFoundException(string.Format("Entities file not found in path: {0}", entitiesFilePath));
            }

            Warnings = new List<string>();
            Errors = new List<string>();

            EntitiesFile entitiesFile = new EntitiesFile()
            {
                Version = string.Empty,
                HierarchyVersion = string.Empty
            };

            int currentLineNumber = 0;

            using (FileStream fs = File.Open(entitiesFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (BufferedStream bs = new BufferedStream(fs))
                {
                    using (StreamReader sr = new StreamReader(bs))
                    {
                        string line = string.Empty;
                        bool nextEntityError = false;
                        int openingBracketCount = 0;
                        int closingBracketCount = 0;
                        List<Entity> entities = new List<Entity>();
                        StringBuilder currentEntity = new StringBuilder();

                        // Get whole entity blocks to pass them to the entity parser
                        while ((line = sr.ReadLine()) != null)
                        {
                            currentLineNumber++;
                            line = line.Trim();

                            if (string.IsNullOrEmpty(line))
                            {
                                continue;
                            }

                            // Try to parse the version headers first if we don't have them yet and
                            // if we haven't found any entity block yet
                            if (entitiesFile.Version == string.Empty || entitiesFile.HierarchyVersion == string.Empty && openingBracketCount == 0)
                            {
                                string[] versionHeaderPart = line.Split(' ');

                                if (versionHeaderPart.Length == 2 && versionHeaderPart[0].Equals("Version"))
                                {
                                    entitiesFile.Version = versionHeaderPart[1];
                                    continue;
                                }
                                else if (versionHeaderPart.Length == 2 && versionHeaderPart[0].Equals("HierarchyVersion"))
                                {
                                    entitiesFile.HierarchyVersion = versionHeaderPart[1];
                                    continue;
                                }
                            }

                            // Look for entities
                            if (openingBracketCount == 0)
                            {
                                if (!line.Equals("entity {") && !line.Equals("entity{"))
                                {
                                    if (line.Equals("entity"))
                                    {
                                        Errors.Add(string.Format("Line {0}: missing '{{'", currentLineNumber));
                                    }
                                    else
                                    {
                                        Errors.Add(string.Format("Line {0}: unexpected '{1}", currentLineNumber, line));
                                    }

                                    continue;
                                }

                                // Found an entity here, add the first line and look for the lines in its block
                                currentEntity.Append(line).Append("\n");
                                openingBracketCount++;
                                closingBracketCount = 0;
                                continue;
                            }

                            // We are in an entity block here, get everything until we find the closing bracket
                            if (line[line.Length - 1] == '{')
                            {
                                openingBracketCount++;
                            }

                            if (line[line.Length - 1] == '}')
                            {
                                closingBracketCount++;
                            }

                            // If we find another entity block, check for the closing bracket of our current entity
                            // and opening bracket for the next entity, then continue with it
                            // If not, just continue adding current entity block lines until we hit another entity
                            if (!line.Equals("entity {") && !line.Equals("entity{"))
                            {
                                if (line.Equals("entity"))
                                {
                                    Errors.Add(string.Format("Line {0}: missing '{{'", currentLineNumber));
                                    nextEntityError = true;
                                }
                                else
                                {
                                    if (openingBracketCount != closingBracketCount)
                                    {
                                        currentEntity.Append(line).Append("\n");
                                        continue;
                                    }
                                    else
                                    {
                                        currentEntity.Append(line);

                                        // All OK, push current entity to the entity parser
                                        // and continue searching for more
                                        try
                                        {
                                            Entity parsedEntity = ParseEntity(currentEntity.ToString(), ref currentLineNumber);

                                            if (parsedEntity != null && parsedEntity.EntityDef != null)
                                            {
                                                entities.Add(parsedEntity);
                                            }
                                        }
                                        catch (FormatException)
                                        {
                                            throw;
                                        }

                                        openingBracketCount = 0;
                                        closingBracketCount = 0;
                                        currentEntity.Clear();
                                        continue;
                                    }
                                }
                            }

                            // We hit another entity at this point
                            // Check for the closing bracket of this current entity
                            // Substract 1 from the opening brackets count to not take
                            // the next entity opening bracket into account
                            if ((openingBracketCount - 1 != closingBracketCount) ||
                                (nextEntityError && (openingBracketCount != closingBracketCount)))
                            {
                                Errors.Add(string.Format("Line {0}: missing '}}'", currentLineNumber));
                                nextEntityError = false;
                            }

                            // All OK, push current entity to the entity parser
                            // and continue with the next one
                            try
                            {
                                // Don't take into account the next entity line
                                currentLineNumber--;

                                // Remove last new line character
                                currentEntity.Remove(currentEntity.Length - 1, 1);

                                Entity parsedEntity = ParseEntity(currentEntity.ToString(), ref currentLineNumber);

                                if (parsedEntity != null && parsedEntity.EntityDef != null)
                                {
                                    entities.Add(parsedEntity);
                                }
                            }
                            catch (FormatException)
                            {
                                throw;
                            }

                            currentLineNumber++;
                            openingBracketCount = 1;
                            closingBracketCount = 0;
                            currentEntity.Clear();
                            currentEntity.Append(line).Append("\n");
                        }

                        entitiesFile.Entities = entities;
                    }
                }
            }

            return new EntitiesFileParseResult()
            {
                Errors = Errors.ToArray(),
                Warnings = Warnings.ToArray(),
                EntitiesFile = entitiesFile
            };
        }

        /// <summary>
        /// Parses an entity
        /// </summary>
        /// <param name="entityText">text block containing the entity</param>
        /// <param name="currentLineNumber">current line number reference in the file</param>
        /// <returns>an Entity object with the parsed data from the entity</returns>
        internal Entity ParseEntity(string entityText, ref int currentLineNumber)
        {
            // Split the entity definition into lines
            string[] entityTextLines = entityText.Split('\n');
            currentLineNumber -= entityTextLines.Length - 1;

            Entity entity = new Entity();
            EntityDef entityDef = null;

            for (int i = 0; i < entityTextLines.Length; i++)
            {
                entityTextLines[i] = entityTextLines[i].Trim();

                if (i > 0)
                {
                    currentLineNumber++;
                }

                // Parse the layers
                if (entityTextLines[i].Equals("layers {") || entityTextLines[i].Equals("layers{"))
                {
                    i++;
                    entity.Layers = new List<EntityPropertyStringValue>();

                    while (!entityTextLines[i].Trim().Equals("}"))
                    {
                        try
                        {
                            var layer = (EntityPropertyStringValue)ParseEntityPropertyValue(entityTextLines[i++].Trim(), ref currentLineNumber);

                            if (layer != null)
                            {
                                entity.Layers.Add(layer);
                            }

                            currentLineNumber++;
                        }
                        catch (FormatException)
                        {
                            throw;
                        }
                    }

                    // Closing bracket
                    currentLineNumber++;
                }
                else if (entityTextLines[i].Contains("originalName"))
                {
                    // Look for "originalName" and "instanceId" properties
                    string[] originalNameParts = entityTextLines[i].Split('=');

                    if (originalNameParts.Length != 2)
                    {
                        Errors.Add(string.Format("Line {0}: bad property declaration '{1}'", currentLineNumber, entityTextLines[i]));
                        continue;
                    }

                    if (entityTextLines[i][entityTextLines[i].Length - 1] != ';')
                    {
                        Errors.Add(string.Format("Line {0}: missing ';'", currentLineNumber));
                        continue;
                    }

                    entity.OriginalName = (EntityPropertyStringValue)ParseEntityPropertyValue(originalNameParts[1].Remove(originalNameParts[1].Length - 1, 1).Trim(), ref currentLineNumber);
                }
                else if (entityTextLines[i].Contains("instanceId"))
                {
                    string[] instanceIdParts = entityTextLines[i].Split('=');

                    if (instanceIdParts.Length != 2)
                    {
                        Errors.Add(string.Format("Line {0}: bad property declaration '{1}'", currentLineNumber, entityTextLines[1]));
                        continue;
                    }

                    if (entityTextLines[i][entityTextLines[i].Length - 1] != ';')
                    {
                        Errors.Add(string.Format("Line {0}: missing ';'", currentLineNumber));
                        continue;
                    }

                    entity.InstanceId = (EntityPropertyLongValue)ParseEntityPropertyValue(instanceIdParts[1].Remove(instanceIdParts[1].Length - 1, 1).Trim(), ref currentLineNumber);
                }
                else if (entityTextLines[i].Contains("entityDef"))
                {
                    // Get the entityDef block and parse it
                    if (entityTextLines[i][entityTextLines[i].Length - 1] != '{')
                    {
                        Errors.Add(string.Format("Line {0}: missing '{{'", currentLineNumber));
                        continue;
                    }

                    StringBuilder entityDefText = new StringBuilder();
                    int openingBracketCount = 1;
                    int closingBracketCount = 0;

                    // Append the first entityDef line, which opens the block
                    entityDefText.Append(entityTextLines[i]).Append("\n");

                    // Get everything until the block is closed
                    for (int j = i + 1; j < entityTextLines.Length; j++, currentLineNumber++, i = j)
                    {
                        if (string.IsNullOrEmpty(entityTextLines[j]))
                        {
                            continue;
                        }

                        if (entityTextLines[j][entityTextLines[j].Length - 1] == '{')
                        {
                            openingBracketCount++;
                            entityDefText.Append(entityTextLines[j]).Append("\n");
                            continue;
                        }

                        if (entityTextLines[j][entityTextLines[j].Length - 1] == '}')
                        {
                            closingBracketCount++;
                        }

                        if (openingBracketCount == closingBracketCount)
                        {
                            entityDefText.Append(entityTextLines[j]);
                            currentLineNumber++;
                            break;
                        }

                        entityDefText.Append(entityTextLines[j]).Append("\n");
                    }

                    entityDef = ParseEntityDef(entityDefText.ToString(), ref currentLineNumber);
                    entityDefText.Clear();
                }
                else if (!entityTextLines[i].Equals("entity{") && !entityTextLines[i].Equals("entity {") &&
                    !entityTextLines[i].Equals("{") && !entityTextLines[i].Equals("}"))
                {
                    Errors.Add(string.Format("Line {0}: unexpected '{1}' inside entity definition", currentLineNumber, entityTextLines[i]));
                    continue;
                }
            }

            entity.EntityDef = entityDef;
            return entity;
        }

        /// <summary>
        /// Parses an entityDef into an EntityDef object
        /// </summary>
        /// <param name="entityDefText">text containing the entityDef</param>
        /// <param name="currentLineNumber">current line number reference in the file</param>
        /// <returns>an EntityDef object containing the parsed data from the entityDef</reurns>
        internal EntityDef ParseEntityDef(string entityDefText, ref int currentLineNumber)
        {
            EntityDef entityDef = new EntityDef();
            List<EntityProperty> entityProperties = new List<EntityProperty>();

            // Split the entity definition into lines
            string[] entityDefTextLines = entityDefText.Split('\n');
            currentLineNumber -= entityDefTextLines.Length - 1;

            for (int i = 0; i < entityDefTextLines.Length; i++)
            {
                if (i > 0)
                {
                    currentLineNumber++;
                }

                entityDefTextLines[i] = entityDefTextLines[i].Trim();

                // The entityDef declaration is in the first line and contains its name
                if (i == 0)
                {
                    string[] entityDefParts = entityDefTextLines[i].Split(' ');

                    if (!entityDefParts[0].Equals("entityDef") && !entityDefParts[0].Equals("entityDef{"))
                    {
                        Errors.Add(string.Format("Line {0}: unknown type '{1}'", currentLineNumber, entityDefParts[0]));
                        return null;
                    }

                    // The bracket could be next to the entityDef name so we have to check that
                    if (entityDefParts.Length == 1)
                    {
                        Errors.Add(string.Format("Line {0}: missing entityDef name", currentLineNumber));
                        return null;
                    }
                    else if (entityDefParts.Length == 2)
                    {
                        if (entityDefParts[1].Equals("{"))
                        {
                            Errors.Add(string.Format("Line {0}: missing entityDef name", currentLineNumber));
                            return null;
                        }
                        else if (entityDefParts[1][entityDefParts[1].Length - 1] != '{')
                        {
                            Errors.Add(string.Format("Line {0}: missing '{{'", currentLineNumber));
                            return null;
                        }
                        else if (!IsValidEntityDefName(entityDefParts[1].Remove(entityDefParts[1].Length - 1, 1)))
                        {
                            Errors.Add(string.Format("Line {0}: invalid entityDef name '{1}'",
                                currentLineNumber, entityDefParts[1].Remove(entityDefParts[1].Length - 1, 1)));

                            return null;
                        }
                        else
                        {
                            entityDef.Name = entityDefParts[1].Remove(entityDefParts[1].Length - 1, 1);
                        }
                    }
                    else if (entityDefParts.Length == 3)
                    {
                        if (!entityDefParts[2].Equals("{"))
                        {
                            Errors.Add(string.Format("Line {0}: missing '{{'", currentLineNumber));
                            return null;
                        }
                        else if (!IsValidEntityDefName(entityDefParts[1]))
                        {
                            Errors.Add(string.Format("Line {0}: invalid entityDef name '{1}'", currentLineNumber, entityDefParts[1]));
                            return null;
                        }
                        else
                        {
                            entityDef.Name = entityDefParts[1];
                        }
                    }
                    else
                    {
                        Errors.Add(string.Format("Line {0}: expected entityDef declaration not found", currentLineNumber, entityDefTextLines[i]));
                        return null;
                    }

                    continue;
                }

                // Everything inside the entityDef must be a property of some kind
                EntityProperty entityProperty = null;

                // We will get all the text of the property and pass it to the property parser
                string[] propertyText = entityDefTextLines[i].Split('=');

                if (propertyText.Length != 2)
                {
                    if (entityDefTextLines[i].Equals("{") || entityDefTextLines[i].Equals("}"))
                    {
                        continue;
                    }
                    else
                    {
                        Errors.Add(string.Format("Line {0}: unexpected '{1}'", currentLineNumber, entityDefTextLines[i]));
                        continue;
                    }
                }

                propertyText[0] = propertyText[0].TrimEnd();
                propertyText[1] = propertyText[1].Trim();

                // First we need to determine which kind of property this is to get all it's text
                // This is probably a "single-line" property, so we can just pass the whole line to the property parser
                if (propertyText[1][propertyText[1].Length - 1] != '{')
                {
                    try
                    {
                        entityProperty = ParseEntityProperty(entityDefTextLines[i], ref currentLineNumber);
                    }
                    catch (FormatException)
                    {
                        throw;
                    }
                }
                else
                {
                    // Check if the "Important" flag is present
                    if (propertyText[1].Length > 1 && propertyText[1][0] == '!')
                    {
                        entityProperty.Important = true;
                    }

                    // This is either an object or an array, so we'll just get the whole block
                    // to pass it to the property parser
                    StringBuilder currentPropertyText = new StringBuilder();
                    int openingBracketCount = 1;
                    int closingBracketCount = 0;

                    currentPropertyText.Append(entityDefTextLines[i]).Append("\n");
                    currentLineNumber++;

                    // Get everything until the block is closed
                    for (int j = i + 1; j < entityDefTextLines.Length; j++, currentLineNumber++, i = j)
                    {
                        if (string.IsNullOrEmpty(entityDefTextLines[j]))
                        {
                            continue;
                        }

                        if (entityDefTextLines[j][entityDefTextLines[j].Length - 1] == '{')
                        {
                            openingBracketCount++;
                            currentPropertyText.Append(entityDefTextLines[j]).Append("\n");
                            continue;
                        }

                        if (entityDefTextLines[j][entityDefTextLines[j].Length - 1] == '}')
                        {
                            closingBracketCount++;
                        }

                        if (openingBracketCount == closingBracketCount)
                        {
                            currentPropertyText.Append(entityDefTextLines[j]);
                            break;
                        }

                        currentPropertyText.Append(entityDefTextLines[j]).Append("\n");
                    }

                    entityProperty = ParseEntityProperty(currentPropertyText.ToString(), ref currentLineNumber);
                    currentPropertyText.Clear();
                }

                if (entityProperty != null)
                {
                    entityProperties.Add(entityProperty);
                }
            }

            entityDef.Properties = entityProperties;
            return entityDef;
        }

        /// <summary>
        /// Parses any kind of property from an entityDef into an EntityProperty object
        /// </summary>
        /// <param name="propertyText">text containing the property</param>
        /// <param name="currentLineNumber">current line number reference in the file</param>
        /// <returns>an EntityProperty object containing the parsed data from the property</returns>
        internal EntityProperty ParseEntityProperty(string propertyText, ref int currentLineNumber)
        {
            // Split the property definition into lines (if it has any)
            EntityProperty entityProperty = new EntityProperty();
            string[] propertyLines = propertyText.Split('\n');
            currentLineNumber -= propertyLines.Length - 1;

            for (int i = 0; i < propertyLines.Length; i++)
            {
                propertyLines[i] = propertyLines[i].Trim();

                if (i > 0)
                {
                    currentLineNumber++;
                }

                // Skip closing brackets
                if (propertyLines[i].Equals("}"))
                {
                    continue;
                }

                // Again, first we need to determine which kind of property this is to properly parse it
                string[] propertyParts = propertyLines[i].Split('=');

                if (propertyParts.Length != 2 || string.IsNullOrEmpty(propertyParts[1].Trim()))
                {
                    Errors.Add(string.Format("Line {0}: bad property declaration '{1}'", currentLineNumber, propertyLines[i].Trim()));
                    continue;
                }

                propertyParts[0] = propertyParts[0].TrimEnd();
                propertyParts[1] = propertyParts[1].Trim();

                // This is a "single-line" property
                if (propertyParts[1][propertyParts[1].Length - 1] != '{')
                {
                    propertyParts[0] = propertyParts[0].Trim();
                    propertyParts[1] = propertyParts[1].Trim();

                    if(!IsValidPropertyName(propertyParts[0]))
                    {
                        Errors.Add(string.Format("Line {0}: invalid property name '{1}'", currentLineNumber, propertyParts[0]));
                        continue;
                    }

                    entityProperty.Name = propertyParts[0];

                    try
                    {
                        // Check for and remove semicolon at the end for the value parser
                        if (propertyParts[1][propertyParts[1].Length - 1] != ';')
                        {
                            Errors.Add(string.Format("Line {0}: missing ';'", currentLineNumber));
                        }
                        else
                        {
                            propertyParts[1] = propertyParts[1].Remove(propertyParts[1].Length - 1, 1);
                        }

                        entityProperty.Value = ParseEntityPropertyValue(propertyParts[1].Trim(), ref currentLineNumber);

                        if (entityProperty.Value == null)
                        {
                            continue;
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                else
                {
                    // Check if the "Important" flag is present
                    if (propertyParts[1].Length > 1 && propertyParts[1][0] == '!')
                    {
                        entityProperty.Important = true;
                    }

                    // We will loop through all the properties in this object/array
                    // and do recursion to get the values
                    List<EntityProperty> properties = new List<EntityProperty>();
                    EntityProperty arrayNumProperty = null;
                    bool isArray = false;
                    int arrayNumPropertyLineNumber = 0;
                    int openingBracketCount = 1;
                    int closingBracketCount = 0;

                    currentLineNumber++;

                    // Get everything until the block is closed
                    for (int j = i + 1; j < propertyLines.Length - 1; j++, currentLineNumber++, i = j)
                    {
                        if (string.IsNullOrEmpty(propertyLines[j]))
                        {
                            continue;
                        }

                        if (propertyLines[j][propertyLines[j].Length - 1] == '{')
                        {
                            openingBracketCount++;
                        }

                        if (propertyLines[j][propertyLines[j].Length - 1] == '}')
                        {
                            closingBracketCount++;
                        }

                        if (openingBracketCount == closingBracketCount)
                        {
                            currentLineNumber++;
                            break;
                        }

                        // Check if this is a single property or an object
                        // Everything inside the entityDef must be a property of some kind
                        // We will get all the text of the property and pass it to the property parser
                        EntityProperty nestedEntityProperty = new EntityProperty();
                        string[] currentPropertyText = propertyLines[j].Split('=');

                        if (currentPropertyText.Length != 2)
                        {
                            if (propertyLines[j].Equals("{") || propertyLines[j].Equals("}"))
                            {
                                continue;
                            }
                            else
                            {
                                Errors.Add(string.Format("Line {0}: unexpected '{1}'", currentLineNumber, propertyLines[j]));
                                continue;
                            }
                        }

                        currentPropertyText[0] = currentPropertyText[0].TrimEnd();
                        currentPropertyText[1] = currentPropertyText[1].Trim();

                        if (string.IsNullOrEmpty(currentPropertyText[1]))
                        {
                            Errors.Add(string.Format("Line {0}: missing property value", currentLineNumber));
                            continue;
                        }

                        // Determine what kind of property this is to get all it's text
                        // This is probably a "single-line" property, so we can just pass the whole line to the property parser
                        if (currentPropertyText[1][currentPropertyText[1].Length - 1] != '{')
                        {
                            // Check if this is an array or an object
                            string trimmedPropertyName = currentPropertyText[0].TrimStart();

                            // Get the number of items in the array
                            // We will use it to check that the number matches with the actual
                            // amount of items
                            if (!isArray & trimmedPropertyName.Equals("num"))
                            {
                                arrayNumPropertyLineNumber = currentLineNumber;
                                arrayNumProperty = ParseEntityProperty(propertyLines[j], ref currentLineNumber);

                                if (arrayNumProperty != null && arrayNumProperty.Value != null &&
                                    arrayNumProperty.Value.GetType() != typeof(EntityPropertyLongValue))
                                {
                                    Errors.Add(string.Format("Line {0}: 'num' property value must be a number", currentLineNumber));
                                }

                                isArray = true;
                            }

                            try
                            {
                                nestedEntityProperty = ParseEntityProperty(propertyLines[j], ref currentLineNumber);
                            }
                            catch (FormatException)
                            {
                                throw;
                            }
                        }
                        else
                        {
                            // Check if the "Important" flag is present
                            if (currentPropertyText[1].Length > 1 && currentPropertyText[1][0] == '!')
                            {
                                nestedEntityProperty.Important = true;
                            }

                            // This is either an object or an array, so we'll just get the whole block
                            // to pass it to the property parser
                            StringBuilder currentNestedPropertyText = new StringBuilder();
                            int nestedOpeningBracketCount = 1;
                            int nestedClosingBracketCount = 0;

                            currentNestedPropertyText.Append(propertyLines[j]).Append("\n");

                            // Get everything until the block is closed
                            for (int k = j + 1; j < propertyLines.Length; k++, currentLineNumber++, j = k)
                            {
                                if (string.IsNullOrEmpty(propertyLines[k]))
                                {
                                    continue;
                                }

                                if (propertyLines[k][propertyLines[k].Length - 1] == '{')
                                {
                                    nestedOpeningBracketCount++;
                                    currentNestedPropertyText.Append(propertyLines[k]).Append("\n");
                                    continue;
                                }

                                if (propertyLines[k][propertyLines[k].Length - 1] == '}')
                                {
                                    nestedClosingBracketCount++;
                                }

                                if (nestedOpeningBracketCount == nestedClosingBracketCount)
                                {
                                    currentNestedPropertyText.Append(propertyLines[k]);
                                    currentLineNumber++;
                                    break;
                                }

                                currentNestedPropertyText.Append(propertyLines[k]).Append("\n");
                            }

                            nestedEntityProperty = ParseEntityProperty(currentNestedPropertyText.ToString(), ref currentLineNumber);
                            currentNestedPropertyText.Clear();
                        }

                        if (nestedEntityProperty != null)
                        {
                            properties.Add(nestedEntityProperty);
                        }
                    }

                    if (!isArray)
                    {
                        EntityPropertyObjectValue objectValue = new EntityPropertyObjectValue();
                        objectValue.Value = properties;

                        entityProperty.Value = objectValue;
                    }
                    else
                    {
                        // If this is an array we only need the values
                        uint elementCount = 0;
                        EntityPropertyArrayValue arrayValue = new EntityPropertyArrayValue();
                        arrayValue.Values = new List<EntityPropertyValue>();

                        foreach (var property in properties)
                        {
                            if (property.Value == null || property.Name == null || property.Name.Equals("num"))
                            {
                                continue;
                            }

                            arrayValue.Values.Add(property.Value);
                            elementCount++;
                        }

                        // Print a warning if the 'num' value doesn't match the array's actual element count
                        if (arrayNumProperty != null && arrayNumProperty.Value != null &&
                            arrayNumProperty.Value.GetType() == typeof(EntityPropertyLongValue) &&
                            ((EntityPropertyLongValue)arrayNumProperty.Value).Value != elementCount)
                        {
                            Warnings.Add(string.Format("Line {0}: 'num' property value '{1}' doesn't match the array's actual element count '{2}'",
                                arrayNumPropertyLineNumber, ((EntityPropertyLongValue)arrayNumProperty.Value).Value, elementCount));
                        }

                        entityProperty.Value = arrayValue;
                    }

                    if (!IsValidPropertyName(propertyParts[0].Trim()))
                    {
                        Errors.Add(string.Format("Line {0}: invalid property name '{1}'", currentLineNumber, propertyParts[0].Trim()));
                        return null;
                    }

                    entityProperty.Name = propertyParts[0].Trim();
                }
            }

            return entityProperty;
        }

        /// <summary>
        /// Parses the value of a property
        /// </summary>
        /// <param name="valueText">text containing the value of the property</param>
        /// <param name="currentLineNumber">current line number reference in the file</param>
        /// <returns>an EntityPropertyValue containing the data from the parsed value</returns>
        internal EntityPropertyValue ParseEntityPropertyValue(string valueText, ref int currentLineNumber)
        {
            if (string.IsNullOrEmpty(valueText.Trim()))
            {
                Errors.Add(string.Format("Line {0}: missing property value", currentLineNumber));
                return null;
            }

            // Check for NULL values
            if (valueText.Equals("NULL"))
            {
                return new EntityPropertyNullValue();
            }

            // Check if it's a string
            if (valueText[0] == '"')
            {
                if (valueText[valueText.Length - 1] != '"')
                {
                    Errors.Add(string.Format("Line {0}: missing '\"'", currentLineNumber));
                    return null;
                }

                // Skip closing quotes
                string stringValue = valueText.Substring(1, valueText.Length - 2);

                return new EntityPropertyStringValue()
                {
                    Value = stringValue
                };
            }

            // Check for boolean values
            if (valueText.Equals("true") || valueText.Equals("false"))
            {
                bool boolValue = Boolean.Parse(valueText);

                return new EntityPropertyBooleanValue()
                {
                    Value = boolValue
                };
            }

            // Check for long values
            long intValue = 0;

            if (!long.TryParse(valueText, out intValue))
            {
                // If we can't parse it as a long, try as a double
                if (valueText.Contains("."))
                {
                    double doubleValue = 0;

                    if (!Double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out doubleValue))
                    {
                        Errors.Add(string.Format("Line {0}: invalid property value: {1}", currentLineNumber, valueText));
                        return null;
                    }

                    return new EntityPropertyDoubleValue()
                    {
                        Value = doubleValue
                    };
                }
            }

            return new EntityPropertyLongValue()
            {
                Value = intValue
            };
        }

        /// <summary>
        /// Checks if the given string is a valid name for properties
        /// </summary>
        /// <param name="text">string to check</param>
        /// <returns>true if the string is valid, false otherwise</returns>
        internal static bool IsValidPropertyName(string text)
        {
            return Regex.IsMatch(text, @"^[a-zA-Z0-9_/]+$") || Regex.IsMatch(text, @"^[a-zA-Z0-9_\/]+\[[0-9]+\]+$");
        }

        /// <summary>
        /// Checks if the given string is a valid name for entityDefs
        /// </summary>
        /// <param name="text">string to check</param>
        /// <returns>true if the string is valid, false otherwise</returns>
        internal static bool IsValidEntityDefName(string text)
        {
            return Regex.IsMatch(text, @"^[a-zA-Z0-9_/]+$");
        }
    }
}
