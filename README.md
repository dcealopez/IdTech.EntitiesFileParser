# IdTech.EntitiesFileParser

A C# library to parse/write idTech6 and idTech7 (older versions might work as well) .entities files

## How to use

You can either download the latest compiled library or clone this repository to add the project to your solution so that you can reference it in your own project.

## Documentation

Here's some basic documentation on how to read / write and manipulate idTech6/7 .entities files using this library.

### Reading a ".entities" file

Example on how to read an .entities file:

```cs
var entitiesFileReader = new EntitiesFileReader();

try
{
	var entitiesFileParseResult = entitiesFileReader.Parse("path_to_an_entities_file");
}
catch (Exception)
{
	// Handle the exception however you want
}
```

If no exceptions occur, the above code will return a **EntitiesFileParseResult** object that will contain the results from parsing the file (a list of errors, warnings, and the parsed .entities file in an EntitiesFile object)

Even if there are errors or warnings found while parsing an .entities file, the parser will still load the data but will skip everything that can't be parsed due to the errors.

### Manipulating a ".entities" file

Example on how to use the **EntitiesFileParseResult** object and how to manipulate the data from the parsed .entities file:

```cs
// Print all the errors and warnings found while parsing the .entities file
foreach (var error in entitiesFileParseResult.Errors)
{
	Console.WriteLine(error);
}

foreach (var warning in entitiesFileParseResult.Warnings)
{
	Console.WriteLine(warning);
}

// Add the "hide" flag to the entityDef of each entity
foreach (var entity in entitiesFileParseResult.EntitiesFile.Entities)
{
	if (entity.EntityDef == null)
	{
		continue;
	}

	// Look for the "edit" object property inside the entityDef
	foreach (var property in entity.EntityDef.Properties)
	{
		if (property.Name != "edit" && property.Value != null && property.Value.GetType() != typeof(EntityPropertyObjectValue))
		{
			continue;
		}

		// Check if the "flags" object property already exists
		bool flagsObjectPropertyFound = false;

		foreach (var editProperty in ((EntityPropertyObjectValue)property.Value).Value)
		{
			if (editProperty.Name != "flags" && editProperty.Value != null && editProperty.Value.GetType() != typeof(EntityPropertyObjectValue))
			{
				continue;
			}

			// If found, check that it doesn't have the "hide" property already set
			bool hidePropertyFound = false;
			flagsObjectPropertyFound = true;

			foreach (var flagsObjectProperty in ((EntityPropertyObjectValue)editProperty.Value).Value)
			{
				if (flagsObjectProperty.Name != "hide")
				{
					continue;
				}

				// If found, check that it its type is correct and set its value
				// If the type is not correct, we will change it
				hidePropertyFound = true;

				if (flagsObjectProperty.Value == null || flagsObjectProperty.Value.GetType() != typeof(EntityPropertyBooleanValue))
				{
					flagsObjectProperty.Value = new EntityPropertyBooleanValue()
					{
						Value = true
					};

					break;
				}

				((EntityPropertyBooleanValue)flagsObjectProperty.Value).Value = true;
			}

			// If the "hide" property was not found, create it
			// and add it to the flags object
			if (!hidePropertyFound)
			{
				var hideProperty = new EntityProperty()
				{
					Name = "hide",
					Value = new EntityPropertyBooleanValue()
					{
						Value = true
					}
				};

				((EntityPropertyObjectValue)editProperty.Value).Value.Add(hideProperty);
			}
		}

		// If the "flags" object was not found inside the "edit" object
		// create it and it's "hide" property, then add the "flags" object
		// to the "edit" object
		if (!flagsObjectPropertyFound)
		{
			var flagsProperty = new EntityProperty()
			{
				Name = "flags",
				Value = new EntityPropertyObjectValue()
				{
					Value = new List<EntityProperty>()
				}
			};

			((EntityPropertyObjectValue)property.Value).Value.Add(flagsProperty);

			var hideProperty = new EntityProperty()
			{
				Name = "hide",
				Value = new EntityPropertyBooleanValue()
				{
					Value = true
				}
			};

			((EntityPropertyObjectValue)flagsProperty.Value).Value.Add(hideProperty);
		}
	}
}
```

As you might have noticed in the above, each **EntityProperty** of the **EntityDef** of each **Entity** has a "Value" object that you will need to cast to the corresponding **EntityPropertyValue** type subclass in order to manipulate the value itself.

Here is a list of all the **EntityPropertyValue** subclasses with all the types that can be contained:

* EntityPropertyArrayValue
* EntityPropertyBooleanValue
* EntityPropertyDoubleValue
* EntityPropertyLongValue
* EntityPropertyNullValue
* EntityPropertyObjectValue
* EntityPropertyStringValue

### Writing a ".entities" file

Example on how to write an .entities file from an **EntitiesFile** object:

```cs
try
{
	EntitiesFileWriter.WriteEntitiesFile(entitiesFileParseResult.EntitiesFile, "entities_file_destination_path");
}
catch (Exception)
{
	// Handle the exception however you want
}
```
